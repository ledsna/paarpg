Shader "Ledsna/Dither"
{
    Properties
    {
        _Colour("Colour", Color) = (0,0,0,1)
        _Density("Density", Float) = 1
    }
    SubShader
    {
        Tags {
            "Queue" = "AlphaTest"
            "RenderType"="Opaque"
        }
        LOD 100
        
        Blend SrcAlpha OneMinusDstColor

        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0
            
            #pragma vertex vert
            #pragma fragment frag

            // #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Density;
            float3 _Colour;

            float2 _PixelResolution;

            SamplerState point_clamp_sampler;
            Texture2D _NormalsTexture;

            float2 ComputeDitherUVs(float3 positionWS)
            {
                if (unity_OrthoParams.w)
                    return TransformWorldToHClipDir(positionWS).xy * 0.5 + 0.5;
                
                float4 hclipPosition = TransformWorldToHClip(positionWS);
                return hclipPosition.xy / hclipPosition.w * 0.5 + 0.5;
            }

            float Dither(float In, float2 ScreenPosition)
            {
                float2 pixelPos = ScreenPosition * _PixelResolution;
                
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

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            v2f vert (appdata input)
            {
                v2f output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag (v2f input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                // float3 colour = SampleSceneColor(uv);

                // float4 normals = _NormalsTexture.Sample(point_clamp_sampler, uv);
                // float depth = _CameraDepthTexture.Sample(point_clamp_sampler, uv);
                //
                // float clear_depth = normals.z;
                // return 1;
                // if (depth > clear_depth)
                //     clip(-1);

                // float2 screenUV = ComputeDitherUVs(input.positionWS);
                float dither = Dither(_Density, uv);
                clip(dither);

                return half4(_Colour * dither, 1);
                // clip(dither);
                // half x = max(max(colour.r, colour.b), colour.g);
                // return x;
                // return half4(x * 0.5, 1);
                // return half4(1 - colour, 1);
                // return dither * 10;
                // return half4(HSVtoRGB(float3(RGBtoHSV(1-colour).x, 1, 1)) + 0.5, 1);
            }
            ENDHLSL
        }
    }
}
