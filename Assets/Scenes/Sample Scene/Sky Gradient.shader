Shader "Custom/URP_SkyboxGradient"
{
    Properties
    {
        _BelowHorizonColor ("Below Horizon Color", Color) = (0.05,0.05,0.1,1)
        _FogColor ("Horizon Fog Color", Color) = (0.8,0.7,0.6,1)
        _CosmosColor ("Overhead Cosmos Color", Color) = (0.02,0.05,0.2,1)
        _StartAngle ("Gradient Start Angle (deg)", Range(0, 90)) = 10
        _Sharpness ("Gradient Sharpness", Range(0.1, 5.0)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS : TEXCOORD0;
            };

            float4 _BelowHorizonColor;
            float4 _FogColor;
            float4 _CosmosColor;
            float _StartAngle;
            float _Sharpness;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.viewDirWS = normalize(TransformObjectToWorld(IN.positionOS));
                return OUT;
            }

            float Bias(float t, float b)
            {
                return t / ((1.0 / b - 2.0) * (1.0 - t) + 1.0);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.viewDirWS);

                // Compute the Y threshold where the gradient starts
                float startY = sin(radians(_StartAngle)); // Y value above horizon where gradient begins

                // If below horizon, blend from below-horizon color up to fog color
                if (dir.y < 0.0)
                {
                    // Remap dir.y from [-1, 0] to [0, 1]
                    float tBelow = saturate(dir.y * -1.0);
                    float3 colorBelow = lerp(_FogColor.rgb, _BelowHorizonColor.rgb, tBelow);
                    return float4(_BelowHorizonColor.rgb, 1.0);
                }

                // Otherwise, above horizon: blend from fog to cosmos
                float t = saturate((dir.y - startY) / (1.0 - startY));
                float curvedT = Bias(t, _Sharpness);
                float3 colorAbove = lerp(_FogColor.rgb, _CosmosColor.rgb, curvedT);

                return float4(colorAbove, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "RenderFX/Skybox"
}
