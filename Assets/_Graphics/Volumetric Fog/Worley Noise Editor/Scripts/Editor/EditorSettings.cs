using UnityEngine;
using __Project.Shared.Editor;

namespace WorleyNoise.Editor
{
    public class EditorSettings : ScriptableObject
    {
        public WorleyNoiseEditor.CloudNoiseType activeTextureType;
        public WorleyNoiseEditor.TextureChannel activeChannel;

        public ComputeShader worleyShader;
        public ComputeShader utilsShader;
        public FilterMode filterMode;

        public int shapeResolution = 128;
        public int detailResolution = 32;

        public WorleyNoiseSettingsPreset shapeSettings;
        public WorleyNoiseSettingsPreset detailSettings;

        public bool viewerGreyscale;
        public bool viewerShowAllChannels;
        public float previewDepthSlice;

        public string lastSaveDirectory;

        private const bool Log = true;

        public void SetUpReferences()
        {
            Utils.LoadFirstAssetIfNull(ref worleyShader, "t:ComputeShader WorleyNoise", Log);
            Utils.LoadFirstAssetIfNull(ref utilsShader, "t:ComputeShader WorleyUtils", Log);
        }
    }
}