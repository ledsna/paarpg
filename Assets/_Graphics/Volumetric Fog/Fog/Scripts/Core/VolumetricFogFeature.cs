using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    public class VolumetricFogFeature : ScriptableRendererFeature
    {
        [Required] public Shader VolumetricFogShader;
        [Required] public Texture3D ShapeTexture;
        [Required] public Texture3D DetailTexture;

        [Tooltip("Number of raymarching steps. Higher = better quality but slower.")]
        [Range(1, 128)] public int NumSteps = 32;

        [Tooltip("Light-march steps toward sun")]
        [InfoBox("Currently NumStepsLight set to 8 in shader with #define NUM_STEPS_LIGHT 8. Made for better performance.")]
        [ReadOnly]
        [Range(1, 16)] public int NumStepsLight = 16;

        private Material material;
        private VolumetricFogPass volumetricFogPass;
        private VolumetricFogVolumeComponent volumeComponent;

        public override void Create()
        {
            volumetricFogPass = new VolumetricFogPass(ShapeTexture, DetailTexture);
            volumetricFogPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (volumetricFogPass == null)
                return;

            if (VolumetricFogShader == null)
                return;

            if (material == null)
                material = new Material(VolumetricFogShader);

            var stack = VolumeManager.instance.stack;
            volumeComponent = stack.GetComponent<VolumetricFogVolumeComponent>();
            volumetricFogPass.Setup(material, NumSteps, NumStepsLight, volumeComponent);
            renderer.EnqueuePass(volumetricFogPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}