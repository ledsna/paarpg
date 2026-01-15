using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace FroxelLighting
{
    /// <summary>
    /// Render pass for Physically-Based Froxel Volumetric Fog
    /// 
    /// Two-pass approach:
    /// 1. InjectLighting: Compute in-scattering and extinction per froxel
    /// 2. Accumulate: Front-to-back ray marching with early exit
    /// </summary>
    public class FroxelVolumetricFogPass : ScriptableRenderPass
    {
        public FroxelVolumetricFogFeature.Settings settings;
        public ComputeShader computeShader;
        public Material compositeMaterial;

        private int kernelInject;
        private int kernelAccumulate;

        // Shader Property IDs
        private static readonly int _VolumeInject = Shader.PropertyToID("_VolumeInject");
        private static readonly int _VolumeAccumulated = Shader.PropertyToID("_VolumeAccumulated");
        private static readonly int _GridResolution = Shader.PropertyToID("_GridResolution");
        private static readonly int _NearPlane = Shader.PropertyToID("_NearPlane");
        private static readonly int _FarPlane = Shader.PropertyToID("_FarPlane");
        private static readonly int _DistributionPower = Shader.PropertyToID("_DistributionPower");
        private static readonly int _CameraPosition = Shader.PropertyToID("_CameraPosition");
        private static readonly int _InverseViewProj = Shader.PropertyToID("_InverseViewProj");
        private static readonly int _WorldScale = Shader.PropertyToID("_WorldScale");
        private static readonly int _GroundDensity = Shader.PropertyToID("_GroundDensity");
        private static readonly int _ScaleHeight = Shader.PropertyToID("_ScaleHeight");
        private static readonly int _AtmosphereHeight = Shader.PropertyToID("_AtmosphereHeight");
        private static readonly int _ScatteringCoeff = Shader.PropertyToID("_ScatteringCoeff");
        private static readonly int _ExtinctionCoeff = Shader.PropertyToID("_ExtinctionCoeff");
        private static readonly int _SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int _SunColor = Shader.PropertyToID("_SunColor");
        private static readonly int _SunIntensity = Shader.PropertyToID("_SunIntensity");
        private static readonly int _SkyColorZenith = Shader.PropertyToID("_SkyColorZenith");
        private static readonly int _SkyColorHorizon = Shader.PropertyToID("_SkyColorHorizon");
        private static readonly int _AmbientIntensity = Shader.PropertyToID("_AmbientIntensity");
        private static readonly int _MieAnisotropy = Shader.PropertyToID("_MieAnisotropy");
        private static readonly int _NoiseScale = Shader.PropertyToID("_NoiseScale");
        private static readonly int _NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        private static readonly int _NoiseHeightFade = Shader.PropertyToID("_NoiseHeightFade");
        private static readonly int _NoiseDistanceFade = Shader.PropertyToID("_NoiseDistanceFade");
        private static readonly int _WindVelocity = Shader.PropertyToID("_WindVelocity");
        private static readonly int _Time = Shader.PropertyToID("_Time");
        private static readonly int _DebugMode = Shader.PropertyToID("_DebugMode");
        private static readonly int _CamParams = Shader.PropertyToID("_CamParams");

        private class PassData
        {
            public ComputeShader computeShader;
            public Material compositeMaterial;
            public FroxelVolumetricFogFeature.Settings settings;
            public TextureHandle volumeInject;
            public TextureHandle volumeAccumulated;
            public TextureHandle source;
            public TextureHandle tempColor;
            public Camera camera;
            public int kernelInject;
            public int kernelAccumulate;
        }

        public FroxelVolumetricFogPass(FroxelVolumetricFogFeature.Settings settings, ComputeShader computeShader, Material compositeMaterial)
        {
            this.settings = settings;
            this.computeShader = computeShader;
            this.compositeMaterial = compositeMaterial;

            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);

            if (computeShader != null)
            {
                kernelInject = computeShader.FindKernel("InjectLighting");
                kernelAccumulate = computeShader.FindKernel("Accumulate");
            }
        }

        // --- Render Graph Implementation (Unity 6) ---

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (computeShader == null || compositeMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Create 3D textures
            var desc = new RenderTextureDescriptor(settings.gridResolution.x, settings.gridResolution.y, RenderTextureFormat.ARGBHalf, 0);
            desc.dimension = TextureDimension.Tex3D;
            desc.volumeDepth = settings.gridResolution.z;
            desc.enableRandomWrite = true;
            desc.msaaSamples = 1;
            desc.sRGB = false;

            TextureDesc volumeDesc = new TextureDesc(desc);
            volumeDesc.name = "_VolumeInject";
            TextureHandle volumeInject = renderGraph.CreateTexture(volumeDesc);

            volumeDesc.name = "_VolumeAccumulated";
            TextureHandle volumeAccumulated = renderGraph.CreateTexture(volumeDesc);

            // Temp color for ping-pong
            var colorDesc = cameraData.cameraTargetDescriptor;
            colorDesc.depthBufferBits = 0;
            colorDesc.msaaSamples = 1;

            TextureDesc tempColorDesc = new TextureDesc(colorDesc);
            tempColorDesc.name = "_TempFroxelColor";
            TextureHandle tempColor = renderGraph.CreateTexture(tempColorDesc);

            // Setup pass data
            using (var builder = renderGraph.AddUnsafePass<PassData>("Froxel Volumetric Fog", out var passData))
            {
                passData.computeShader = computeShader;
                passData.compositeMaterial = compositeMaterial;
                passData.settings = settings;
                passData.volumeInject = volumeInject;
                passData.volumeAccumulated = volumeAccumulated;
                passData.source = resourceData.activeColorTexture;
                passData.tempColor = tempColor;
                passData.camera = cameraData.camera;
                passData.kernelInject = kernelInject;
                passData.kernelAccumulate = kernelAccumulate;

                builder.UseTexture(volumeInject, AccessFlags.ReadWrite);
                builder.UseTexture(volumeAccumulated, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(tempColor, AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = ctx.cmd;
                    ExecutePassLogicUnsafe(data, cmd, data.camera);
                });
            }
        }

        private void ExecutePassLogicUnsafe(PassData data, UnsafeCommandBuffer cmd, Camera cam)
        {
            var s = data.settings;

            // Grid parameters
            cmd.SetComputeVectorParam(data.computeShader, _GridResolution,
                new Vector4(s.gridResolution.x, s.gridResolution.y, s.gridResolution.z, 0));
            cmd.SetComputeFloatParam(data.computeShader, _NearPlane, s.nearPlane);
            cmd.SetComputeFloatParam(data.computeShader, _FarPlane, s.farPlane);
            cmd.SetComputeFloatParam(data.computeShader, _DistributionPower, s.distributionPower);

            // Camera
            cmd.SetComputeVectorParam(data.computeShader, _CameraPosition, cam.transform.position);
            Matrix4x4 viewProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix;
            cmd.SetComputeMatrixParam(data.computeShader, _InverseViewProj, viewProj.inverse);
            cmd.SetComputeFloatParam(data.computeShader, _WorldScale, s.metersPerUnit);

            // Atmosphere
            cmd.SetComputeFloatParam(data.computeShader, _GroundDensity, s.groundDensity);
            cmd.SetComputeFloatParam(data.computeShader, _ScaleHeight, s.scaleHeight);
            cmd.SetComputeFloatParam(data.computeShader, _AtmosphereHeight, s.atmosphereHeight);
            cmd.SetComputeVectorParam(data.computeShader, _ScatteringCoeff, s.scatteringCoeff);
            cmd.SetComputeVectorParam(data.computeShader, _ExtinctionCoeff, s.extinctionCoeff);

            // Sun
            Light mainLight = RenderSettings.sun;
            if (mainLight != null)
            {
                cmd.SetComputeVectorParam(data.computeShader, _SunDirection, -mainLight.transform.forward);
                cmd.SetComputeVectorParam(data.computeShader, _SunColor,
                    new Vector4(mainLight.color.r, mainLight.color.g, mainLight.color.b, 1));
            }
            else
            {
                cmd.SetComputeVectorParam(data.computeShader, _SunDirection, Vector3.up);
                cmd.SetComputeVectorParam(data.computeShader, _SunColor, Vector4.one);
            }
            cmd.SetComputeFloatParam(data.computeShader, _SunIntensity, s.sunIntensity);

            // Sky ambient
            cmd.SetComputeVectorParam(data.computeShader, _SkyColorZenith,
                new Vector4(s.skyColorZenith.r, s.skyColorZenith.g, s.skyColorZenith.b, 1));
            cmd.SetComputeVectorParam(data.computeShader, _SkyColorHorizon,
                new Vector4(s.skyColorHorizon.r, s.skyColorHorizon.g, s.skyColorHorizon.b, 1));
            cmd.SetComputeFloatParam(data.computeShader, _AmbientIntensity, s.ambientIntensity);

            // Phase function
            cmd.SetComputeFloatParam(data.computeShader, _MieAnisotropy, s.mieAnisotropy);

            // Noise
            cmd.SetComputeFloatParam(data.computeShader, _NoiseScale, s.noiseScale);
            cmd.SetComputeFloatParam(data.computeShader, _NoiseStrength, s.noiseStrength);
            cmd.SetComputeFloatParam(data.computeShader, _NoiseHeightFade, s.noiseHeightFade);
            cmd.SetComputeFloatParam(data.computeShader, _NoiseDistanceFade, s.noiseDistanceFade);
            cmd.SetComputeVectorParam(data.computeShader, _WindVelocity, s.windVelocity);
            cmd.SetComputeFloatParam(data.computeShader, _Time, Time.time);

            // Debug
            cmd.SetComputeIntParam(data.computeShader, _DebugMode, (int)s.debugMode);

            // Dispatch InjectLighting
            cmd.SetComputeTextureParam(data.computeShader, data.kernelInject, _VolumeInject, data.volumeInject);
            int threadGroupsX = Mathf.CeilToInt(s.gridResolution.x / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(s.gridResolution.y / 8.0f);
            int threadGroupsZ = Mathf.CeilToInt(s.gridResolution.z / 8.0f);
            cmd.DispatchCompute(data.computeShader, data.kernelInject, threadGroupsX, threadGroupsY, threadGroupsZ);

            // Dispatch Accumulate
            cmd.SetComputeTextureParam(data.computeShader, data.kernelAccumulate, _VolumeInject, data.volumeInject);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelAccumulate, _VolumeAccumulated, data.volumeAccumulated);
            cmd.DispatchCompute(data.computeShader, data.kernelAccumulate, threadGroupsX, threadGroupsY, 1);

            // Composite
            cmd.SetGlobalTexture(_VolumeAccumulated, data.volumeAccumulated);
            cmd.SetGlobalVector(_GridResolution, new Vector4(s.gridResolution.x, s.gridResolution.y, s.gridResolution.z, 0));
            cmd.SetGlobalVector(_CamParams, new Vector4(s.nearPlane, s.farPlane, s.distributionPower, 0));

            if (data.source.IsValid() && data.tempColor.IsValid())
            {
                // Get native command buffer for Blitter
                CommandBuffer nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);

                // Ensure _BlitTexture is set (Blitter should do this, but be explicit)
                nativeCmd.SetGlobalTexture("_BlitTexture", data.source);

                Blitter.BlitCameraTexture(nativeCmd, data.source, data.tempColor, data.compositeMaterial, 0);
                Blitter.BlitCameraTexture(nativeCmd, data.tempColor, data.source);
            }
        }

        public void Dispose()
        {
        }
    }
}
