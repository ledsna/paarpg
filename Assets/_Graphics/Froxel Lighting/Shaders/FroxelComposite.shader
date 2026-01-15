Shader "Hidden/FroxelComposite"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FroxelComposite"
            
            HLSLPROGRAM
            #pragma vertex FroxelVert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct FroxelVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            // For legacy cmd.Blit path
            // TEXTURE2D(_MainTex);
            // SAMPLER(sampler_MainTex);
            
            TEXTURE3D(_VolumeAccumulated);
            SAMPLER(sampler_VolumeAccumulated);
            
            float4 _GridResolution; // x, y, z
            float4 _CamParams; // x: Near, y: Far, z: DistributionPower

            FroxelVaryings FroxelVert(Attributes input)
            {
                FroxelVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }
            
            // Power-Law Depth Distribution (Must match Compute Shader)
            // Converts linear depth to Z-slice normalized coordinate
            float DistanceToSlice(float distance, float near, float far, float power)
            {
                float t = (distance - near) / (far - near);
                return pow(saturate(t), 1.0 / power);
            }

            half4 Frag(FroxelVaryings input) : SV_Target
            {
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                
                float depth = SampleSceneDepth(input.uv);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
                
                // Handle case where _CamParams.z (power) might be 0 or invalid
                float power = max(_CamParams.z, 0.1);
                
                // Calculate Z coordinate in Froxel Grid using power-law distribution
                float zNorm = DistanceToSlice(linearDepth, _CamParams.x, _CamParams.y, power);
                zNorm = saturate(zNorm);
                
                // Sample Volumetric Texture
                float3 uvw = float3(input.uv, zNorm);
                half4 fogData = SAMPLE_TEXTURE3D(_VolumeAccumulated, sampler_VolumeAccumulated, uvw);

                half3 inscatteredLight = fogData.rgb;
                half transmittance = fogData.a;

                // Safety: Default to transparent if uninitialized or invalid
                // transmittance should be between 0 and 1, with 1 meaning fully transparent
                if (transmittance <= 0.0001)
                {
                    // If both are zero, texture wasn't written - show scene
                    if (length(inscatteredLight) <= 0.0001)
                    {
                        return sceneColor;
                    }
                    // Otherwise clamp transmittance to prevent pure black
                    transmittance = 0.0001;
                }
                
                // Composite: Final = Scene * Transmittance + Inscattered
                return half4(sceneColor.rgb * transmittance + inscatteredLight, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
