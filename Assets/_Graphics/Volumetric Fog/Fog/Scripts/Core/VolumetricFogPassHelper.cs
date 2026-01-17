using UnityEngine;
using UnityEngine.Rendering;
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

            material.SetFloat(
                ShapeScale,
                GetValue(sceneVolumeComponent?.shapeScale, volumeComponent.shapeScale)
            );

            material.SetVector(
                ShapeOffset,
                GetValue(sceneVolumeComponent?.shapeOffset, volumeComponent.shapeOffset)
            );

            material.SetFloat(
                DetailScale,
                GetValue(sceneVolumeComponent?.detailScale, volumeComponent.detailScale)
            );

            material.SetVector(
                DetailOffset,
                GetValue(sceneVolumeComponent?.detailOffset, volumeComponent.detailOffset)
            );
            // --------

            // Clouds
            // ------
            material.SetVector(
                ShapeWeights,
                GetValue(sceneVolumeComponent?.shapeWeights, volumeComponent.shapeWeights)
            );

            material.SetVector(
                DetailWeights,
                GetValue(sceneVolumeComponent?.detailWeights, volumeComponent.detailWeights)
            );

            material.SetFloat(
                DensityOffset,
                GetValue(sceneVolumeComponent?.densityOffset, volumeComponent.densityOffset)
            );

            material.SetFloat(
                DensityMultiplier,
                GetValue(sceneVolumeComponent?.densityMultiplier, volumeComponent.densityMultiplier)
            );

            material.SetFloat(
                DetailMultiplier,
                GetValue(sceneVolumeComponent?.detailMultiplier, volumeComponent.detailMultiplier)
            );

            material.SetInt(NumSteps, numSteps);
            material.SetInt(NumStepsLight, numStepsLight);
            // ------

            // Lighting
            // --------
            var phaseParams = new Vector4(
                GetValue(sceneVolumeComponent?.forwardScattering, volumeComponent.forwardScattering),
                GetValue(sceneVolumeComponent?.backScattering, volumeComponent.backScattering),
                GetValue(sceneVolumeComponent?.baseBrightness, volumeComponent.baseBrightness),
                GetValue(sceneVolumeComponent?.phaseFactor, volumeComponent.phaseFactor)
            );

            material.SetVector(PhaseParams, phaseParams);

            material.SetFloat(
                LightAbsorptionThroughCloud,
                GetValue(sceneVolumeComponent?.lightAbsorptionThroughCloud, volumeComponent.lightAbsorptionThroughCloud)
            );

            material.SetFloat(
                LightAbsorptionTowardSun,
                GetValue(sceneVolumeComponent?.lightAbsorptionTowardSun, volumeComponent.lightAbsorptionTowardSun)
            );

            material.SetFloat(
                DarknessThreshold,
                GetValue(sceneVolumeComponent?.darknessThreshold, volumeComponent.darknessThreshold)
            );
            // --------

            // Wind
            // ----
            material.SetFloat(
                ShapeSpeed,
                GetValue(sceneVolumeComponent?.shapeSpeed, volumeComponent.shapeSpeed)
            );

            material.SetFloat(
                DetailSpeed,
                GetValue(sceneVolumeComponent?.detailSpeed, volumeComponent.detailSpeed)
            );

            material.SetVector(
                WindDir,
                GetValue(sceneVolumeComponent?.windDirection, volumeComponent.windDirection)
            );
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
            material.SetFloat(
                EnablePointLights,
                GetValue(sceneVolumeComponent?.enablePointLights, volumeComponent.enablePointLights) ? 1f : 0f
            );

            material.SetInt(
                MaxPointLights,
                GetValue(sceneVolumeComponent?.maxPointLights, volumeComponent.maxPointLights)
            );

            material.SetInt(
                PointLightExtraSamples,
                GetValue(sceneVolumeComponent?.pointLightExtraSamples, volumeComponent.pointLightExtraSamples)
            );

            material.SetFloat(
                PointLightExtraThreshold,
                GetValue(sceneVolumeComponent?.pointLightExtraThreshold, volumeComponent.pointLightExtraThreshold)
            );
            
            // Fog appearance
            material.SetVector(
                FogColor,
                GetValue(sceneVolumeComponent?.fogColor, volumeComponent.fogColor)
            );
            // ------------

            // Quality
            // -------
            material.SetFloat(
                MaxStepSize,
                GetValue(sceneVolumeComponent?.maxStepSize, volumeComponent.maxStepSize)
            );
            // -------

            // Edge Fade
            // ---------
            material.SetFloat(
                ContainerEdgeFadeDst,
                GetValue(sceneVolumeComponent?.edgeFadeDistance, volumeComponent.edgeFadeDistance)
            );

            material.SetFloat(
                TopFadeStrength,
                GetValue(sceneVolumeComponent?.topFadeStrength, volumeComponent.topFadeStrength)
            );

            material.SetFloat(
                VerticalFadeMultiplier,
                GetValue(sceneVolumeComponent?.verticalFadeMultiplier, volumeComponent.verticalFadeMultiplier)
            );
            // ---------
        }

        private static T GetValue<T>(
            VolumeParameter<T> sceneParam,
            VolumeParameter<T> defaultParam
        ) =>
            sceneParam != null && sceneParam.overrideState
                ? sceneParam.value
                : defaultParam.value;
    }
}