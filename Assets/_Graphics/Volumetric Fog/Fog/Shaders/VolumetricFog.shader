Shader "Ledsna/VolumetricShader"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "DisableBatching"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Blend Off

        Pass
        {
            Name "Volumetric Fog"
            
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            // #pragma shader_feature_local_fragment FOG_BLENDING 
            #pragma multi_compile_fragment _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #include "../ShaderLibrary/HelpFunctions.hlsl"
            #include "../ShaderLibrary/PhaseFunction.hlsl"

            // Currently I decided to set num steps to light to constant value 8. More than 8 have almost zero defference
            // 4 is look little worse than 8, but seems to be fine.
            // less than 4 look bad
            #define NUM_STEPS_LIGHT 8
            #define NUM_STEPS_LIGHT_RCP 1 / NUM_STEPS_LIGHT
            
            #pragma region Variables


            float4x4 _Custom_I_VP;
            // Global
            // ------
            float3 _BoundsMin;
            float3 _BoundsMax;
            // ------

            // Textures
            // --------
            FRAMEBUFFER_INPUT_X_FLOAT(0); // Color Texture
            Texture3D _ShapeTex;
            Texture3D _DetailTex;
            SamplerState sampler_ShapeTex;
            SamplerState sampler_DetailTex;

            float _ShapeScale;
            float3 _ShapeOffset;

            float _DetailScale;
            float3 _DetailOffset;
            // --------

            // Clouds
            // ------
            float _ContainerEdgeFadeDst;
            float4 _ShapeWeights;
            float4 _DetailWeights;
            float _DensityOffset;
            float _DensityMultiplier;
            float _DetailMultiplier;
            int _NumSteps;
            int _NumStepsLight;
            // ------

            // Lighting
            // --------
            // _PhaseParams:
            // .x — forward scattering
            // .y — back scattering
            // .z — base brightness
            // .w — phase factor
            float4 _PhaseParams;
            float _LightAbsorptionThroughCloud;
            float _LightAbsorptionTowardSun;
            float _DarknessThreshold;
            // --------

            // Wind
            // ---------
            float _ShapeSpeed;
            float _DetailSpeed;
            float3 _WindDir;
            // ---------

            // Height Fog removed - use 3D noise only
            
            // Point Lights
            // ------------
            float _EnablePointLights;
            int _MaxPointLights;
            int _PointLightExtraSamples;
            float _PointLightExtraThreshold;
            // ------------
            
            // Quality
            // -------
            float _MaxStepSize;
            // -------
            
            // Edge Fade
            // ---------
            float _TopFadeStrength;
            float _VerticalFadeMultiplier;
            // ---------

            // Other
            // -----
            // Calculating inverse direction of main light on CPU once for better performance on GPU side
            float3 _MainLightInvDir;
            float3 _FogColor;
            // -----
            

            #pragma endregion
            
            // Smooth edge fade to prevent hard cutoffs at container boundaries
            // Returns [0, 1] where 0 = fully faded, 1 = full density
            float ContainerEdgeFade(float3 position)
            {
                if (_ContainerEdgeFadeDst < 0.001)
                    return 1.0;
                    
                float3 boundsCenter = (_BoundsMin + _BoundsMax) * 0.5;
                float3 boundsExtents = (_BoundsMax - _BoundsMin) * 0.5;
                
                // Calculate distance from each edge (positive = inside)
                float3 distFromMin = position - _BoundsMin;
                float3 distFromMax = _BoundsMax - position;
                
                // Horizontal edge fade (X and Z axes)
                float fadeX = min(distFromMin.x, distFromMax.x);
                float fadeZ = min(distFromMin.z, distFromMax.z);
                float horizontalFade = min(fadeX, fadeZ);
                horizontalFade = saturate(horizontalFade / _ContainerEdgeFadeDst);
                
                // Top edge fade only (keep bottom thick)
                float topFadeDist = _ContainerEdgeFadeDst * _VerticalFadeMultiplier;
                float topFade = saturate(distFromMax.y / topFadeDist);
                // Apply smoothstep for more natural falloff at top
                topFade = lerp(topFade, smoothstep(0, 1, topFade), _TopFadeStrength);

                // Only top edge fade (keep bottom thick)
                float verticalFade = topFade;
                
                // Use smoothstep for extra-smooth transition
                return smoothstep(0, 1, min(horizontalFade, verticalFade));
            }

            // Height attenuation removed; we use pure 3D noise for density
            // Sample point lights at a position - scatter + shadowed contribution
            float3 SamplePointLights(float3 worldPos, float3 viewDir)
            {
                if (_EnablePointLights < 0.5 || _MaxPointLights <= 0)
                    return float3(0, 0, 0);

                float3 totalLight = float3(0, 0, 0);

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                uint maxLights = min(lightCount, (uint)_MaxPointLights);

                for (uint i = 0; i < maxLights; i++)
                {
                    Light light = GetAdditionalLight(i, worldPos);
                    float atten = light.distanceAttenuation;
                    if (atten < 0.001)
                        continue;

                    // Direction from sample point to light (approx)
                    float3 L = normalize(light.direction);

                    // Phase function (use same params as directional light)
                    float cosTheta = dot(viewDir, L);
                    float phaseVal = phase(cosTheta, _PhaseParams);

                    // Sample per-light shadow if available
                    float shadow = 1.0;
                    #if defined(ADDITIONAL_LIGHT_SHADOWS)
                        shadow = AdditionalLightRealtimeShadow(i, worldPos, L, GetAdditionalLightShadowParams(i), GetAdditionalLightShadowSamplingData(i));
                    #endif

                    // Use Unity light color directly (color already encodes brightness)
                    totalLight += light.color * atten * phaseVal * shadow;
                }
                #endif

                return totalLight;
            }
            
            float sampleDensity(float3 position)
            {
                const float baseScale = 0.001;
                const float offsetScale = 0.01;
                float time = _Time.x;
                
                // Sample position with wind offset
                float3 uvw = position * baseScale * _ShapeScale;
                float3 shapeSamplePos = uvw + _ShapeOffset * offsetScale + time * _ShapeSpeed * _WindDir;
                
                // Calculate base shape density
                float4 shapeNoise = _ShapeTex.Sample(sampler_ShapeTex, shapeSamplePos);
                float4 normalizedShapeWeights = _ShapeWeights / dot(_ShapeWeights, 1);
                float shapeFBM = dot(shapeNoise, normalizedShapeWeights);
                float baseShapeDensity = shapeFBM + _DensityOffset * 0.1;
                
                if (baseShapeDensity <= 0)
                    return 0;
                
                // Sample detail noise
                float3 detailSamplePos = uvw * _DetailScale + _DetailOffset * offsetScale + time * _DetailSpeed * _WindDir;
                float4 detailNoise = _DetailTex.Sample(sampler_DetailTex, detailSamplePos);
                float4 normalizedDetailWeights = _DetailWeights / dot(_DetailWeights, 1);
                float detailFBM = dot(detailNoise, normalizedDetailWeights);

                // Erode edges more than center
                float oneMinusShape = 1 - shapeFBM;
                float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
                float cloudDensity = baseShapeDensity - (1 - detailFBM) * detailErodeWeight * _DetailMultiplier;
                
                if (cloudDensity <= 0)
                    return 0;
                
                // No height attenuation — keep density based on 3D noise and edge fade
                
                // Apply smooth edge fade to prevent hard container cutoffs
                cloudDensity *= ContainerEdgeFade(position);
                
                return max(0, cloudDensity * _DensityMultiplier * 0.1);
            }

            // Calculate proportion of light that reaches the given point from the lightsource
            float lightmarch(float3 position, float shadowAtten)
            {
                // If in shadow, reduce light contribution significantly
                if (shadowAtten < 0.01)
                    return _DarknessThreshold * 0.5;
                    
                float3 dirToLight = _MainLightPosition.xyz;
                float dstInsideBox = rayBoxDst(_BoundsMin, _BoundsMax, position, _MainLightInvDir).y;

                float stepSize = dstInsideBox * NUM_STEPS_LIGHT_RCP;
                float totalDensity = 0;

                [unroll(NUM_STEPS_LIGHT)]
                for (int step = 0; step < NUM_STEPS_LIGHT; step++)
                {
                    position += dirToLight * stepSize;
                    totalDensity += max(0, sampleDensity(position) * stepSize);
                }

                float transmittance = exp(-totalDensity * _LightAbsorptionTowardSun);
                float result = _DarknessThreshold + transmittance * (1 - _DarknessThreshold);
                
                // Modulate by shadow
                return lerp(_DarknessThreshold * 0.5, result, shadowAtten);
            }
            
            float ComputeFogCoord(float HCSposZ, float3 posWS)
            {
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(HCSposZ);
                #endif
                float fogCoord = InitializeInputDataFog(float4(posWS, 1.0), fogFactor);
                return fogCoord;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = LOAD_FRAMEBUFFER_INPUT(0, input.positionCS.xy);
                
                float depth = GetCorrectDepth(input.texcoord);

                float3 rayEnd = ComputeWorldSpacePosition(
                    input.texcoord,
                    depth,
                    _Custom_I_VP
                );
                float3 rayStart = ComputeWorldSpacePosition(
                    input.texcoord,
                    UNITY_NEAR_CLIP_VALUE,
                    _Custom_I_VP
                );
                
                float3 rayDir = normalize(rayEnd - rayStart);

                float2 rayBoxInfo = rayBoxDst(_BoundsMin, _BoundsMax, rayStart, 1.0 / rayDir);
                float dstToBox = rayBoxInfo.x;
                float dstInsideBox = rayBoxInfo.y;

                float linearDepth = length(rayStart - rayEnd);
                
                // Check if camera is inside the fog box
                bool cameraInsideBox = all(rayStart >= _BoundsMin) && all(rayStart <= _BoundsMax);
                
                // When camera is inside, dstToBox will be 0, start raymarching from camera
                float entryDist = cameraInsideBox ? 0 : dstToBox;
                
                bool rayBoxHit = dstInsideBox > 0 && entryDist < linearDepth;
                if (!rayBoxHit)
                    return color;

                // Clamp the ray distance to scene geometry depth
                // When inside the box: march from camera to min(geometry, box exit)
                // When outside: march from box entry to min(geometry, box exit)
                float maxMarchDist = cameraInsideBox ? dstInsideBox : dstInsideBox;
                float dstLimit = min(linearDepth - entryDist, maxMarchDist);
                
                // Ensure we don't march past geometry
                dstLimit = max(0, dstLimit);

                float3 rayPos = rayStart + rayDir * entryDist;
                
                // Calculate step size with maximum limit to maintain quality in large volumes
                // Base step size from NumSteps
                float baseStepSize = dstLimit / max(1, _NumSteps);
                
                // Apply max step size limit if set (> 0)
                float stepSize = _MaxStepSize > 0.001 ? min(baseStepSize, _MaxStepSize) : baseStepSize;
                
                float3 rayStep = rayDir * stepSize;

                // Jitter ray start to break up banding and stabilize point light sampling
                // Use interleaved gradient noise - static per-pixel dither only
                float2 pixelCoord = input.positionCS.xy;
                float jitter = InterleavedGradientNoise(pixelCoord);
                float rayStartOffset = jitter * stepSize;
                rayPos += rayDir * rayStartOffset;
                float dstTravelled = rayStartOffset;
                
                // Phase function makes clouds brighter around sun
                float cosAngle = dot(rayDir, _MainLightPosition.xyz);
                float phaseVal = phase(cosAngle, _PhaseParams);

                float transmittance = 1;
                float3 lightEnergy = float3(0, 0, 0);

                [loop]
                while (dstTravelled < dstLimit)
                {
                    float density = sampleDensity(rayPos);
                    
                    // Always sample point lights along the ray, even in low density areas
                    // This prevents flickering when fog density changes around lights
                    float3 pointLightContrib = SamplePointLights(rayPos, rayDir);

                    // If point lights are bright here, take extra sub-samples between steps and average
                    if (_PointLightExtraSamples > 0)
                    {
                        float bright = max(max(pointLightContrib.r, pointLightContrib.g), pointLightContrib.b);
                        if (bright > _PointLightExtraThreshold)
                        {
                            float3 accum = pointLightContrib;
                            int extra = 50;// _PointLightExtraSamples;
                            // distribute samples evenly between current and next step
                            for (int s = 1; s <= extra; s++)
                            {
                                float t = (float(s) / float(extra + 1));
                                float3 subPos = rayPos + rayDir * (t * stepSize);
                                accum += SamplePointLights(subPos, rayDir);
                            }
                            pointLightContrib = accum / float(extra + 1);
                        }
                    }
                    
                    // Point lights contribute based on transmittance and a base scattering
                    // Use a minimum density for point light scattering to keep them stable
                    float pointLightDensity = max(density, 0.01);
                    lightEnergy += pointLightDensity * stepSize * transmittance * pointLightContrib;
                    
                    if (density > 0)
                    {
                        // Sample shadow at current ray position
                        float4 shadowCoord = TransformWorldToShadowCoord(rayPos);
                        float shadowAtten = MainLightRealtimeShadow(shadowCoord);
                        
                        // Main directional light contribution
                        float lightTransmittance = lightmarch(rayPos, shadowAtten);
                        float3 mainLightContrib = lightTransmittance * phaseVal * _MainLightColor.rgb;
                        
                        // Accumulate main light energy
                        lightEnergy += density * stepSize * transmittance * mainLightContrib;
                        
                        transmittance *= exp(-density * stepSize * _LightAbsorptionThroughCloud);

                        // Exit early if T is close to zero as further samples won't affect the result much
                        if (transmittance < 0.01)
                            break;
                    }

                    rayPos += rayStep;
                    dstTravelled += stepSize;
                }

                // Clean fog output - no dithering at 480x270
                // The temporal jitter + TAA handles smoothing naturally
                // Apply fog absorption/tint (color controls which wavelengths are scattered/absorbed)
                float3 fogColor = MixFog(lightEnergy * _FogColor, ComputeFogCoord(input.positionCS.z, rayPos));
                
                // Blend fog with scene based on transmittance
                return float4(lerp(fogColor, color.rgb, transmittance), 1);
            }
            ENDHLSL
        }
    }
}