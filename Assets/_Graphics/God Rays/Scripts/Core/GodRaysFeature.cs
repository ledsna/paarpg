using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GodRays.Core
{
    public class GodRaysFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class GodRaysSettings
        {
            [InfoBox("This is default settings of God Rays effect. " +
                     "For changing params you need create Global Volume and add God Rays Volume Component")]
            [Min(0)]
            public float Intensity = 1;

            [Range(0, 0.95f)] public float Scattering = 0.5f;
            [Min(0)] public float Density = 1.0f;
            [Min(0)] public float MaxDistance = 100f;
            [Range(0, 200)] public float JitterVolumetric = 100;
            public Color GodRayColor = Color.white;

            [Header("Height Fog")]
            [Range(0, 1)] public float HeightFogDensity = 0.5f;
            [Range(0, 10)] public float HeightFogFalloff = 2.0f;

            [Header("Tone Mapping")]
            [Range(0.5f, 4f)] public float Contrast = 1.5f;
        }

        [Serializable]
        public class BlurSettings
        {
            [InfoBox("This is default settings of Bilaterial Blur. " +
                     "For changing params you need create Global Volume and add God Rays Volume Component")]
            // MAX VALUE = 7. You can't set values higher than 8
            [Range(0, 8)]
            public int GaussSamples = 4;

            [Min(0)] public float GaussAmount = 0.5f;

            [Min(0)] public float BlurDepthFalloff = 300.0f;
        }

        // God Rays
        // --------
        [SerializeField] private GodRaysSettings defaultGodRaysSettings;
        [SerializeField][Required] private Shader godRaysShader;
        private SampleCountEnum lastSampleCount;
        private Material godRaysMaterial;
        private GodRaysPass godRaysPass;

        [Space(10)]

        // Blur Settings
        // -------------
        [SerializeField]
        private BlurSettings defaultBlurSettings;
        [SerializeField][Required] private Shader blurShader;
        private Material blurMaterial;

        [Space(10)]

        // General 
        // -------
        [Header("General")]
        [SerializeField] private bool renderInScene = false;

        public override void Create()
        {
            godRaysPass = new GodRaysPass(defaultGodRaysSettings, defaultBlurSettings);
            godRaysPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (godRaysPass == null)
                return;

            if (!renderInScene && renderingData.cameraData.cameraType != CameraType.Game)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            // Check if Main Light exists and is active
            var mainLightIndex = renderingData.lightData.mainLightIndex;
            if (mainLightIndex == -1) // -1 means no main light
                return;

            var mainLight = renderingData.lightData.visibleLights[mainLightIndex];
            if (mainLight.light == null || !mainLight.light.enabled)
                return;

            if (godRaysMaterial == null)
                godRaysMaterial = new Material(godRaysShader);
            if (blurMaterial == null)
                blurMaterial = new Material(blurShader);
            godRaysPass.Setup(godRaysMaterial, blurMaterial);
            renderer.EnqueuePass(godRaysPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (Application.isPlaying)
            {
                Destroy(godRaysMaterial);
                Destroy(blurMaterial);
            }
            else
            {
                DestroyImmediate(godRaysMaterial);
                DestroyImmediate(blurMaterial);
            }
        }
    }
}