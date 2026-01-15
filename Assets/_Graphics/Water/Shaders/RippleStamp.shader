Shader "Hidden/RippleStamp"
{
    Properties
    {
        // Procedural shader, no texture needed
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Blend One One // Additive blending
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Age)
                UNITY_DEFINE_INSTANCED_PROP(float, _MaxAge)
                UNITY_DEFINE_INSTANCED_PROP(float, _Speed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Amplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _Frequency)
                UNITY_DEFINE_INSTANCED_PROP(float, _QuadRadius) // World radius of this quad
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float age = UNITY_ACCESS_INSTANCED_PROP(Props, _Age);
                float maxAge = UNITY_ACCESS_INSTANCED_PROP(Props, _MaxAge);
                float speed = UNITY_ACCESS_INSTANCED_PROP(Props, _Speed);
                float amp = UNITY_ACCESS_INSTANCED_PROP(Props, _Amplitude);
                float freq = UNITY_ACCESS_INSTANCED_PROP(Props, _Frequency);
                float quadRadius = UNITY_ACCESS_INSTANCED_PROP(Props, _QuadRadius);

                // UV 0..1 -> -1..1
                float2 centered = i.uv * 2.0 - 1.0;
                float distNorm = length(centered); // 0..1
                
                // Clip outside circle
                if (distNorm > 1.0) discard;
                
                float dist = distNorm * quadRadius; // World distance

                float rippleRadius = age * speed;
                float D = dist - rippleRadius;
                
                // Packet width
                float packetWidth = 0.5 + rippleRadius * 0.1;
                float packetWidthSq = packetWidth * packetWidth;
                
                float envelope = exp(-(D * D) / packetWidthSq);
                
                float phase = D * freq;
                float wave = sin(phase) * envelope;
                
                // Fades
                float innerFade = saturate(rippleRadius * 2.5);
                innerFade = innerFade * innerFade * (3.0 - 2.0 * innerFade);
                
                float outerFadeLinear = 1.0 - age / maxAge;
                float timeFade = outerFadeLinear * outerFadeLinear;
                
                float energyFalloff = 1.0 / (1.0 + rippleRadius * 0.8);

                float finalAmp = innerFade * timeFade * energyFalloff * amp;
                
                float height = wave * finalAmp;
                
                return float4(height, 0, 0, 1);
            }
            ENDCG
        }
    }
}
