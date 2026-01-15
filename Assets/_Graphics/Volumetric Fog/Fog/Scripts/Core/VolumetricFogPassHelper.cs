using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    public partial class VolumetricFogPass
    {
        private static readonly int ShapeTex = Shader.PropertyToID("_ShapeTex");
        private static readonly int DetailTex = Shader.PropertyToID("_DetailTex");
        private static readonly int ShapeScale = Shader.PropertyToID("_ShapeScale");
        private static readonly int ShapeOffset = Shader.PropertyToID("_ShapeOffset");
        private static readonly int DetailScale = Shader.PropertyToID("_DetailScale");
        private static readonly int DetailOffset = Shader.PropertyToID("_DetailOffset");
        private static readonly int ShapeWeights = Shader.PropertyToID("_ShapeWeights");
        private static readonly int DetailWeights = Shader.PropertyToID("_DetailWeights");
        private static readonly int DensityOffset = Shader.PropertyToID("_DensityOffset");
        private static readonly int DensityMultiplier = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int DetailMultiplier = Shader.PropertyToID("_DetailMultiplier");
        private static readonly int NumSteps = Shader.PropertyToID("_NumSteps");
        private static readonly int NumStepsLight = Shader.PropertyToID("_NumStepsLight");
        private static readonly int PhaseParams = Shader.PropertyToID("_PhaseParams");
        private static readonly int LightAbsorptionThroughCloud = Shader.PropertyToID("_LightAbsorptionThroughCloud");
        private static readonly int LightAbsorptionTowardSun = Shader.PropertyToID("_LightAbsorptionTowardSun");
        private static readonly int FogColor = Shader.PropertyToID("_FogColor");
        private static readonly int DarknessThreshold = Shader.PropertyToID("_DarknessThreshold");
        private static readonly int ShapeSpeed = Shader.PropertyToID("_ShapeSpeed");
        private static readonly int DetailSpeed = Shader.PropertyToID("_DetailSpeed");
        private static readonly int WindDir = Shader.PropertyToID("_WindDir");

        // Height Fog
        // (Height fog parameters removed)

        // Point Lights
        private static readonly int EnablePointLights = Shader.PropertyToID("_EnablePointLights");
        private static readonly int MaxPointLights = Shader.PropertyToID("_MaxPointLights");
        private static readonly int PointLightExtraSamples = Shader.PropertyToID("_PointLightExtraSamples");
        private static readonly int PointLightExtraThreshold = Shader.PropertyToID("_PointLightExtraThreshold");

        // Quality
        // Temporal jitter removed — not used
        private static readonly int MaxStepSize = Shader.PropertyToID("_MaxStepSize");

        // Edge Fade
        private static readonly int ContainerEdgeFadeDst = Shader.PropertyToID("_ContainerEdgeFadeDst");
        private static readonly int TopFadeStrength = Shader.PropertyToID("_TopFadeStrength");
        private static readonly int VerticalFadeMultiplier = Shader.PropertyToID("_VerticalFadeMultiplier");

        private void UpdateSettings(UniversalLightData lightData)
        {
            // Textures
            // --------
            material.SetTexture(ShapeTex, shapeTexture);
            material.SetTexture(DetailTex, detailTexture);

            material.SetFloat(ShapeScale, volumeComponent.shapeScale.value);
            material.SetVector(ShapeOffset, volumeComponent.shapeOffset.value);

            material.SetFloat(DetailScale, volumeComponent.detailScale.value);
            material.SetVector(DetailOffset, volumeComponent.detailOffset.value);
            // --------

            // Clouds
            // ------
            material.SetVector(ShapeWeights, volumeComponent.shapeWeights.value);
            material.SetVector(DetailWeights, volumeComponent.detailWeights.value);
            material.SetFloat(DensityOffset, volumeComponent.densityOffset.value);
            material.SetFloat(DensityMultiplier, volumeComponent.densityMultiplier.value);
            material.SetFloat(DetailMultiplier, volumeComponent.detailMultiplier.value);
            material.SetInt(NumSteps, numSteps);
            material.SetInt(NumStepsLight, numStepsLight);
            // ------

            // Lighting
            // --------
            var phaseParams = new Vector4(
                volumeComponent.forwardScattering.value,
                volumeComponent.backScattering.value,
                volumeComponent.baseBrightness.value,
                volumeComponent.phaseFactor.value
            );

            material.SetVector(PhaseParams, phaseParams);
            material.SetFloat(LightAbsorptionThroughCloud, volumeComponent.lightAbsorptionThroughCloud.value);
            material.SetFloat(LightAbsorptionTowardSun, volumeComponent.lightAbsorptionTowardSun.value);
            material.SetFloat(DarknessThreshold, volumeComponent.darknessThreshold.value);
            // --------

            // Wind
            // ----
            material.SetFloat(ShapeSpeed, volumeComponent.shapeSpeed.value);
            material.SetFloat(DetailSpeed, volumeComponent.detailSpeed.value);
            material.SetVector(WindDir, volumeComponent.windDirection.value);
            // ----

            // Other 
            // -----
            // Calculating inverse direction of main light on CPU once for better performance on GPU side
            var dir = Vector3.forward;

            if (lightData.mainLightIndex >= 0)
            {
                // Visible lights are in lightData.visibleLights
                var vl = lightData.visibleLights[lightData.mainLightIndex];
                // For directional lights, forward (column 2) points from the light.
                dir = -vl.localToWorldMatrix.GetColumn(2);
                dir.Normalize();
            }
            else if (RenderSettings.sun) // fallback if no main light picked by URP
            {
                dir = -RenderSettings.sun.transform.forward;
            }

            var invDir = new Vector3(
                Mathf.Abs(dir.x) > 1e-6f ? 1f / dir.x : 0f,
                Mathf.Abs(dir.y) > 1e-6f ? 1f / dir.y : 0f,
                Mathf.Abs(dir.z) > 1e-6f ? 1f / dir.z : 0f
            );

            material.SetVector("_MainLightInvDir", invDir);
            // -----

            // Height Fog removed - nothing to set

            // Point Lights
            // ------------
            material.SetFloat(EnablePointLights, volumeComponent.enablePointLights.value ? 1f : 0f);
            material.SetInt(MaxPointLights, volumeComponent.maxPointLights.value);
            material.SetInt(PointLightExtraSamples, volumeComponent.pointLightExtraSamples.value);
            material.SetFloat(PointLightExtraThreshold, volumeComponent.pointLightExtraThreshold.value);
            // Fog appearance
            material.SetVector(FogColor, volumeComponent.fogColor.value);
            // ------------

            // Quality
            // -------
            material.SetFloat(MaxStepSize, volumeComponent.maxStepSize.value);
            // -------

            // Edge Fade
            // ---------
            material.SetFloat(ContainerEdgeFadeDst, volumeComponent.edgeFadeDistance.value);
            material.SetFloat(TopFadeStrength, volumeComponent.topFadeStrength.value);
            material.SetFloat(VerticalFadeMultiplier, volumeComponent.verticalFadeMultiplier.value);
            // ---------
        }
    }
}