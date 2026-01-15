// reference
// https://valeriomarty.medium.com/raymarched-volumetric-lighting-in-unity-urp-e7bc84d31604

Shader "Ledsna/BilaterialBlur"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    int _GaussSamples;
    float _GaussAmount;
    float _BlurDepthFalloff;

    FRAMEBUFFER_INPUT_FLOAT(0);

    static const float gauss_filter_weights[] = {
        0.14446445, 0.13543542,
        0.11153505, 0.08055309,
        0.05087564, 0.02798160,
        0.01332457, 0.00545096
    };
    
    float GetCorrectDepth(float2 uv)
    {
        #if UNITY_REVERSED_Z
        real depth = SampleSceneDepth(uv);
        #else
        // Adjust z to match NDC for OpenGL
        real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
        #endif
        return depth;
    }

    float ReadTexture(float2 uv)
    {
        float epsilon = 0.5 / _ScreenParams.xy; // half a pixel
        float2 clampedUV = clamp(uv, float2(0.0, 0.0), float2(1.0 - epsilon, 1.0 - epsilon));
        float2 positionCS_xy = clampedUV * _ScreenParams.xy;
        float kernelSample = LOAD_FRAMEBUFFER_INPUT(0, positionCS_xy).x;
        return kernelSample;
    }

    // blurAxis must take follow values:
    // (1, 0) — blur by X axis
    // (0, 1) — blur by Y axis
    float BilaterialBlur(Varyings input, float2 blurAxis)
    {
        float accumResult = 0.0;
        float accumWeights = 0.0;
        float depthCenter = GetCorrectDepth(input.texcoord);

        for (int index = -_GaussSamples; index <= _GaussSamples; index++)
        {
            //we offset our uvs by a tiny amount 
            float2 uv = input.texcoord + index * _GaussAmount / 1000 * blurAxis;
            //sample the color at that location
            float kernelSample = ReadTexture(uv);
            //depth at the sampled pixel
            float depthKernel = GetCorrectDepth(uv);
            //weight calculation depending on distance and depth difference
            float depthDiff = abs(depthKernel - depthCenter);
            float r2 = depthDiff * _BlurDepthFalloff;
            float g = exp(-r2 * r2);
            float weight = g * gauss_filter_weights[abs(index)];
            //sum for every iteration of the color and weight of this sample 
            accumResult += weight * kernelSample;
            accumWeights += weight;
        }
        //final color
        return accumResult / accumWeights;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "DisableBatching"="True"
            "RenderPipeline" = "UniversalPipeline"
            "LightMode" = "ScriptableRenderPipeline"
        }
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Bilaterial Blur X"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragX
            
            float2 _LightDirSS;
            
            float fragX(Varyings input) : SV_Target
            {
                // return ReadTexture(input.texcoord);
                return BilaterialBlur(input, float2(1, 0));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Bilaterial Blur Y"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragY

            float2 _LightDirSS;
            
            float fragY(Varyings input) : SV_Target
            {
                // return ReadTexture(input.texcoord);
                return BilaterialBlur(input, float2(0, 1));
            }
            ENDHLSL
        }
    }
}