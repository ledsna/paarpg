Shader "Ledsna/Water/Splash Particle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"  // Render after water
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;
            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color * _TintColor;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                half4 col = tex * i.color * _Brightness;

                // Apply fog
                col.rgb = MixFog(col.rgb, i.fogCoord);

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
