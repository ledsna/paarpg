using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    public class VolumetricFogFeature : ScriptableRendererFeature
    {
        // Пришлось отказаться от Shader по той причине, что внутри AddRenderPasses запрещается
        // через доки создавать объекты, а в Create оно работает через жопу, так как вызов Create 
        // происходит тоже часто и надо иметь Dispose который вызывается криво и вызывает
        // ошибки при попытке осовбодить созданный ресурс.
        // Если что источник: https://docs.unity3d.com/6000.5/Documentation/Manual/urp/renderer-features/scriptable-renderer-features/inject-a-pass-using-a-scriptable-renderer-feature.html
        [Required] public Material material;
        [Required] public Texture3D ShapeTexture;
        [Required] public Texture3D DetailTexture;

        [Tooltip("Number of raymarching steps. Higher = better quality but slower.")]
        [Range(1, 128)] public int NumSteps = 32;

        [Tooltip("Light-march steps toward sun")]
        [InfoBox("Currently NumStepsLight set to 8 in shader with #define NUM_STEPS_LIGHT 8. Made for better performance.")]
        [ReadOnly]
        [Range(1, 16)] public int NumStepsLight = 16;
        
        [Header("General")]
        [SerializeField] private bool renderInScene;

        public bool useCustomFogSettingsForScene;
        [ShowIf("useCustomFogSettingsForScene")]
        [Tooltip("Используй кастомный пресет чтобы переопределить какие то настройки в сцене")]
        [SerializeField] private VolumetricFogVolumeComponent sceneFogSettings;
        
        
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

            var isSceneCamera = renderingData.cameraData.cameraType == CameraType.SceneView;
            if (!renderInScene && isSceneCamera)
                return;
            
            if (material == null)
            {
                Debug.LogWarning(name + " material is null and will be skipped.");
                return;
            }
            
            var stack = VolumeManager.instance.stack;
            volumeComponent = stack.GetComponent<VolumetricFogVolumeComponent>();
            var sceneVolumeComponent = useCustomFogSettingsForScene && isSceneCamera ? sceneFogSettings : null;
            volumetricFogPass.Setup(material, NumSteps, NumStepsLight, volumeComponent, sceneVolumeComponent);
            renderer.EnqueuePass(volumetricFogPass);
        }
    }
}