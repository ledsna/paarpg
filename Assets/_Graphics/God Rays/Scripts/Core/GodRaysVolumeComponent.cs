using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GodRays.Core
{
    public enum SampleCountEnum
    {
        _8 = 8,
        _16 = 16,
        _32 = 32,
        _64 = 64,
        _86 = 86,
        _128 = 128,
    }

    [VolumeRequiresRendererFeatures(typeof(GodRaysFeature))]
    [Serializable, VolumeComponentMenu("Custom/God Rays")]
    public class GodRaysVolumeComponent : VolumeComponent
    {
        [Header("God Rays Settings")]
        [Tooltip("Overall intensity of the god rays effect")]
        public ClampedFloatParameter Intensity = new(0.5f, 0.0f, 4.0f);

        [Tooltip("Scattering anisotropy (0 = isotropic, positive = forward scatter toward sun)")]
        public ClampedFloatParameter Scattering = new(0.5f, 0.0f, 0.95f);

        [Tooltip("Volumetric density - higher values = more visible rays")]
        public ClampedFloatParameter Density = new(1.0f, 0.0f, 5.0f);

        [Tooltip("Maximum ray marching distance")]
        public MinFloatParameter MaxDistance = new(100.0f, 0.0f);

        [Tooltip("Jitter amount to reduce banding artifacts")]
        public ClampedFloatParameter JitterVolumetric = new(100.0f, 0.0f, 200.0f);

        [Tooltip("Color tint of the god rays")]
        public ColorParameter GodRayColor = new(Color.white);

        [Space(10)]
        [Header("Height Fog")]
        [Tooltip("How much height affects fog density (0 = uniform, 1 = full height-based falloff)")]
        public ClampedFloatParameter HeightFogDensity = new(0.5f, 0.0f, 1.0f);

        [Tooltip("Height falloff rate - higher = fog concentrated closer to ground")]
        public ClampedFloatParameter HeightFogFalloff = new(2.0f, 0.0f, 10.0f);

        [Space(10)]
        [Header("Tone Mapping")]
        [Tooltip("Contrast curve power - higher values crush shadows more for punchier rays")]
        public ClampedFloatParameter Contrast = new(1.5f, 0.5f, 4.0f);

        [Space(10)]
        [Header("Blur Settings")]
        public ClampedIntParameter GaussSamples = new(4, 0, 8);

        public MinFloatParameter GaussAmount = new(0.5f, 0.0f);
        public MinFloatParameter BlurDepthFalloff = new(300.0f, 0.0f);
    }
}
