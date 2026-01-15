#ifndef GRASS_SHADER_INCLUDED
#define GRASS_SHADER_INCLUDED

#include "BillboardGpuInstance.hlsl"

// Texture array
TEXTURE2D_ARRAY(_TextureArray);
SAMPLER(sampler_TextureArray);

// Fallback texture if no array assigned
TEXTURE2D(_FallbackTexture);
SAMPLER(sampler_FallbackTexture);

Texture2D _CloudsCookie;
SamplerState sampler_CloudsCookie;
half _XOffsetScale;
half _ZOffsetScale;

// Wind Properties
float _WindSpeed;
float _WindStrength;
float4 _WindDirection;
float _WindFrequency;
float _WindGustStrength;

// Wild Grass Properties
float _WildNormalStrength;

// Flower Properties (size props are declared in BillboardGpuInstance.hlsl)
float _FlowerCameraNudge;

// Sample from texture array based on world position hash (for texture variation)
half4 SampleTextureArray(float2 uv, int precomputedTexIndex)
{
    float width, height, elements;
    _TextureArray.GetDimensions(width, height, elements);

    if (elements == 0)
        return half4(1, 1, 1, 1);

    int texIndex = clamp(precomputedTexIndex, 0, (int)elements - 1);
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_TextureArray, sampler_TextureArray, uv, texIndex, dx, dy);
}

// Hash function for deterministic randomness based on position
float Hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Generate a random 3D direction from a seed
float3 RandomDirection(float seed)
{
    float theta = Hash(float3(seed, seed * 2.0, seed * 3.0)) * 6.28318530718; // 0 to 2PI
    float phi = Hash(float3(seed * 4.0, seed * 5.0, seed * 6.0)) * 3.14159265359; // 0 to PI
    return float3(sin(phi) * cos(theta), cos(phi), sin(phi) * sin(theta));
}

// Apply wild grass normal perturbation if this grass is "wild"
float3 ApplyWildGrassNormal(float3 originalNormal, float3 worldPos)
{
    float hash = Hash(worldPos);
    
    // Check if this grass instance is "wild" based on chance
    if (hash < _WildGrassChance)
    {
        // Generate a random direction based on world position
        float3 randomDir = RandomDirection(hash * 1000.0);
        
        // Blend between original normal and random direction
        float3 perturbedNormal = lerp(originalNormal, randomDir, _WildNormalStrength);
        return normalize(perturbedNormal);
    }
    
    return originalNormal;
}

#ifndef UNIVERSAL_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Lighting.hlsl"

#if defined(_NORMALMAP)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// Calculate wind displacement for grass
float3 CalculateWindDisplacement(float3 worldPos, float vertexHeight)
{
    float time = _Time.y * _WindSpeed;
    float3 windDir = normalize(_WindDirection.xyz);
    
    // Base wind wave using world position
    float windWave = sin(time + (worldPos.x + worldPos.z) * _WindFrequency);
    
    // Add variation based on position for more natural look
    float windVariation = sin(time * 0.7 + worldPos.x * 0.5) * cos(time * 0.9 + worldPos.z * 0.5);
    
    // Wind gust (faster, stronger variation)
    float windGust = sin(time * 2.5 + worldPos.x * 2.0 + worldPos.z * 1.5) * _WindGustStrength;
    
    // Combine wind effects - multiply by vertex height so top of grass moves more
    float windAmount = (windWave * _WindStrength + windVariation * _WindStrength * 0.3 + windGust) * vertexHeight;
    
    return windDir * windAmount;
}

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

    half  fogFactor                 : TEXCOORD5;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
#if defined(_NORMALMAP)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    // ========================================
    // SHADOW SAMPLING EXPERIMENTS
    //
    // KEY VARIABLES:
    // - positionWS (global): Terrain root position where billboard is spawned
    // - inputData.positionWS: Actual billboard fragment position (after offset + wind)
    // ========================================

    float3 offset = normalWS * 0.2f;

    // positionWS += offset;

    // CURRENT (BASELINE): Use billboard's actual fragment position
    // inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

    // TERRAIN ONLY: Use terrain position (causes popping on vertical billboards)
    // inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);

    // ====== IDEA 1 VARIATIONS (Height-based lerp) ======

    // Since grass is created with an offset below the terrain:

    // IDEA 1A: Linear height-based lerp
    // float3 billboardOffset = inputData.positionWS - positionWS;
    // float heightAboveTerrain = length(billboardOffset);
    // float heightFactor = saturate(heightAboveTerrain / _Scale);
    // float3 shadowSamplePos = lerp(inputData.positionWS, positionWS, heightFactor);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // IDEA 1B: Exponential falloff (more terrain influence at bottom)
    // float3 billboardOffset = inputData.positionWS - positionWS;
    // float heightAboveTerrain = length(billboardOffset);
    // float heightFactor = saturate(heightAboveTerrain / _Scale);
    // heightFactor = heightFactor * heightFactor; // Square for exponential
    // float3 shadowSamplePos = lerp(inputData.positionWS, positionWS, heightFactor);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // IDEA 1C: Inverse exponential (more terrain influence at top)
    // float3 billboardOffset = inputData.positionWS - positionWS;
    // float heightAboveTerrain = length(billboardOffset);
    // float heightFactor = saturate(heightAboveTerrain / _Scale);
    // heightFactor = sqrt(heightFactor); // Square root for inverse exponential
    // float3 shadowSamplePos = lerp(inputData.positionWS, positionWS, heightFactor);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // IDEA 1D: Clamped blend (mostly terrain, with slight billboard influence)
    // float3 billboardOffset = inputData.positionWS - positionWS;
    // float heightAboveTerrain = length(billboardOffset);
    // float heightFactor = saturate(heightAboveTerrain / _Scale);
    // float blendFactor = heightFactor * 0.3; // Only 30% max billboard influence
    // float3 shadowSamplePos = lerp(inputData.positionWS, positionWS, blendFactor);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // IDEA 1E: Use UV.y instead of world height (might be more stable)
    float heightFactor = input.uv.y;
    float3 shadowSamplePos = lerp(inputData.positionWS, positionWS, 0);
    inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // ====== OTHER IDEAS ======

    // IDEA 2: Simple 50/50 blend between terrain and billboard position
    // float3 shadowSamplePos = lerp(positionWS, inputData.positionWS, 0.5);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // IDEA 3: Dithered selection between terrain and billboard position
    // float hash = frac(sin(dot(positionWS.xz, float2(12.9898, 78.233))) * 43758.5453);
    // inputData.shadowCoord = TransformWorldToShadowCoord(lerp(positionWS, inputData.positionWS, step(0.5, hash)));

    // IDEA 4: Project onto terrain Y height, keep billboard XZ position
    // float3 shadowSamplePos = float3(inputData.positionWS.x, positionWS.y, inputData.positionWS.z);
    // inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);

    // positionWS -= offset;

#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactor);

#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(positionWS),
        normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.positionCS = input.positionCS;
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Backface cull grass based on terrain normal - do this FIRST before any calculations
    float3 viewDirToCamera = GetWorldSpaceNormalizeViewDir(positionWS);
    float terrainFacing = dot(normalWS, viewDirToCamera);
    if (terrainFacing < -0.2)
    {
        output.positionCS = float4(0, 0, 0, 0);
        return output;
    }

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    float3 windDisplacement = CalculateWindDisplacement(vertexInput.positionWS, input.texcoord.y);
    vertexInput.positionWS += windDisplacement;

    #if defined(_USE_TEXTURE_COLOR)
        bool shouldNudge = true;
        float nudgeAmount = _FlowerCameraNudge;
    #else
        bool shouldNudge = isWildGrass;
        float nudgeAmount = 0.02;
    #endif

    if (shouldNudge)
    {
        vertexInput.positionWS -= normalize(viewDirToCamera) * nudgeAmount;
    }

    vertexInput.positionCS = TransformWorldToHClip(vertexInput.positionWS);
    
    // Apply wild grass normal perturbation (use grass root position for deterministic randomness)
    // normalWS is already in world space from GrassData (from the underlying mesh)
    float3 perturbedNormal = isWildGrass ? ApplyWildGrassNormal(normalWS, positionWS) : normalWS;
    
    // For tangents, we still need to process the billboard's own tangent from OS
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // Use the perturbed normal from the underlying mesh (already in WS)
    output.normalWS = normalize(perturbedNormal);
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

    // Use lightmapUV from the underlying mesh (set in Setup() function)
    // IMPORTANT: lightmapUV is already in correct lightmap space from the underlying mesh
    // Do NOT apply unity_LightmapST as it belongs to the billboard renderer, not the terrain
#if defined(LIGHTMAP_ON)
    output.staticLightmapUV = lightmapUV;
#endif
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
    OUTPUT_SH(normalWS, output.vertexSH);
    output.fogFactor = fogFactor;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    
    output.positionCS = vertexInput.positionCS;
    return output;
}

// Used in Standard (Physically Based) shader
void LitPassFragment(
    Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 textureColor = SampleTextureArray(input.uv, textureIndex);

    // Early alpha clip
    clip(textureColor.a - 0.5);

    SurfaceData surfaceData;

    #if defined(_USE_TEXTURE_COLOR)
        // For flowers: Use texture array as the color source
        // Initialize surface data manually without _BaseColor tinting
        surfaceData.alpha = Alpha(textureColor.a, half4(1,1,1,1), _Cutoff);
        surfaceData.albedo = textureColor.rgb; // Use raw texture color from texture array

        half4 specGloss = SampleMetallicSpecGloss(input.uv);
        surfaceData.metallic = specGloss.r;
        surfaceData.specular = half3(0.0, 0.0, 0.0);
        surfaceData.smoothness = specGloss.a;
        surfaceData.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
        surfaceData.occlusion = SampleOcclusion(input.uv);
        surfaceData.emission = SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
        surfaceData.clearCoatMask = half(0.0);
        surfaceData.clearCoatSmoothness = half(0.0);
    #else
        // For grass: Use normal initialization which multiplies by _BaseColor
        InitializeStandardLitSurfaceData(input.uv, surfaceData);
    #endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    inputData.normalWS = normalWS;
    inputData.positionWS = positionWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    // inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(TransformWorldToHClip(positionWS));
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    half3 colour = color.rgb;
 
    // Value/Saturation cel shading removed - was unused
    
    colour = MixFog(colour, inputData.fogCoord);

    outColor.rgb = colour;
    outColor.a = 1;

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

#endif

// --------------------------
#endif
