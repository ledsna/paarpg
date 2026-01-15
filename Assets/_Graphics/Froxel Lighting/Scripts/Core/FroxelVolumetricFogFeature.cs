using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FroxelLighting
{
    /// <summary>
    /// Physically-Based Froxel Volumetric Fog
    /// 
    /// Core Principle: The froxel grid represents OPTICAL IMPORTANCE, not uniform space.
    /// 
    /// Features:
    /// - Power-law Z distribution (dense near camera, sparse far away)
    /// - Camera-centered XZ, world Y (infinite world illusion)
    /// - Single unified atmosphere with exponential height falloff
    /// - Sun optical depth per froxel (no shadow maps - just medium thickness)
    /// - Noise as secondary modulation (not primary atmosphere)
    /// - Front-to-back accumulation with early exit
    /// - No TAA needed (stable by design)
    /// </summary>
    public class FroxelVolumetricFogFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Header("Grid Settings")]
            [Tooltip("Froxel grid resolution. X/Y are screen-aligned, Z is optical-depth mapped.")]
            public Vector3Int gridResolution = new Vector3Int(160, 90, 128);

            [Tooltip("Near plane in Unity units")]
            public float nearPlane = 0.3f;

            [Tooltip("Far plane / max distance in Unity units (e.g., 5000 for 5km)")]
            public float farPlane = 5000f;

            [Tooltip("Z-slice distribution power. Higher = more slices near camera. 2.0 is quadratic (good default).")]
            [Range(1.0f, 4.0f)]
            public float distributionPower = 2.0f;

            [Header("World Scale")]
            [Tooltip("How many meters is one Unity unit? 1.0 = 1 unit = 1 meter")]
            public float metersPerUnit = 1.0f;

            [Header("Atmosphere (Single Unified Medium)")]
            [Tooltip("Base density at ground level. Start with very small values (0.00001-0.0001).")]
            [Range(0.000001f, 0.001f)]
            public float groundDensity = 0.00005f;

            [Tooltip("Height in meters where density drops to 37% (1/e). 1200m for ground fog, 8000m for atmosphere.")]
            public float scaleHeight = 1200f;

            [Tooltip("Upper atmosphere cutoff in meters. Above this, density is zero.")]
            public float atmosphereHeight = 8000f;

            [Tooltip("Scattering coefficient (RGB). Controls fog color. Start low (0.1-0.5).")]
            public Vector3 scatteringCoeff = new Vector3(0.2f, 0.3f, 0.5f);

            [Tooltip("Extinction coefficient (RGB). Controls how quickly light is absorbed. Start low.")]
            public Vector3 extinctionCoeff = new Vector3(0.3f, 0.4f, 0.6f);
            [Header("Sun")]
            [Tooltip("Sun intensity multiplier")]
            [Range(0f, 10f)]
            public float sunIntensity = 1.5f;

            [Header("Sky Ambient")]
            [Tooltip("Sky color at zenith (looking straight up)")]
            public Color skyColorZenith = new Color(0.4f, 0.6f, 1.0f);

            [Tooltip("Sky color at horizon")]
            public Color skyColorHorizon = new Color(0.8f, 0.85f, 0.9f);

            [Tooltip("Ambient lighting intensity")]
            [Range(0f, 2f)]
            public float ambientIntensity = 0.3f;

            [Header("Phase Function")]
            [Tooltip("Mie anisotropy (g). 0 = isotropic, 0.8 = strong forward scattering. 0.76 is typical for fog.")]
            [Range(0f, 0.99f)]
            public float mieAnisotropy = 0.76f;

            [Header("Noise (Secondary Modulation)")]
            [Tooltip("Noise is detail ON TOP of atmosphere, not the atmosphere itself.")]
            public float noiseScale = 0.01f;

            [Tooltip("Noise strength. 0 = no noise, 1 = full modulation (+/- 100%)")]
            [Range(0f, 1f)]
            public float noiseStrength = 0.3f;

            [Tooltip("Height in meters where noise fades out")]
            public float noiseHeightFade = 500f;

            [Tooltip("Distance in meters where noise fades out (vanishes near horizon)")]
            public float noiseDistanceFade = 2000f;

            [Tooltip("Wind velocity for noise animation")]
            public Vector3 windVelocity = new Vector3(5f, 0f, 3f);

            [Header("Debug")]
            public DebugMode debugMode = DebugMode.None;

            public enum DebugMode
            {
                None = 0,
                Density = 1,
                OpticalDepth = 2,
                SunTransmittance = 3,
                Transmittance = 4  // Shows accumulated transmittance
            }
        }

        public Settings settings = new Settings();
        public ComputeShader computeShader;
        public Shader compositeShader;

        private FroxelVolumetricFogPass pass;
        private Material compositeMaterial;

        public override void Create()
        {
            // Lazy init in AddRenderPasses
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (compositeShader == null)
                compositeShader = Shader.Find("Hidden/FroxelComposite");

            if (computeShader == null || compositeShader == null)
            {
                Debug.LogWarning("FroxelVolumetricFogFeature: Missing shaders.");
                return;
            }

            if (pass == null || pass.computeShader != computeShader || pass.compositeMaterial == null)
            {
                pass?.Dispose();
                if (compositeMaterial == null)
                    compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
                pass = new FroxelVolumetricFogPass(settings, computeShader, compositeMaterial);
            }

            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(compositeMaterial);
        }
    }
}
