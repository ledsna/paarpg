using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    [VolumeRequiresRendererFeatures(typeof(VolumetricFogFeature))]
    [Serializable, VolumeComponentMenu("Custom/Volumetric Fog")]
    [CreateAssetMenu(fileName = "VolumetricFogSettings", menuName = "Volumetric Fog/Volumetric Fog Settings")]
    public class VolumetricFogVolumeComponent : VolumeComponent
    {
        // --- Shape ---
        [BoxGroup("Shape")] public MinFloatParameter shapeScale = new(10f, 0f);
        [HideInInspector]
        public Vector3Parameter shapeOffset = new(Vector3.zero);

        // --- Detail ---
        [BoxGroup("Detail")] public MinFloatParameter detailScale = new(10f, 0f);
        [HideInInspector]
        public Vector3Parameter detailOffset = new(Vector3.zero);

        // --- Density / marching ---
        [Header("Density / marching")]
        [Tooltip("Signed density bias added before multipliers. Use it like threshold")]
        public FloatParameter densityOffset = new(-3.36f);

        [Tooltip("Overall density multiplier.")]
        public MinFloatParameter densityMultiplier = new(15.221f, 0f);

        [Tooltip("Multiplier applied to detail noise contribution.")]
        public MinFloatParameter detailMultiplier = new(4.16f, 0f);

        [Tooltip("Weights for fractal/octaves of the shape noise.")]
        [BoxGroup("Shape")] public Vector4Parameter shapeWeights = new(new Vector4(1f, 0.48f, 0.15f, 0f));

        [Tooltip("Weights for fractal/octaves of the detail noise.")]
        [BoxGroup("Detail")] public Vector4Parameter detailWeights = new(new Vector4(1f, 0.5f, 0.25f, 0.1f));

        // --- Lighting ---
        [Header("Phase Parameters")]
        [Tooltip("Forward scattering term [0,1].")]
        [Foldout("Lighting")] public ClampedFloatParameter forwardScattering = new(0.827f, 0f, 1f);

        [Tooltip("Back scattering term [0,1].")]
        [Foldout("Lighting")] public ClampedFloatParameter backScattering = new(0.007f, 0f, 1f);

        [Tooltip("Base brightness term [0,1].")]
        [Foldout("Lighting")] public ClampedFloatParameter baseBrightness = new(0.657f, 0f, 1f);

        [Tooltip("Phase factor [0,1] used by your phase function.")]
        [Foldout("Lighting")] public ClampedFloatParameter phaseFactor = new(0.506f, 0f, 1f);

        [Header("Absorption and Threshold")]
        [Tooltip("Light absorption through cloud body.")]
        [Foldout("Lighting")] public FloatParameter lightAbsorptionThroughCloud = new(1f);

        [Tooltip("Light absorption toward sun direction.")]
        [Foldout("Lighting")] public FloatParameter lightAbsorptionTowardSun = new(1f);

        [Tooltip("Darkness threshold [0,1].")]
        [Foldout("Lighting")] public ClampedFloatParameter darknessThreshold = new(0.253f, 0f, 1f);

        // --- Wind ---
        [Tooltip("Scroll speed for shape noise (units/sec).")]
        [Foldout("Wind")] public MinFloatParameter shapeSpeed = new(0f, 0f);

        [Tooltip("Scroll speed for detail noise (units/sec).")]
        [Foldout("Wind")] public MinFloatParameter detailSpeed = new(0f, 0f);

        [Tooltip("Normalized wind direction (x,y,z).")]
        [Foldout("Wind")] public Vector3Parameter windDirection = new(new Vector3(1f, 0f, 0f));

        // --- Height Fog (Gravity Settling) ---
        [Header("Height Fog")]
        // Height Fog removed: behavior simplified to use 3D noise only

        // --- Point Lights (Simplified) ---
        [Header("Point Lights")]
        [Tooltip("Enable point light contribution to volumetric fog.")]
        [Foldout("Point Lights")] public BoolParameter enablePointLights = new(false);

        [Tooltip("Maximum number of point lights to sample (2-4 recommended).")]
        [Foldout("Point Lights")] public ClampedIntParameter maxPointLights = new(2, 0, 8);

        [Tooltip("Extra sub-samples to take between ray steps when near point lights (0 = off).")]
        [Foldout("Point Lights")] public ClampedIntParameter pointLightExtraSamples = new(0, 0, 3);

        [Tooltip("Brightness threshold to trigger extra point light samples (small values like 0.05).")]
        [Foldout("Point Lights")] public MinFloatParameter pointLightExtraThreshold = new(0.05f, 0f);

        // --- Quality ---
        [Header("Quality")]
        [Tooltip("Enable temporal jitter for smoother results with TAA.")]
        [Foldout("Quality")] public MinFloatParameter maxStepSize = new(2f, 0f);

        // --- Edge Fade ---
        [Header("Edge Fade")]
        [Tooltip("Distance from container edges where fog fades out (world units).")]
        [Foldout("Edge Fade")] public MinFloatParameter edgeFadeDistance = new(10f, 0f);

        [Tooltip("Top edge fade strength. 0 = same as sides, 1 = fade completely.")]
        [Foldout("Edge Fade")] public ClampedFloatParameter topFadeStrength = new(1f, 0f, 1f);

        [Tooltip("Top fade distance multiplier.")]
        [Foldout("Edge Fade")] public MinFloatParameter verticalFadeMultiplier = new(2f, 0.1f);

        // --- Appearance ---
        [Header("Appearance")]
        [Tooltip("Color used to tint/absorb scattered light in the fog (not emission).")]
        [Foldout("Appearance")] public ColorParameter fogColor = new(new Color(0.7f, 0.7f, 0.7f));
    }
}