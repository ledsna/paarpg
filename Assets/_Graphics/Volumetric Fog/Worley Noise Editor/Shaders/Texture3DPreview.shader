// https://docs.unity3d.com/6000.1/Documentation/Manual/class-CustomRenderTexture-write-shader.html
// https://docs.unity3d.com/6000.1/Documentation/Manual/class-Texture3D-use-in-shader.html
Shader "Hidden/Texture3DPreview"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture3D _VolumeTex;
            SamplerState sampler_VolumeTex;
            float _ZSlice;
            bool _GrayScale;
            bool4 _ChannelMask;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv: TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv: TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float GetFirstChannel(float4 val)
            {
                if (val.r > 0)
                    return val.r;
                if (val.g > 0)
                    return val.g;
                if (val.b > 0)
                    return val.b;
                return val.a;
            }

            float4 frag(Varyings IN) : COLOR
            {
                float3 uvw = float3(IN.uv, _ZSlice);
                float4 voxel = _VolumeTex.SampleLevel(sampler_VolumeTex, uvw, 0);
                float4 val = voxel * _ChannelMask;
                if (_GrayScale)
                    return GetFirstChannel(val);
                return val;
            }
            ENDHLSL
        }
    }
}