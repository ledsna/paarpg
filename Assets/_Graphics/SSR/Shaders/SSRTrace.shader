Shader "Hidden/Ledsna/SSRTrace"
{
    Properties
    {
        _SSRThickness("Object Thickness", Float) = 1.5
        _SSRStep("Step Size", Float) = 0.2
        _SSRStr("Strength", Float) = 1.0
        _SSRMaxSteps("Max Steps", Float) = 50.0
        _SSREdgeFade("Edge Fade", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SSRTrace"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Inputs from SSR Prepass
            TEXTURE2D(_SSRDepthTexture);
            SAMPLER(sampler_SSRDepthTexture);
            
            // Input from Scene (Camera Depth)
            TEXTURE2D(_SceneDepthTexture);
            SAMPLER(sampler_SceneDepthTexture);
            
            // Raw SFloat Normals
            TEXTURE2D(_SSRNormalsTexture);
            SAMPLER(sampler_SSRNormalsTexture);
            
            float _SSRThickness;
            float _SSRStep;
            float _SSRStepGrowth;
            float _SSRMaxSteps;
            float _SSREdgeFade;
            float _SSRDistanceFade;
            float _SSRIntensity;

            // Helper to reconstruct View Space position from Water Surface Depth
            float3 GetSurfacePositionVS(float2 uv)
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_SSRDepthTexture, sampler_SSRDepthTexture, uv);
                float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return TransformWorldToView(posWS);
            }

            // Helper to reconstruct View Space position from Background Scene Depth
            float3 GetScenePositionVS(float2 uv)
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_SceneDepthTexture, sampler_SceneDepthTexture, uv);
                float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return TransformWorldToView(posWS);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // Read RAW Normal (-1..1)
                float4 normalSample = SAMPLE_TEXTURE2D(_SSRNormalsTexture, sampler_SSRNormalsTexture, uv);
                float3 normalWS = normalSample.xyz; 
                
                // Mask: Ignore pixels where Smoothness (Alpha) > 1.0
                if(normalSample.a > 1.0) return 0;

                // Extract Roughness
                float smoothness = normalSample.a;
                float roughness = 1.0 - smoothness;
                
                // Optimization: Skip background (zero length normal)
                if (dot(normalWS, normalWS) < 0.1) return 0;
                normalWS = normalize(normalWS);

                // Start ray at water surface (Use Surface Depth)
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_SSRDepthTexture, sampler_SSRDepthTexture, uv);
                float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                
                float3 viewDirWS = normalize(GetCameraPositionWS() - posWS);

                // --- SSR Logic ---
                float3 reflectionVector = reflect(-viewDirWS, normalWS);
                
                float3 viewDirVS = normalize(mul((float3x3)UNITY_MATRIX_V, reflectionVector));
                float3 startPosVS = TransformWorldToView(posWS);
                
                // Ray Params
                float stepSize = _SSRStep;
                float growth = _SSRStepGrowth; 
                float maxSteps = _SSRMaxSteps;
                float currentThickness = _SSRThickness;
                
                // Define Ray Start
                float3 rayPosVS = startPosVS;

                // [Trick 1] Dithering / Jitter
                // Interleaved Gradient Noise (Jimenez 2014)
                float2 pixelPos = uv * _ScreenParams.xy;
                float dither = frac(52.9829189 * frac(dot(pixelPos, float2(0.06711056, 0.00583715))));
                
                // Offset start position along the ray
                rayPosVS += viewDirVS * stepSize * dither; 

                // [Modification] Smooth Sampling: Removed Ray Direction Jitter.
                // We rely on Cone Radius LOD sampling for roughness. 
                // This eliminates the "gritty noise" but requires accurate Mip Maps.
                
                float3 initialRayPosVS = rayPosVS; 
                
                half3 foundColor = 0;
                float alphaOut = 0; // Output Validity (0..1) instead of Distance
                float hitDistOut = -1.0; // Default to Miss (-1)
                
                [loop]
                for(int i = 0; i < maxSteps; i++)
                {
                    rayPosVS += viewDirVS * stepSize;
                    
                    // Progressive growth: Starts linear, ramps up to exponential
                    // This keeps steps smaller near the start for better detail
                    float progress = (float)i / maxSteps;
                    float frameGrowth = lerp(1.0, growth, progress);
                    
                    stepSize *= frameGrowth;
                    currentThickness *= frameGrowth;

                    // [Cone Trace] Cone Logic
                    float rayDist = distance(rayPosVS, startPosVS);
                    float coneRadius = rayDist * roughness * 0.1; // k=0.1 (Tunable)
                    float checkThickness = currentThickness + coneRadius;
                    
                    // Project to Screen
                    float4 posCS = mul(UNITY_MATRIX_P, float4(rayPosVS, 1));
                    float4 screenPos = ComputeScreenPos(posCS);
                    float2 screenUV = screenPos.xy / screenPos.w;
                    
                    // Bounds
                    if(any(screenUV < 0) || any(screenUV > 1)) break;
                    
                    // Sample Depth at Ray Position (FROM SCENE)
                    float3 scenePosVS = GetScenePositionVS(screenUV);
                    
                    // Intersection Check
                    float depthDiff = scenePosVS.z - rayPosVS.z;
                    
                    // Check if Ray went BEHIND Scene (We refine FIRST, then check thickness)
                    // This fixes holes where the ray stepped too deep into the floor.
                    if (rayPosVS.z < scenePosVS.z)
                    {
                        // Save state in case we need to continue (occlusion)
                        float3 coarseRayPosVS = rayPosVS;
                        float currentStepSize = stepSize;
                        
                        // [Trick 2] Binary Refinement
                        // 1. Rewind to previous "safe" position
                        rayPosVS -= viewDirVS * stepSize; 
                        stepSize *= 0.5; 
                        
                        // 2. Binary Search (6 iterations)
                        [unroll]
                        for(int j = 0; j < 6; j++)
                        {
                            float3 checkPos = rayPosVS + viewDirVS * stepSize;
                            float4 cPosCS = mul(UNITY_MATRIX_P, float4(checkPos, 1));
                            float4 cScreenPos = ComputeScreenPos(cPosCS);
                            float2 cScreenUV = cScreenPos.xy / cScreenPos.w;
                            float3 cScenePosVS = GetScenePositionVS(cScreenUV);
                            
                            // If checkPos is IN FRONT of scene, we can advance
                            if (checkPos.z >= cScenePosVS.z) 
                                rayPosVS = checkPos; 
                                
                            stepSize *= 0.5;
                        }
                        
                        // 3. Final Check at refined position
                        float4 finalPosCS = mul(UNITY_MATRIX_P, float4(rayPosVS, 1));
                        float4 finalScreenPos = ComputeScreenPos(finalPosCS);
                        float2 finalScreenUV = finalScreenPos.xy / finalScreenPos.w;
                        float3 finalScenePosVS = GetScenePositionVS(finalScreenUV);
                        
                        // Re-calculate context at impact point
                        float refinedDist = distance(rayPosVS, startPosVS);
                        float refinedConeRadius = refinedDist * roughness * 0.1;
                        float refinedThickness = currentThickness + refinedConeRadius;
                        
                        // Check Thickness on the REFINED SURFACE
                        float refinedDiff = finalScenePosVS.z - rayPosVS.z;
                        
                        if (abs(refinedDiff) < refinedThickness)
                        {
                            // HIT CONFIRMED
                            float2 edgeDist = min(finalScreenUV, 1 - finalScreenUV);
                            float edgeFactor = saturate(min(edgeDist.x, edgeDist.y) / _SSREdgeFade);
                            
                            float pixelRadius = (refinedConeRadius * _ScreenParams.y) / abs(rayPosVS.z);
                            float lod = log2(max(1.0, pixelRadius));

                            // [Cone Trace] Stochastic Sampling (Bilateral 4 taps)    
                            float2 taps[4] = { float2(0,1), float2(1,0), float2(0,-1), float2(-1,0) };
                            float3 accumColor = 0;
                            float totalWeight = 0.0;

                            // Get Center Normal to reject invalid neighbors
                            float3 centerNormal = SAMPLE_TEXTURE2D_LOD(_SSRNormalsTexture, sampler_SSRNormalsTexture, finalScreenUV, 0).xyz;
                            
                            // Random rotation
                            float angle = dither * 6.28;
                            float c = cos(angle); float s = sin(angle);
                            float2x2 rot = float2x2(c, -s, s, c);
                            
                            [unroll]
                            for(int t=0; t<4; t++) {
                                float2 offset = mul(rot, taps[t]) * pixelRadius * _BlitTexture_TexelSize.xy * 0.5;
                                float2 tapUV = finalScreenUV + offset;

                                float3 tapNormal = SAMPLE_TEXTURE2D_LOD(_SSRNormalsTexture, sampler_SSRNormalsTexture, tapUV, 0).xyz;
                                float3 tapColor = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, tapUV, lod).rgb;

                                // 1. Normal Check (Bilateral)
                                // If normals diverge too much (> ~30 deg), it's a different surface orientation.
                                float wNormal = (dot(centerNormal, tapNormal) > 0.99) ? 1.0 : 0.0;

                                // 2. Depth Check (Bilateral)
                                // Prevent bleeding from foreground/background objects (e.g. a pole in front of a wall).
                                // We check if the tap's depth is similar to our Ray Hit depth.
                                float3 tapScenePosVS = GetScenePositionVS(tapUV);
                                float tapDepthDiff = abs(tapScenePosVS.z - rayPosVS.z);
                                // Threshold: 5% of View Distance or fixed small value? 
                                // Dynamic threshold helps with perspective foreshortening.
                                float wDepth = (tapDepthDiff < (0.001 * abs(rayPosVS.z))) ? 1.0 : 0.0;

                                float w = wNormal * wDepth;

                                accumColor += tapColor * w;
                                totalWeight += w;
                            }
                            
                            // Normalize or Fallback 
                            // If all taps rejected (e.g. at an edge), use SHARP center (LOD 0) to avoid bleeding.
                            // DO NOT use 'lod' here, as the mipmap itself contains the bleed we just tried to filter out.
                            foundColor = (totalWeight > 0.001) ? accumColor / totalWeight : SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, finalScreenUV, 0).rgb;
                            
                            // [Fix] Loop Clamp
                            foundColor = min(foundColor, half3(8.0, 8.0, 8.0)); 
                            
                            // 2. Physical Falloff
                            float physicsFactor = _SSRIntensity / (1.0 + refinedDist * _SSRDistanceFade); 
                            
                            // Apply Edge Fade to Color (Premul)
                            foundColor *= physicsFactor * edgeFactor;
                            
                            // Output Validity
                            alphaOut = physicsFactor * edgeFactor; 
                            break;
                        }
                        else
                        {
                            // MISS / OCCLUSION
                            // We hit something too thick (e.g. stepping behind a wall).
                            // Restore state and CONTINUE traversing.
                            rayPosVS = coarseRayPosVS;
                            stepSize = currentStepSize;
                        }
                    }
                }
                
                return half4(foundColor, alphaOut);
            }
            ENDHLSL
        }

        // Pass 1: Copy & Unpack Normals
        Pass
        {
            Name "CopyAndUnpackNormals"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUnpack

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragUnpack(Varyings input) : SV_Target
            {
                // Unpack URP Scene Normals which may be Oct-encoded
                float3 normal = SampleSceneNormals(input.texcoord);
                
                // [Fix] Attempt to read Smoothness from Source Alpha
                // Note: Standard URP _CameraNormalsTexture is RGB only. 
                // This assumes a custom DepthNormals pass that packs smoothness in Alpha.
                // If this is missing/white, reflections will be sharp 
                // (Smoothness 1 -> Roughness 0 -> No Blur).
                float4 raw = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, input.texcoord);
                
                return half4(normal, raw.a);
            }
            ENDHLSL
        }

        // Pass 2: Copy Depth (Downsample Support)
        Pass
        {
            Name "CopyDepth"
            ZTest Always 
            ZWrite On 
            ColorMask 0 
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float FragDepth(Varyings input) : SV_Depth
            {
                return SampleSceneDepth(input.texcoord);
            }
            ENDHLSL
        }

        // Pass 3: Gaussian Downsample (4-tap)
        Pass
        {
            Name "BoxDownsample"
            ZTest Always 
            ZWrite Off 
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Source is _BlitTexture (set by Blitter)
            float _SSRSourceMipLevel;

            half4 FragDownsample(Varyings input) : SV_Target
            {
                // Simple 4-tap box filter
                float2 texelSize = _BlitTexture_TexelSize.xy * pow(2.0, _SSRSourceMipLevel); // Scale texel size for source mip
                float2 uv = input.texcoord;
                
                half4 c = 0;
                // Force LOD sampling
                c += SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-0.5, -0.5) * texelSize, _SSRSourceMipLevel);
                c += SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( 0.5, -0.5) * texelSize, _SSRSourceMipLevel);
                c += SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-0.5,  0.5) * texelSize, _SSRSourceMipLevel);
                c += SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( 0.5,  0.5) * texelSize, _SSRSourceMipLevel);
                return c * 0.25;
            }
            ENDHLSL
        }
    }
}
