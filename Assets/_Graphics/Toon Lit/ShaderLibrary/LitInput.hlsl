#ifndef UNIVERSAL_LIT_INPUT_INCLUDED
#define UNIVERSAL_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
CBUFFER_START(UnityPerMaterial)

half _DepthThreshold;
half _NormalsThreshold;
half _ExternalScale;
half _InternalScale;

half _DebugOn;
half _External;
half _Convex;
half _Concave;
half _OutlineStrength;
half4 _OutlineColour;

half _DiffuseSpecularCelShader;

half _DiffuseSteps;
half _FresnelSteps;
half _SpecularStep;

half _DistanceSteps;
half _ShadowSteps;
half _ReflectionSteps;

float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _Surface;
CBUFFER_END

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)

UNITY_DOTS_INSTANCED_PROP(float , _DepthThreshold)
UNITY_DOTS_INSTANCED_PROP(float , _NormalsThreshold)
UNITY_DOTS_INSTANCED_PROP(float , _ExternalScale)
UNITY_DOTS_INSTANCED_PROP(float , _InternalScale)

UNITY_DOTS_INSTANCED_PROP(float , _DebugOn)
UNITY_DOTS_INSTANCED_PROP(float , _External)
UNITY_DOTS_INSTANCED_PROP(float , _Convex)
UNITY_DOTS_INSTANCED_PROP(float , _Concave)
UNITY_DOTS_INSTANCED_PROP(float , _OutlineStrength)
UNITY_DOTS_INSTANCED_PROP(float4 , _OutlineColour)

UNITY_DOTS_INSTANCED_PROP(float , _DiffuseSpecularCelShader)

UNITY_DOTS_INSTANCED_PROP(float , _DiffuseSteps)
UNITY_DOTS_INSTANCED_PROP(float , _FresnelSteps)
UNITY_DOTS_INSTANCED_PROP(float , _SpecularStep)

UNITY_DOTS_INSTANCED_PROP(float , _DistanceSteps)
UNITY_DOTS_INSTANCED_PROP(float , _ShadowSteps)
UNITY_DOTS_INSTANCED_PROP(float , _ReflectionSteps)

    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.

static float unity_DOTS_Sampled_DepthThreshold;
static float unity_DOTS_Sampled_NormalsThreshold;
static float unity_DOTS_Sampled_ExternalScale;
static float unity_DOTS_Sampled_InternalScale;

static float unity_DOTS_Sampled_DebugOn;
static float unity_DOTS_Sampled_External;
static float unity_DOTS_Sampled_Convex;
static float unity_DOTS_Sampled_Concave;
static float unity_DOTS_Sampled_OutlineStrength;
static float4 unity_DOTS_Sampled_OutlineColour;

static float unity_DOTS_Sampled_DiffuseSpecularCelShader;

static float unity_DOTS_Sampled_DiffuseSteps;
static float unity_DOTS_Sampled_FresnelSteps;
static float unity_DOTS_Sampled_SpecularStep;

static float unity_DOTS_Sampled_DistanceSteps;
static float unity_DOTS_Sampled_ShadowSteps;
static float unity_DOTS_Sampled_ReflectionSteps;

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_OcclusionStrength;
static float  unity_DOTS_Sampled_Surface;

void SetupDOTSLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_DepthThreshold       = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DepthThreshold);
    unity_DOTS_Sampled_NormalsThreshold     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _NormalsThreshold);
    unity_DOTS_Sampled_ExternalScale       = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ExternalScale);
    unity_DOTS_Sampled_InternalScale       = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _InternalScale);
    
    unity_DOTS_Sampled_DebugOn              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DebugOn);
    unity_DOTS_Sampled_External             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _External);
    unity_DOTS_Sampled_Convex               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Convex);
    unity_DOTS_Sampled_Concave              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Concave);
    unity_DOTS_Sampled_OutlineStrength      = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OutlineStrength);
    unity_DOTS_Sampled_OutlineColour        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColour);
    
    unity_DOTS_Sampled_DiffuseSpecularCelShader = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DiffuseSpecularCelShader);

    unity_DOTS_Sampled_DiffuseSteps         = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DiffuseSteps);
    unity_DOTS_Sampled_FresnelSteps         = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FresnelSteps);
    unity_DOTS_Sampled_SpecularStep         = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _SpecularStep);

    unity_DOTS_Sampled_DistanceSteps        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DistanceSteps);
    unity_DOTS_Sampled_ShadowSteps          = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShadowSteps);
    unity_DOTS_Sampled_ReflectionSteps      = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ReflectionSteps);

    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_SpecColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
    unity_DOTS_Sampled_EmissionColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Cutoff               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
    unity_DOTS_Sampled_Surface              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitMaterialPropertyCaches()

#define _DepthThreshold         unity_DOTS_Sampled_DepthThreshold
#define _NormalsThreshold       unity_DOTS_Sampled_NormalsThreshold
#define _ExternalScale          unity_DOTS_Sampled_ExternalScale
#define InternalScale          unity_DOTS_Sampled_InternalScale

#define _DebugOn                unity_DOTS_Sampled_DebugOn
#define _External               unity_DOTS_Sampled_External
#define _Convex                 unity_DOTS_Sampled_Convex
#define _Concave                unity_DOTS_Sampled_Concave
#define _OutlineStrength        unity_DOTS_Sampled_OutlineStrength
#define _OutlineColour          unity_DOTS_Sampled_OutlineColour

#define _DiffuseSpecularCelShader unity_DOTS_Sampled_DiffuseSpecularCelShader

#define _DiffuseSteps           unity_DOTS_Sampled_DiffuseSteps
#define _FresnelSteps           unity_DOTS_Sampled_FresnelSteps
#define _SpecularStep           unity_DOTS_Sampled_SpecularStep

#define _DistanceSteps          unity_DOTS_Sampled_DistanceSteps
#define _ShadowSteps            unity_DOTS_Sampled_ShadowSteps
#define _ReflectionSteps        unity_DOTS_Sampled_ReflectionSteps

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _SpecColor              unity_DOTS_Sampled_SpecColor
#define _EmissionColor          unity_DOTS_Sampled_EmissionColor
#define _Cutoff                 unity_DOTS_Sampled_Cutoff
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic
#define _BumpScale              unity_DOTS_Sampled_BumpScale
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength
#define _Surface                unity_DOTS_Sampled_Surface

#endif

TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

half4 SampleMetallicSpecGloss(float2 uv)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
    specGloss.a *= _Smoothness;
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif
    specGloss.a = _Smoothness;
#endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
        return LerpWhiteTo(occ, _OcclusionStrength);
    #else
        return half(1.0);
    #endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
