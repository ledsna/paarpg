#ifndef UNIVERSAL_REALTIME_LIGHTS_INCLUDED
#define UNIVERSAL_REALTIME_LIGHTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LightCookie/LightCookie.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Clustering.hlsl"

// LEDSNA
// ------
float3 _OffsetWS;
// ------

// Abstraction over Light shading data.
struct Light
{
    half3   direction;
    half3   color;
    float   distanceAttenuation; // full-float precision required on some platforms
    half    shadowAttenuation;
    uint    layerMask;
};

#if USE_CLUSTER_LIGHT_LOOP && defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
#define CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK if (_AdditionalLightsColor[lightIndex].a > 0.0h) continue;
#else
#define CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
#endif

#if USE_CLUSTER_LIGHT_LOOP
    #define LIGHT_LOOP_BEGIN(lightCount) { \
    uint lightIndex; \
    ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0); \
    [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) { \
        lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT; \
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
    #define LIGHT_LOOP_END } }
#else
    #define LIGHT_LOOP_BEGIN(lightCount) \
    for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex) {
    #define LIGHT_LOOP_END }
#endif

///////////////////////////////////////////////////////////////////////////////
//                        Attenuation Functions                               /
///////////////////////////////////////////////////////////////////////////////

// Matches Unity Vanilla HINT_NICE_QUALITY attenuation
// Attenuation smoothly decreases to light range.
float DistanceAttenuation(float distanceSqr, half2 distanceAttenuation)
{
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    float lightAtten = rcp(distanceSqr);
    float2 distanceAttenuationFloat = float2(distanceAttenuation);

    // Use the smoothing factor also used in the Unity lightmapper.
    half factor = half(distanceSqr * distanceAttenuationFloat.x);
    half smoothFactor = saturate(half(1.0) - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;

    return lightAtten * smoothFactor;
}

half AngleAttenuation(half3 spotDirection, half3 lightDirection, half2 spotAttenuation)
{
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}

///////////////////////////////////////////////////////////////////////////////
//                      Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////

Light GetMainLight()
{
    Light light;
    light.direction = half3(_MainLightPosition.xyz);
#if USE_CLUSTER_LIGHT_LOOP
#if defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
    light.distanceAttenuation = _MainLightColor.a;
#else
    light.distanceAttenuation = 1.0;
#endif
#else
    light.distanceAttenuation = unity_LightData.z; // unity_LightData.z is 1 when not culled by the culling mask, otherwise 0.
#endif
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;

    light.layerMask = _MainLightLayerMask;

    return light;
}

Light GetMainLight(float4 shadowCoord)
{
    Light light = GetMainLight();
    light.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
    return light;
}

// LEDSNA
// ------
#ifndef QUANTIZE_INCLUDED
#define QUANTIZE_INCLUDED

float3 RGBtoHSV(float3 In)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float  E = 1e-10;
    return float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), Q.x);
}

float3 HSVtoRGB(float3 In)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
    return In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
}

float SmoothRound(float x, float r)
{
    float p = 1 / (1 - r);
    float a = pow(2 * x, p);
    float b = 2 - pow(2 - 2 * x, p);
    float c = sign(x - 0.5) + 1;
    float d = (a * (2 - c) + b * c) / 4;
    return d;
}

real3 NonZero(real3 vec)
{
    return real3(
        max(vec.x, 1e-6),
        max(vec.y, 1e-6),
        max(vec.z, 1e-6));
}

float3 DeferredCel(float3 final_color, float3 base_color, float palette_size)
{
    float3 f = RGBtoHSV(final_color.rgb / NonZero(base_color.rgb));
    // hue
    f.r *= palette_size;
    f.r = floor(f.r) + SmoothRound(frac(f.r), 1);
    f.r /= palette_size;

    // saturation
    f.g *= 10;
    f.g = SmoothRound(frac(f.g), 1) + floor(f.g);
    f.g /= 10;
    // f.g  = Quantize(_SaturationSteps, f.g);

    // value
    float v = log2(f.b);
    v *= .5;
    v = floor(v) + SmoothRound(frac(v), 1);
    v /= .5;
    f.b = pow(2, v);
    return NonZero(base_color.rgb) * HSVtoRGB(f);
}

real3 QuantizeDirectionSpherical(real3 dir, real levelsTheta, real levelsPhi)
{
    real theta = acos(clamp(dir.y, -1.0, 1.0));      // elevation
    real phi   = atan2(dir.z, dir.x);                // azimuth

    theta = floor(theta * levelsTheta / PI) * (PI / levelsTheta);
    phi   = floor((phi + PI) * levelsPhi / (2.0 * PI)) * (2.0 * PI / levelsPhi) - PI;

    float3 result;
    result.x = sin(theta) * cos(phi);
    result.y = cos(theta);
    result.z = sin(theta) * sin(phi);
    return result;
}


real Quantize(real steps, real shade)
{
    if (steps == -1) return shade;
    if (steps == 0) return 0;
    if (steps == 1) return 1;

    shade = floor(shade * (steps)) / (steps - 1);
    return shade;
}

real3 Quantize(real steps, real3 shade)
{
    if (steps == -1) return shade;
    if (steps == 0) return 0;
    if (steps == 1) return 1;

    shade = floor(shade * (steps)) / (steps - 1);
    return shade;
}
#endif

// LEDSNA
float2 ComputeDitherUVs(float3 positionWS, float4 positionCS)
{
    float3 worldPosDiff = positionWS - mul(UNITY_MATRIX_I_VP, positionCS).xyz;
    if (unity_OrthoParams.w)
        return TransformWorldToHClipDir(positionWS - worldPosDiff).xy * 0.5 + 0.5;
    
    float4 hclipPosition = TransformWorldToHClip(positionWS - worldPosDiff);
    return hclipPosition.xy / hclipPosition.w * 0.5 + 0.5;
}

float Dither(float In, float2 ScreenPosition)
{
    float2 pixelPos = ScreenPosition * float2(641, 361);
    
    uint    x       = (pixelPos.x % 4 + 4) % 4;
    uint    y       = (pixelPos.y % 4 + 4) % 4;
    uint    index     = x * 4 + y;

    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };
    return In - DITHER_THRESHOLDS[index];
}

float GetCloudShadow(float3 positionWS)
{
    float cookieColor = SampleMainLightCookie(positionWS).r;
    if (cookieColor.x < 0.75)
        cookieColor = 0;
    else if (cookieColor.x > 0.95f)
        cookieColor = 1;
    else
    {
        cookieColor = (cookieColor.x - 0.75) * 10;
        // cookieColor = Dither(cookieColor.x, ComputeDitherUVs(positionWS, positionCS));
        // if (smoothness < 0.75)
        cookieColor = Quantize(7, saturate(cookieColor));

    }
    return saturate(cookieColor);
}
// ------

Light GetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)
{
    Light light = GetMainLight();
    light.shadowAttenuation = MainLightShadow(shadowCoord, positionWS, shadowMask, _MainLightOcclusionProbes);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = GetCloudShadow(positionWS);
        // real3 cookieColor = SampleMainLightCookie(positionWS);
        light.color *= cookieColor;
    #endif

    return light;
}

Light GetMainLight(InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    Light light = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);

    #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
    {
        light.color *= aoFactor.directAmbientOcclusion;
    }
    #endif

    return light;
}

// Fills a light struct given a perObjectLightIndex
Light GetAdditionalPerObjectLight(int perObjectLightIndex, float3 positionWS)
{
    // Abstraction over Light input constants
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 lightPositionWS = _AdditionalLightsBuffer[perObjectLightIndex].position;
    half3 color = _AdditionalLightsBuffer[perObjectLightIndex].color.rgb;
    half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[perObjectLightIndex].attenuation;
    half4 spotDirection = _AdditionalLightsBuffer[perObjectLightIndex].spotDirection;
    uint lightLayerMask = _AdditionalLightsBuffer[perObjectLightIndex].layerMask;
#else
    float4 lightPositionWS = _AdditionalLightsPosition[perObjectLightIndex];
    half3 color = _AdditionalLightsColor[perObjectLightIndex].rgb;
    half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
    half4 spotDirection = _AdditionalLightsSpotDir[perObjectLightIndex];
    uint lightLayerMask = asuint(_AdditionalLightsLayerMasks[perObjectLightIndex]);
#endif

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    // full-float precision required on some platforms
    float attenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy) * AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw);

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0; // This value can later be overridden in GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
    light.color = color;
    light.layerMask = lightLayerMask;

    return light;
}

uint GetPerObjectLightIndexOffset()
{
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    return uint(unity_LightData.x);
#else
    return 0;
#endif
}

// Returns a per-object index given a loop index.
// This abstract the underlying data implementation for storing lights/light indices
int GetPerObjectLightIndex(uint index)
{
/////////////////////////////////////////////////////////////////////////////////////////////
// Structured Buffer Path                                                                   /
//                                                                                          /
// Lights and light indices are stored in StructuredBuffer. We can just index them.         /
// Currently all non-mobile platforms take this path :(                                     /
// There are limitation in mobile GPUs to use SSBO (performance / no vertex shader support) /
/////////////////////////////////////////////////////////////////////////////////////////////
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    uint offset = uint(unity_LightData.x);
    return _AdditionalLightsIndices[offset + index];

/////////////////////////////////////////////////////////////////////////////////////////////
// UBO path                                                                                 /
//                                                                                          /
// We store 8 light indices in half4 unity_LightIndices[2];                                 /
// Due to memory alignment unity doesn't support int[] or float[]                           /
// Even trying to reinterpret cast the unity_LightIndices to float[] won't work             /
// it will cast to float4[] and create extra register pressure. :(                          /
/////////////////////////////////////////////////////////////////////////////////////////////
#else
    // since index is uint shader compiler will implement
    // div & mod as bitfield ops (shift and mask).

    // TODO: Can we index a float4? Currently compiler is
    // replacing unity_LightIndicesX[i] with a dp4 with identity matrix.
    // u_xlat16_40 = dot(unity_LightIndices[int(u_xlatu13)], ImmCB_0_0_0[u_xlati1]);
    // This increases both arithmetic and register pressure.
    //
    // NOTE: min16float4 bug workaround.
    // Take the "vec4" part into float4 tmp variable in order to force float4 math.
    // It appears indexing half4 as min16float4 on DX11 can fail. (dp4 {min16f})
    float4 tmp = unity_LightIndices[index / 4];
    return int(tmp[index % 4]);
#endif
}

// Fills a light struct given a loop i index. This will convert the i
// index to a perObjectLightIndex
Light GetAdditionalLight(uint i, float3 positionWS)
{
#if USE_CLUSTER_LIGHT_LOOP
    int lightIndex = i;
#else
    int lightIndex = GetPerObjectLightIndex(i);
#endif
    return GetAdditionalPerObjectLight(lightIndex, positionWS);
}

Light GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
{
#if USE_CLUSTER_LIGHT_LOOP
    int lightIndex = i;
#else
    int lightIndex = GetPerObjectLightIndex(i);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, positionWS);

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
#else
    half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
#endif
    light.shadowAttenuation = AdditionalLightShadow(lightIndex, positionWS, light.direction, shadowMask, occlusionProbeChannels);
#if defined(_LIGHT_COOKIES)
    real3 cookieColor = SampleAdditionalLightCookie(lightIndex, positionWS);
    light.color *= cookieColor;
#endif

    return light;
}

Light GetAdditionalLight(uint i, InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    Light light = GetAdditionalLight(i, inputData.positionWS, shadowMask);

    #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
    {
        light.color *= aoFactor.directAmbientOcclusion;
    }
    #endif

    return light;
}

int GetAdditionalLightsCount()
{
#if USE_CLUSTER_LIGHT_LOOP
    // Counting the number of lights in clustered requires traversing the bit list, and is not needed up front.
    return 0;
#else
    // TODO: we need to expose in SRP api an ability for the pipeline cap the amount of lights
    // in the culling. This way we could do the loop branch with an uniform
    // This would be helpful to support baking exceeding lights in SH as well
    return int(min(_AdditionalLightsCount.x, unity_LightData.y));
#endif
}

half4 CalculateShadowMask(InputData inputData)
{
    // To ensure backward compatibility we have to avoid using shadowMask input, as it is not present in older shaders
    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
    half4 shadowMask = inputData.shadowMask; // Shadowmask was sampled from lightmap
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    half4 shadowMask = inputData.shadowMask; // Shadowmask (probe occlusion) was sampled from APV
    #elif !defined (LIGHTMAP_ON)
    half4 shadowMask = unity_ProbesOcclusion; // Sample shadowmask (probe occlusion) from legacy probes
    #else
    half4 shadowMask = half4(1, 1, 1, 1); // Fallback shadowmask, fully unoccluded
    #endif

    return shadowMask;
}

#endif