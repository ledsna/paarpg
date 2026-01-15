Shader "Ledsna/BoxFilter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // Declare the variable here

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f data) : SV_Target
            {
                float2 boxSize = clamp(fwidth(data.uv) * _MainTex_TexelSize.zw, 1e-5, 1);
                float2 tx = data.uv * _MainTex_TexelSize.zw - 0.5 * boxSize;
                float2 txOffset = smoothstep(1 - boxSize, 1, frac(tx));
                float2 uv = (floor(tx) + 0.5 + txOffset) * _MainTex_TexelSize.xy;
                
                return tex2Dgrad(_MainTex, uv, ddx(data.uv), ddy(data.uv));
            }
            ENDCG
        }
    }
}