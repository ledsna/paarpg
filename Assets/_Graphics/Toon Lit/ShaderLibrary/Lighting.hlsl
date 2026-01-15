#ifndef UNIVERSAL_LIGHTING_INCLUDED
#define UNIVERSAL_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
// #include "AmbientOcclusion.hlsl"
#include "Assets/_Graphics/Pixel Art/ShaderLibrary/Outlines.hlsl"

#if defined(LIGHTMAP_ON)
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
    #define OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION)
    #define OUTPUT_SH(normalWS, OUT)
#else
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
    #ifdef USE_APV_PROBE_OCCLUSION
        #define OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION) OUT.xyz = SampleProbeSHVertex(absolutePositionWS, normalWS, viewDir, OUT_OCCLUSION)
    #else
        #define OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION) OUT.xyz = SampleProbeSHVertex(absolutePositionWS, normalWS, viewDir)
    #endif
    // Note: This is the legacy function, which does not support APV.
    // Kept to avoid breaking shaders still calling it (UUM-37723)
    #define OUTPUT_SH(normalWS, OUT) OUT.xyz = SampleSHVertex(normalWS)
#endif

///////////////////////////////////////////////////////////////////////////////
//                      Lighting Functions                                   //
///////////////////////////////////////////////////////////////////////////////

half3 LightingPhysicallyBased(BRDFData brdfData,
    half3 lightColor, half3 lightDirectionWS, float distanceAttenuation, float shadowAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    bool specularHighlightsOff)
{

    half s = saturate(_SpecularStep + HALF_MIN);

    half NdotL = saturate(dot(normalWS, lightDirectionWS));

    float quantized_ndotl = pow(Quantize(_DiffuseSpecularCelShader ? _DiffuseSteps : -1, pow(NdotL, 1 / 2.2)), 2.2);

    // half3 diffuse = rcp(pow( Quantize(_DistanceSteps, sqrt(rcp(distanceAttenuation))) , 2 ));

    half3 diffuse = lightColor * quantized_ndotl *
        Quantize(_ShadowSteps, shadowAttenuation) * distanceAttenuation;
    half3 brdf = brdfData.diffuse;

#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        half specComponent = max(DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS), HALF_MIN);

        // half s1 = rcp(Quantize(3, rcp(specComponent)));
        // half s2 = Quantize(3, specComponent);

        half s3 = exp(floor(log(specComponent) / s) * s);
        
        brdf += brdfData.specular * s3;
    }
#endif // _SPECULARHIGHLIGHTS_OFF
    return brdf * diffuse;
}

half3 VertexLighting(float3 positionWS, half3 normalWS)
{
    half3 vertexLightColor = half3(0.0, 0.0, 0.0);

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    uint lightsCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    LIGHT_LOOP_BEGIN(lightsCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);

    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
    {
        half3 lightColor = light.color * light.distanceAttenuation;
        vertexLightColor += LightingLambert(lightColor, light.direction, normalWS);
    }

    LIGHT_LOOP_END
#endif

    return vertexLightColor;
}

struct LightingData
{
    half3 giColor;
    half3 mainLightColor;
    half3 additionalLightsColor;
    half3 emissionColor;
};

half3 CalculateLightingColor(LightingData lightingData, half outlineType)
{
    half3 lightingColor = 0;

    if (IsOnlyAOLightingFeatureEnabled())
        return lightingData.giColor; // Contains white + AO

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_GLOBAL_ILLUMINATION))
        lightingColor += lightingData.giColor;

    // return lightingColor;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT))
        lightingColor += lightingData.mainLightColor;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS))
        lightingColor += lightingData.additionalLightsColor;

    // blue for outer, red for inner, black for no outline
    if (_DebugOn)
    {
        return lerp(half3(0, 0, 0), half3(floor(outlineType / 2), 0, outlineType % 2), outlineType);
    }

    // Legacy: lit outlines are lighted, shadowed are dimmed
    // if (outlineType != 0)
    // {
    //     float factor = RGBtoHSV(lightingColor).b;
    //
    //     float final_factor = 0;
    //
    //     if (factor >= 0)
    //         final_factor = lerp(2.25, 2.75, _OutlineStrength);
    //     else
    //         final_factor = lerp(0, 0.5, 1 - _OutlineStrength);
    //     
    //     return final_factor * lightingColor;
    // }

    if (outlineType == 1)
        return lightingColor / (1 + _OutlineStrength);
    if (outlineType == 2)
        return lightingColor * (1 + _OutlineStrength);
    
    // Why is this even here? Could potentially break something, but leaving it for now
    // lightingColor *= albedo;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_EMISSION))
        lightingColor += lightingData.emissionColor;

    return lightingColor;
}
//.
half4 CalculateFinalColor(LightingData lightingData, half outlineType, half alpha)
{
    half3 finalColor = CalculateLightingColor(lightingData, outlineType);

    return half4(finalColor, alpha);
}

//.
LightingData CreateLightingData(InputData inputData, SurfaceData surfaceData)
{
    LightingData lightingData;

    lightingData.giColor = inputData.bakedGI;
    lightingData.emissionColor = surfaceData.emission;
    lightingData.mainLightColor = 0;
    lightingData.additionalLightsColor = 0;

    return lightingData;
}

///////////////////////////////////////////////////////////////////////////////
//                      Fragment Functions                                   //
//       Used by ShaderGraph and others builtin renderers                    //
///////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
/// PBR lighting...
////////////////////////////////////////////////////////////////////////////////
half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
        bool specularHighlightsOff = true;
    #else
        bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();

    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    half3 directSpecular = inputData.bakedGI * brdfData.diffuse;
    lightingData.giColor = directSpecular * aoFactor.indirectAmbientOcclusion;

    half outlineType = OutlineType(inputData.normalizedScreenSpaceUV);
    
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, mainLight.color,
            mainLight.direction, mainLight.distanceAttenuation, mainLight.shadowAttenuation,
            inputData.normalWS, inputData.viewDirectionWS, specularHighlightsOff);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, light.color, light.direction,
                light.distanceAttenuation, light.shadowAttenuation, inputData.normalWS, inputData.viewDirectionWS,
                specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, light.color, light.direction,
                light.distanceAttenuation, light.shadowAttenuation, inputData.normalWS, inputData.viewDirectionWS,
                specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif
    
    if (outlineType == 0)
        {
            half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
            half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));

            reflectVector = QuantizeDirectionSpherical(reflectVector, _ReflectionSteps, _ReflectionSteps);
            half fresnelTerm = Pow4(1.0 - Quantize(_FresnelSteps, NoV));

            half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, brdfData.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);

            lightingData.giColor += indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm) * aoFactor.indirectAmbientOcclusion;
        }

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(CalculateFinalColor(lightingData, outlineType, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, outlineType, surfaceData.alpha);
#endif
}
#endif