Shader "Hidden/Ledsna/SSRResolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SSRResolve"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // TEXTURE2D(_BlitTexture); // Already defined in Blit.hlsl
            // SAMPLER(sampler_BlitTexture); // Already defined in Blit.hlsl
            TEXTURE2D(_SSRNormalsTexture); 
            SAMPLER(sampler_SSRNormalsTexture);
            TEXTURE2D(_SceneDepthTexture);
            SAMPLER(sampler_SceneDepthTexture);

            float _SSRBlurStrength;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;
                
                // Read Roughness for Variable Blur Radius
                float4 normalData = SAMPLE_TEXTURE2D(_SSRNormalsTexture, sampler_SSRNormalsTexture, uv);
                
                // Mask: Ignore pixels where Smoothness (Alpha) > 1.0
                if(normalData.a > 1.0) return 0;
                
                float smoothness = normalData.a;
                float roughness = 1.0 - smoothness;
                
                // --- Simple Spatial Blur ---
                // We blur the final texture (Color + Alpha) to soften noise and silhouettes.
                // Radius scales with roughness so mirrors have sharp edges.
                
                float4 totalColor = 0;
                float totalWeight = 0.0;
                
                // Radius: 0.0px (Sharp) to Max (Rough)
                // Scales with proper artistic control
                float radius = roughness * _SSRBlurStrength; 
                
                // Center data for bilateral rejection
                float3 centerNormal = normalData.xyz;
                float centerDepth = SAMPLE_TEXTURE2D_LOD(_SceneDepthTexture, sampler_SceneDepthTexture, uv, 0).r;
                centerDepth = LinearEyeDepth(centerDepth, _ZBufferParams);

                float2 offsets[9] = {
                    float2(0,0),
                    float2(1,1), float2(-1,-1), float2(1,-1), float2(-1,1),
                    float2(0,1), float2(0,-1), float2(1,0), float2(-1,0)
                };
                
                // simple weights
                 float weights[9] = {
                    1.0, 
                    0.5, 0.5, 0.5, 0.5,
                    0.75, 0.75, 0.75, 0.75
                };
                
                for(int i=0; i<9; i++)
                {
                    float2 off = offsets[i] * radius * texelSize;
                    float2 tapUV = uv + off;
                    
                    half4 s = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, tapUV, 0);
                    
                    // Bilateral Check (Preserve Edges)
                    // 1. Normal
                    float4 tapNormalData = SAMPLE_TEXTURE2D_LOD(_SSRNormalsTexture, sampler_SSRNormalsTexture, tapUV, 0);
                    float wNormal = (dot(centerNormal, tapNormalData.xyz) > 0.9) ? 1.0 : 0.0;
                    
                    // 2. Depth
                    float tapDepth = SAMPLE_TEXTURE2D_LOD(_SceneDepthTexture, sampler_SceneDepthTexture, tapUV, 0).r;
                    tapDepth = LinearEyeDepth(tapDepth, _ZBufferParams);
                    float wDepth = (abs(tapDepth - centerDepth) < 0.5) ? 1.0 : 0.0; 
                    
                    float w = weights[i] * wNormal * wDepth;
                    
                    totalColor += s * w;
                    totalWeight += w;
                }
                
                return (totalWeight > 0.001) ? totalColor / totalWeight : SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            }
            ENDHLSL
        }
    }
}
