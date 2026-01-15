#ifndef BILLBOARD_DEPTH_NORMALS_PASS_INCLUDED
#define BILLBOARD_DEPTH_NORMALS_PASS_INCLUDED

#include "BillboardGpuInstance.hlsl"

// Texture array - must match ForwardPass
TEXTURE2D_ARRAY(_TextureArray);
SAMPLER(sampler_TextureArray);

// Wind Properties (need to match ForwardPass)
float _WindSpeed;
float _WindStrength;
float4 _WindDirection;
float _WindFrequency;
float _WindGustStrength;

// Flower Properties (need to match ForwardPass)
float _FlowerCameraNudge;

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Import wind calculation from ForwardPass
float3 CalculateWindDisplacement(float3 worldPos, float vertexHeight)
{
    float time = _Time.y * _WindSpeed;
    float3 windDir = normalize(_WindDirection.xyz);
    float windWave = sin(time + (worldPos.x + worldPos.z) * _WindFrequency);
    float windVariation = sin(time * 0.7 + worldPos.x * 0.5) * cos(time * 0.9 + worldPos.z * 0.5);
    float windGust = sin(time * 2.5 + worldPos.x * 2.0 + worldPos.z * 1.5) * _WindGustStrength;
    float windAmount = (windWave * _WindStrength + windVariation * _WindStrength * 0.3 + windGust) * vertexHeight;
    return windDir * windAmount;
}

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

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    float3 normal       : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float3 positionWS   : TEXCOORD1; // For texture array sampling
    float3 normalWS     : TEXCOORD2;
    nointerpolation int textureIndex : TEXCOORD3;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Backface cull grass based on terrain normal - do this FIRST
    // Note: normalWS is global from Setup() in BillboardGpuInstance.hlsl
    float3 viewDirToCamera = GetWorldSpaceNormalizeViewDir(positionWS);
    float terrainFacing = dot(normalWS, viewDirToCamera);
    if (terrainFacing < -0.2)
    {
        output.positionCS = float4(0, 0, 0, 0);
        return output;
    }

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);

    float3 windDisplacement = CalculateWindDisplacement(vertexInput.positionWS, input.texcoord.y);
    vertexInput.positionWS += windDisplacement;

    #if defined(_USE_TEXTURE_COLOR)
        bool shouldNudge = true;
        float nudgeAmount = _FlowerCameraNudge;
        
        if (shouldNudge)
        {
            float3 viewDir = _WorldSpaceCameraPos - vertexInput.positionWS;
            float dist = length(viewDir);
            float3 nudgeDir = viewDir / dist;
            
            // Only nudge if close enough
            float nudgeFactor = saturate(1.0 - (dist - 0.5) / 5.0);
            vertexInput.positionWS -= nudgeDir * nudgeAmount * nudgeFactor * input.texcoord.y;
        }
    #endif

    output.positionWS = vertexInput.positionWS;
    output.positionCS = TransformWorldToHClip(vertexInput.positionWS);
    
    // Billboard normal calculation
    // For billboards, we usually want the normal to face the camera or be the terrain normal
    // Here we use the terrain normal (normalWS from Setup) but maybe modified by wind?
    // For now, let's just use the terrain normal passed from Setup()
    output.normalWS = normalWS; 
    output.textureIndex = textureIndex;

    return output;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        half4 clipSample = SampleTextureArray(input.uv, input.textureIndex);
        clip(clipSample.a - 0.5);
    #endif

    // "Tad-aa!"
    // Hijack _CameraNormalsTexture:
    // RGB = World Position
    // A   = Smoothness
    // We add 2.0 to smoothness to flag this pixel as a billboard for the Water shader
    outNormalWS = half4(positionWS, _Smoothness + 2.0);
}

#endif
