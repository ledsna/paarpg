using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    public partial class VolumetricFogPass : ScriptableRenderPass
    {
        private Material material;
        private Texture3D shapeTexture;
        private Texture3D detailTexture;
        private VolumetricFogVolumeComponent volumeComponent;
        private VolumetricFogVolumeComponent sceneVolumeComponent;

        private int numSteps;
        private int numStepsLight;

        public VolumetricFogPass(Texture3D shapeTexture, Texture3D detailTexture)
        {
            this.shapeTexture = shapeTexture;
            this.detailTexture = detailTexture;
        }

        class PassData
        {
            public Material material;
        }

        public void Setup(
            Material material, 
            int numSteps, 
            int numStepsLight, 
            VolumetricFogVolumeComponent volumeComponent,
            VolumetricFogVolumeComponent sceneVolumeComponent
        )
        {
            this.material = material;
            this.numSteps = numSteps;
            this.numStepsLight = numStepsLight;
            this.volumeComponent = volumeComponent;
            this.sceneVolumeComponent = sceneVolumeComponent;
        }

        private bool CanRecord() => material != null;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            
            var view = cam.worldToCameraMatrix;
            var proj = cam.projectionMatrix;

            Matrix4x4 projForReconstruction = proj;
            if (cam.cameraType == CameraType.Reflection)
            {
                projForReconstruction =
                    Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, cam.nearClipPlane, cam.farClipPlane);
            }

            Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(projForReconstruction, true);

            Matrix4x4 gpuVP = gpuProj * view;
            Matrix4x4 invGpuVP = gpuVP.inverse;

            material.SetMatrix("_Custom_I_VP", invGpuVP);

            if (!CanRecord())
            {
                Debug.LogWarning("Can't record volumetric clouds pass because material are null");
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            var srcCamColor = resourceData.activeColorTexture;

            if (!srcCamColor.IsValid())
                return;

            var lightData = frameData.Get<UniversalLightData>();

            UpdateSettings(lightData);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog", out var passData))
            {
                passData.material = material;

                var desc = srcCamColor.GetDescriptor(renderGraph);
                desc.name = "_VolumePassTexture";

                var outputTexture = renderGraph.CreateTexture(desc);

                builder.SetInputAttachment(srcCamColor, 0);
                builder.UseTexture(resourceData.cameraDepthTexture);
                builder.SetRenderAttachment(outputTexture, 0);

                builder.SetRenderFunc<PassData>(ExecutePass);

                resourceData.cameraColor = outputTexture;
            }
        }

        private static void ExecutePass(PassData data, RasterGraphContext context) =>
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, 0);
    }
}