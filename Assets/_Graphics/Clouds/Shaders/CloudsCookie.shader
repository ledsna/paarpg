Shader "Ledsna/CloudShadows"
{
    Properties
    {
        [HideInInspector]
        _Noise("Clouds Noise", 2D) = "white" {}
        [HideInInspector]
        _Details("Details Noise", 2D) = "white" {}
        [HideInInspector]
        _CookieSteps("Cookie Steps", Float) = -1
        [HideInInspector]
        _NoiseSpeed("Noise speed", Vector) = (0., 0., 0., 0.)
        [HideInInspector]
        _DetailsSpeed("Details speed", Vector) = (0., 0., 0., 0.)
     }

     SubShader
     {
        Blend One Zero

        Pass
        {
            Name "Cookie"

            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            sampler2D   _Noise;
            sampler2D   _Details;

            half       _CookieSteps;

            half2       _NoiseSpeed;
            half2       _DetailsSpeed;
            
            float dither(float4 In, float2 uv)
            {
                float DITHER_THRESHOLDS[16] =
                {
                    1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                    13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                    4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                    16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
                };
                uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
                return In - DITHER_THRESHOLDS[index];
            }

            #ifndef QUANTIZE_INCLUDED
            #define QUANTIZE_INCLUDED
            float Quantize(float steps, float shade)
            {
                if (steps == -1) return shade;
                if (steps == 0) return 0;
                if (steps == 1) return 1;

                return floor(shade * (steps - 1) + 0.5) / (steps - 1);
            }
            #endif
            
            float4 frag(v2f_customrendertexture IN) : SV_Target
            {
                half2 noiseOffset = _Time.yy * _NoiseSpeed / 60;
                half2 detailsOffset = _Time.yy * _DetailsSpeed / 60;

                half CookieSample = tex2D(_Noise, IN.globalTexcoord.xy + noiseOffset).r * tex2D(_Details, IN.globalTexcoord.xy + detailsOffset).r + 0.1;
                // half color = smoothstep(0, 1, CookieSample * 2.2);
                half color = CookieSample;
                
                color = smoothstep(0, 1, saturate(color * 5.2));

                return color;
            }
            ENDCG
        }
    }
}
