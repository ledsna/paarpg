using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using __Project.Shared.Extensions;


namespace GodRays.Core
{
    public class GodRaysPass : ScriptableRenderPass
    {
        private GodRaysFeature.GodRaysSettings defaultGodRaysSettings;
        private Material godRaysMaterial;

        private GodRaysFeature.BlurSettings defaultBlurSettings;
        private Material blurMaterial;

        private TextureDesc godRaysTextureDescriptor;

        // God Rays Shader Properties
        // --------------------------
        private static readonly int intensityId = Shader.PropertyToID("_Intensity");
        private static readonly int scatteringId = Shader.PropertyToID("_Scattering");
        private static readonly int godRayColorId = Shader.PropertyToID("_GodRayColor");
        private static readonly int maxDistanceId = Shader.PropertyToID("_MaxDistance");
        private static readonly int jitterVolumetricId = Shader.PropertyToID("_JitterVolumetric");
        private static readonly int densityId = Shader.PropertyToID("_Density");
        private static readonly int heightFogDensityId = Shader.PropertyToID("_HeightFogDensity");
        private static readonly int heightFogFalloffId = Shader.PropertyToID("_HeightFogFalloff");
        private static readonly int contrastId = Shader.PropertyToID("_Contrast");

        private static string godRaysTextureName = "_GodRaysTexture";
        private static string godRaysPassName = "God Rays";
        private static string compositePassName = "Compositing";

        // Blur Shader Properties
        // ----------------------
        private static readonly int gaussSamplesId = Shader.PropertyToID("_GaussSamples");
        private static readonly int gaussAmountId = Shader.PropertyToID("_GaussAmount");
        private static readonly int blurDepthFalloffId = Shader.PropertyToID("_BlurDepthFalloff");
        private static string blurTextureName = "_BilaterialBlurTexture";
        private static string horizontalBlurPassName = "Horizontal Blur";
        private static string verticalBlurPassName = "Vertical Blur";
        private static readonly int LightDirSs = Shader.PropertyToID("_LightDirSS");

        public GodRaysPass(GodRaysFeature.GodRaysSettings defaultGodRaysSettings,
            GodRaysFeature.BlurSettings defaultBlurSettings)
        {
            this.defaultGodRaysSettings = defaultGodRaysSettings;
            this.defaultBlurSettings = defaultBlurSettings;
        }

        class GodRaysPassData
        {
            internal Material material;
        }

        class BlurPassData
        {
            internal Material material;
            internal int pass;
        }

        class CompositePassData
        {
            internal Material material;
        }

        private bool CanRecord() => blurMaterial != null || godRaysMaterial != null;

        public void Setup(Material godRaysMaterial, Material blurMaterial)
        {
            this.godRaysMaterial = godRaysMaterial;
            this.blurMaterial = blurMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!CanRecord())
            {
                // In the unity 6.0 there is bug with Create method of ScriptableRenderFeature: it's not called when it should
                // So is better to update to 6.1
                Debug.LogWarning("Can't record god rays pass because materials are null");
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            // The following line ensures that the render pass doesn't blit
            // from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var srcCamColor = resourceData.activeColorTexture;

            // This check is to avoid an error from the material preview in the scene
            if (!srcCamColor.IsValid())
                return;

            UpdateSettings(cameraData, lightData);

            ComputeGodRaysPass(renderGraph, resourceData, out var godRaysTexture);

            if (IsBlurEnabled())
            {
                var desc = srcCamColor.GetDescriptor(renderGraph);
                godRaysTextureDescriptor.width = desc.width;
                godRaysTextureDescriptor.height = desc.height;

                godRaysTextureDescriptor.name = blurTextureName;
                var horizontalBlurredTexture = renderGraph.CreateTexture(godRaysTextureDescriptor);
                BlurPass(renderGraph, resourceData.cameraDepthTexture, godRaysTexture, horizontalBlurredTexture, 0);
                // TODO: Unnecessary pass. Need change logic here later
                BlurPass(renderGraph, resourceData.cameraDepthTexture, horizontalBlurredTexture, godRaysTexture, 1);
            }

            CompositingPass(renderGraph, resourceData, godRaysTexture);
        }

        private void ComputeGodRaysPass(RenderGraph renderGraph, UniversalResourceData resourceData,
            out TextureHandle godRaysTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<GodRaysPassData>(godRaysPassName,
                       out var passData))
            {
                var srcCameraColorDesc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
                godRaysTextureDescriptor = new TextureDesc(
                    srcCameraColorDesc.width,
                    srcCameraColorDesc.height,
                    false, false
                );
                // TODO: Recheck this initialization. Maybe I need remove something
                godRaysTextureDescriptor.format = GraphicsFormat.R16_UNorm;
                godRaysTextureDescriptor.depthBufferBits = 0;
                godRaysTextureDescriptor.clearBuffer = false;
                godRaysTextureDescriptor.msaaSamples = MSAASamples.None;
                godRaysTextureDescriptor.name = godRaysTextureName;

                godRaysTexture = renderGraph.CreateTexture(godRaysTextureDescriptor);

                builder.SetRenderAttachment(godRaysTexture, 0);

                passData.material = godRaysMaterial;

                builder.UseTexture(resourceData.cameraDepthTexture);
                builder.UseTexture(resourceData.mainShadowsTexture);

                builder.SetRenderFunc<GodRaysPassData>(ExecuteGodRaysPass);
            }
        }

        private void BlurPass(RenderGraph renderGraph, TextureHandle depthTexture, TextureHandle source,
            TextureHandle destination, int pass)
        {
            var passName = pass == 0 ? horizontalBlurPassName : verticalBlurPassName;

            using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>(passName,
                       out var passData))
            {
                passData.material = blurMaterial;
                passData.pass = pass;

                builder.SetInputAttachment(source, 0);
                builder.UseTexture(depthTexture);
                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc<BlurPassData>(ExecuteBlurPass);
            }
        }

        private void CompositingPass(RenderGraph renderGraph, UniversalResourceData resourceData,
            TextureHandle godRaysTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(compositePassName,
                       out var passData))
            {
                passData.material = godRaysMaterial;

                var desc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
                desc.name = "_CompositeTexture";

                var compositeTexture = renderGraph.CreateTexture(desc);

                builder.SetInputAttachment(resourceData.activeColorTexture, 0);
                builder.SetInputAttachment(godRaysTexture, 1);

                builder.SetRenderAttachment(compositeTexture, 0);

                builder.SetRenderFunc<CompositePassData>(ExecuteCompositePass);

                resourceData.cameraColor = compositeTexture;
            }
        }

        private static void ExecuteGodRaysPass(GodRaysPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, 0);
        }

        private static void ExecuteCompositePass(CompositePassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, 1);
        }

        private static void ExecuteBlurPass(BlurPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, data.pass);
        }

        private void UpdateSettings(UniversalCameraData cameraData, UniversalLightData lightData)
        {
            if (godRaysMaterial == null) return;
            UpdateGodRaysSettings();

            if (blurMaterial == null) return;
            UpdateBlurSettings(cameraData, lightData);
        }

        private void UpdateGodRaysSettings()
        {
            var volumeComponent = VolumeManager.instance.stack.GetComponent<GodRaysVolumeComponent>();

            godRaysMaterial.SetFloat(intensityId,
                volumeComponent.Intensity.GetValueOrDefault(defaultGodRaysSettings.Intensity));

            godRaysMaterial.SetFloat(scatteringId,
                volumeComponent.Scattering.GetValueOrDefault(defaultGodRaysSettings.Scattering));

            godRaysMaterial.SetFloat(densityId,
                volumeComponent.Density.GetValueOrDefault(defaultGodRaysSettings.Density));

            godRaysMaterial.SetColor(godRayColorId,
                volumeComponent.GodRayColor.GetValueOrDefault(defaultGodRaysSettings.GodRayColor));

            godRaysMaterial.SetFloat(maxDistanceId,
                volumeComponent.MaxDistance.GetValueOrDefault(defaultGodRaysSettings.MaxDistance));

            godRaysMaterial.SetFloat(jitterVolumetricId,
                volumeComponent.JitterVolumetric.GetValueOrDefault(defaultGodRaysSettings.JitterVolumetric));

            godRaysMaterial.SetFloat(heightFogDensityId,
                volumeComponent.HeightFogDensity.GetValueOrDefault(defaultGodRaysSettings.HeightFogDensity));

            godRaysMaterial.SetFloat(heightFogFalloffId,
                volumeComponent.HeightFogFalloff.GetValueOrDefault(defaultGodRaysSettings.HeightFogFalloff));

            godRaysMaterial.SetFloat(contrastId,
                volumeComponent.Contrast.GetValueOrDefault(defaultGodRaysSettings.Contrast));
        }

        private void UpdateBlurSettings(UniversalCameraData cameraData, UniversalLightData lightData)
        {
            var volumeComponent = VolumeManager.instance.stack.GetComponent<GodRaysVolumeComponent>();

            blurMaterial.SetInt(gaussSamplesId,
                Mathf.Max(volumeComponent.GaussSamples.GetValueOrDefault(defaultBlurSettings.GaussSamples) - 1, 0));

            blurMaterial.SetFloat(gaussAmountId,
                volumeComponent.GaussAmount.GetValueOrDefault(defaultBlurSettings.GaussAmount));

            blurMaterial.SetFloat(blurDepthFalloffId,
                volumeComponent.BlurDepthFalloff.GetValueOrDefault(defaultBlurSettings.BlurDepthFalloff));

            // Calculating screen space light direction 
            // ----------------------------------------
            var cam = cameraData.camera;
            var mainLight = RenderSettings.sun;
            var dirVS = cam.worldToCameraMatrix.MultiplyVector(mainLight.transform.forward);

            var dirCS = cam.projectionMatrix.MultiplyVector(dirVS);
            var dirSS = new Vector2(dirCS.x, dirCS.y).normalized;

            blurMaterial.SetVector(LightDirSs, dirSS);
        }

        private bool IsBlurEnabled()
        {
            var volumeComponent = VolumeManager.instance.stack.GetComponent<GodRaysVolumeComponent>();
            if (volumeComponent.GaussSamples.overrideState)
                return volumeComponent.GaussSamples.value != 0;
            return defaultBlurSettings.GaussSamples != 0;
        }
    }
}