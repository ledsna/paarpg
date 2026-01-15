// Volumetric God Rays - Redesigned for natural, volumetric feel
// Based on physically-inspired light scattering with artistic controls
Shader "Ledsna/GodRays"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "DisableBatching"="True"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "God Rays"

            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #define _MAIN_LIGHT_SHADOWS
            #define LOOP_COUNT 64
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Scattering;
            float _MaxDistance;
            float _JitterVolumetric;
            float _Density;
            float _HeightFogDensity;
            float _HeightFogFalloff;
            float _Contrast;

            // Interleaved gradient noise - better than white noise for temporal stability
            float InterleavedGradientNoise(float2 pixelCoord)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixelCoord, magic.xy)));
            }

            // Blue noise approximation using golden ratio
            float BlueNoise(float2 uv)
            {
                float noise = InterleavedGradientNoise(uv * _ScreenParams.xy);
                // Add temporal variation for TAA-like smoothing
                noise = frac(noise + frac(_Time.y * 0.61803398875));
                return noise;
            }

            // Henyey-Greenstein phase function with controllable anisotropy
            // g > 0: forward scattering (sun halo)
            // g < 0: back scattering
            // g = 0: isotropic
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
            }

            // Cornette-Shanks phase function - more physically accurate for atmospheric scattering
            float CornetteShanks(float cosTheta, float g)
            {
                float g2 = g * g;
                float num = 3.0 * (1.0 - g2) * (1.0 + cosTheta * cosTheta);
                float denom = 2.0 * (2.0 + g2) * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5);
                return num / max(denom, 0.0001);
            }

            // Height-based density falloff (exponential fog model)
            float GetHeightDensity(float3 worldPos)
            {
                // Density increases exponentially as height decreases
                float heightFactor = exp(-max(0, worldPos.y) * _HeightFogFalloff * 0.01);
                return lerp(1.0, heightFactor, _HeightFogDensity);
            }

            float GetCorrectDepth(float2 uv)
            {
                #if UNITY_REVERSED_Z
                    return SampleSceneDepth(uv);
                #else
                    return lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
            }
            
            float frag(Varyings input) : SV_Target
            {
                float depth = GetCorrectDepth(input.texcoord);
                float3 rayEnd = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                float3 rayStart = _WorldSpaceCameraPos;

                float3 rayVec = rayEnd - rayStart;
                float totalDistance = min(length(rayVec), _MaxDistance);
                float3 rayDir = rayVec / max(length(rayVec), 0.0001);
                
                float rayStep = totalDistance / LOOP_COUNT;
                float3 stepVec = rayDir * rayStep;

                // Pre-compute phase function (view-dependent, constant along ray)
                float cosTheta = dot(_MainLightPosition.xyz, rayDir);
                // Blend between forward scatter (looking toward sun) and mild backscatter
                float phase = HenyeyGreenstein(cosTheta, _Scattering * 0.8);
                // Add subtle backscatter for more natural look
                phase += HenyeyGreenstein(cosTheta, -0.2) * 0.15;
                phase = saturate(phase);

                // Temporal jitter using blue noise
                float jitter = BlueNoise(input.texcoord) * rayStep * _JitterVolumetric * 0.01;
                float3 rayPos = rayStart + rayDir * jitter;
                float dist = jitter;

                // Pre-compute shadow coord transformation
                float4 rayPosLS = TransformWorldToShadowCoord(rayPos);
                float4 rayEndLS = TransformWorldToShadowCoord(rayPos + stepVec * LOOP_COUNT);
                float4 stepLS = (rayEndLS - rayPosLS) / LOOP_COUNT;

                float transmittance = 1.0;
                float inscatter = 0.0;
                float rcpMaxDist = rcp(max(_MaxDistance, 0.001));

                [unroll(LOOP_COUNT)]
                for (int i = 0; i < LOOP_COUNT; i++)
                {
                    // Sample shadow and cookie
                    float cookie = step(0.9, SampleMainLightCookie(rayPos).r);
                    float shadow = MainLightRealtimeShadow(rayPosLS) * cookie;
                    
                    // Height-based density
                    float heightDensity = GetHeightDensity(rayPos);
                    
                    // Local density considering height fog
                    float localDensity = _Density * heightDensity;
                    
                    // Beer-Lambert extinction
                    float extinction = exp(-localDensity * rayStep * 0.1);
                    
                    // Inscattering contribution (light that scatters into the ray)
                    // Only accumulate where there's light (shadow == 1)
                    float scatterContrib = shadow * localDensity * transmittance;
                    
                    // Distance-based falloff for softer far rays
                    float distFalloff = exp(-dist * rcpMaxDist * 1.5);
                    
                    inscatter += scatterContrib * distFalloff * rayStep;
                    
                    // Update transmittance
                    transmittance *= extinction;

                    rayPos += stepVec;
                    rayPosLS += stepLS;
                    dist += rayStep;
                }
                
                // Apply phase function and normalize
                float radiance = inscatter * phase * 0.5;
                
                // Contrast curve: pow for shadow crushing, then soft knee
                // Higher _Contrast = more shadow crushing = punchier rays
                radiance = pow(max(radiance, 0.0), _Contrast);
                
                // Soft toe curve for smooth highlight rolloff
                float threshold = 0.15;
                radiance = radiance / (radiance + threshold) * (1.0 + threshold);
                
                return saturate(radiance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Compositing"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            float _Intensity;
            float3 _GodRayColor;

            FRAMEBUFFER_INPUT_X_FLOAT(0); // Color Texture
            FRAMEBUFFER_INPUT_X_FLOAT(1); // God Rays Texture

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = LOAD_FRAMEBUFFER_INPUT(0, input.positionCS.xy);
                float godRays = LOAD_FRAMEBUFFER_INPUT(1, input.positionCS.xy).x;
                
                // Soft light blend - more natural than screen or additive
                float3 rays = godRays * _Intensity * _GodRayColor;
                
                // Modified screen blend with intensity control
                // Preserves dark areas while allowing bright shafts
                float3 result = color.rgb + rays * (1.0 - color.rgb * 0.5);
                
                return float4(result, color.a);
            }
            ENDHLSL
        }
    }
}