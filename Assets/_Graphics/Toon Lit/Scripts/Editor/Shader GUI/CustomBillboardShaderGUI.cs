using UnityEditor;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    public class CustomBillboardShaderGUI : CustomLitShader
    {
        bool showOutlineThresholds = false;
        bool showOutlineHeader = true;
        bool showCelShadingHeader = true;
        bool showBillboardSettings = true;
        bool showTextureArray = true;
        bool showWindSettings = true;
        bool showFlowerSettings = true;

        private MaterialProperty[] properties;

        // Billboard Properties
        private MaterialProperty _Scale;
        private MaterialProperty _DEBUG_CULL_MASK;

        // Texture Array
        private MaterialProperty _TextureArray;

        // Wind Properties
        private MaterialProperty _WindSpeed;
        private MaterialProperty _WindStrength;
        private MaterialProperty _WindDirection;
        private MaterialProperty _WindFrequency;
        private MaterialProperty _WindGustStrength;

        // Wild Grass Properties
        private MaterialProperty _WildGrassChance;
        private MaterialProperty _WildNormalStrength;

        // Flower Properties
        private MaterialProperty _FlowerSizeMultiplier;
        private MaterialProperty _FlowerSizeVariation;
        private MaterialProperty _FlowerCameraNudge;

        // Outline Thresholds
        private MaterialProperty _DepthThreshold;
        private MaterialProperty _NormalsThreshold;
        private MaterialProperty _ExternalScale;
        private MaterialProperty _InternalScale;

        // Outline Settings
        private MaterialProperty _OutlineColour;
        private MaterialProperty _OutlineStrength;
        private MaterialProperty _DebugOn;
        private MaterialProperty _External;
        private MaterialProperty _Convex;
        private MaterialProperty _Concave;

        // Cel Shading
        private MaterialProperty _DiffuseSpecularCelShader;
        private MaterialProperty _DiffuseSteps;
        private MaterialProperty _FresnelSteps;
        private MaterialProperty _SpecularStep;
        private MaterialProperty _DistanceSteps;
        private MaterialProperty _ShadowSteps;
        private MaterialProperty _ReflectionSteps;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;

            // Find Billboard Properties
            _Scale = FindProperty("_Scale", properties);
            _DEBUG_CULL_MASK = FindProperty("_DEBUG_CULL_MASK", properties);

            // Find Texture Array
            _TextureArray = FindProperty("_TextureArray", properties);

            // Find Wind Properties
            _WindSpeed = FindProperty("_WindSpeed", properties);
            _WindStrength = FindProperty("_WindStrength", properties);
            _WindDirection = FindProperty("_WindDirection", properties);
            _WindFrequency = FindProperty("_WindFrequency", properties);
            _WindGustStrength = FindProperty("_WindGustStrength", properties);

            // Find Wild Grass Properties
            _WildGrassChance = FindProperty("_WildGrassChance", properties);
            _WildNormalStrength = FindProperty("_WildNormalStrength", properties);

            // Find Flower Properties (skip _UseTextureColor as it's controlled via keyword by C#)
            _FlowerSizeMultiplier = FindProperty("_FlowerSizeMultiplier", properties, false);
            _FlowerSizeVariation = FindProperty("_FlowerSizeVariation", properties, false);
            _FlowerCameraNudge = FindProperty("_FlowerCameraNudge", properties, false);

            // Find Outline Thresholds
            _DepthThreshold = FindProperty("_DepthThreshold", properties);
            _NormalsThreshold = FindProperty("_NormalsThreshold", properties);
            _ExternalScale = FindProperty("_ExternalScale", properties);
            _InternalScale = FindProperty("_InternalScale", properties);

            // Find Outline Settings
            _OutlineColour = FindProperty("_OutlineColour", properties);
            _OutlineStrength = FindProperty("_OutlineStrength", properties);
            _DebugOn = FindProperty("_DebugOn", properties);
            _External = FindProperty("_External", properties);
            _Convex = FindProperty("_Convex", properties);
            _Concave = FindProperty("_Concave", properties);

            // Find Cel Shading
            _DiffuseSpecularCelShader = FindProperty("_DiffuseSpecularCelShader", properties);
            _DiffuseSteps = FindProperty("_DiffuseSteps", properties);
            _FresnelSteps = FindProperty("_FresnelSteps", properties);
            _SpecularStep = FindProperty("_SpecularStep", properties);
            _DistanceSteps = FindProperty("_DistanceSteps", properties);
            _ShadowSteps = FindProperty("_ShadowSteps", properties);
            _ReflectionSteps = FindProperty("_ReflectionSteps", properties);

            DrawCustomProperties();
            DrawDefaultProperties();
        }

        private void DrawDefaultProperties()
        {
            base.OnGUI(materialEditor, properties);
        }

        private void DrawCustomProperties()
        {
            // Billboard Settings
            showBillboardSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBillboardSettings, "Billboard Settings");
            if (showBillboardSettings)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_Scale, "Scale");
                materialEditor.ShaderProperty(_DEBUG_CULL_MASK, "Debug Cull Mask");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Texture Array
            showTextureArray = EditorGUILayout.BeginFoldoutHeaderGroup(showTextureArray, "Texture Array");
            if (showTextureArray)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Assign a Texture2DArray asset for automatic variation. Each grass blade will randomly pick one texture from the array based on its world position.\n\nTo create a Texture2DArray, select multiple textures in Project view, right-click → Create → Texture2D Array.", MessageType.Info);
                EditorGUILayout.Space();

                materialEditor.TextureProperty(_TextureArray, "Texture Array");

                // Show texture array info
                if (_TextureArray.textureValue is Texture2DArray texArray)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Array contains {texArray.depth} textures", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Size: {texArray.width}x{texArray.height}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Wind Settings
            showWindSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showWindSettings, "Wind Settings");
            if (showWindSettings)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_WindSpeed, "Wind Speed");
                materialEditor.ShaderProperty(_WindStrength, "Wind Strength");
                materialEditor.ShaderProperty(_WindDirection, "Wind Direction");
                materialEditor.ShaderProperty(_WindFrequency, "Wind Frequency");
                materialEditor.ShaderProperty(_WindGustStrength, "Wind Gust Strength");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Wild Grass", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_WildGrassChance, "Wild Grass Chance");
                materialEditor.ShaderProperty(_WildNormalStrength, "Wild Normal Strength");

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Flower Settings
            showFlowerSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showFlowerSettings, "Flower Settings");
            if (showFlowerSettings)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("USE_TEXTURE_COLOR keyword is controlled by the material's 'Use Texture Color' setting in GrassMaterialVariant", MessageType.Info);
                if (_FlowerSizeMultiplier != null)
                    materialEditor.ShaderProperty(_FlowerSizeMultiplier, "Flower Size Multiplier");
                if (_FlowerSizeVariation != null)
                    materialEditor.ShaderProperty(_FlowerSizeVariation, "Flower Size Variation");
                if (_FlowerCameraNudge != null)
                    materialEditor.ShaderProperty(_FlowerCameraNudge, "Flower Camera Nudge");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Outline Thresholds
            showOutlineThresholds = EditorGUILayout.BeginFoldoutHeaderGroup(showOutlineThresholds, "Outline Thresholds");
            if (showOutlineThresholds)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_DepthThreshold, "Depth Threshold");
                materialEditor.ShaderProperty(_NormalsThreshold, "Normals Threshold");
                materialEditor.ShaderProperty(_ExternalScale, "External Thickness");
                materialEditor.ShaderProperty(_InternalScale, "Internal Thickness");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Outline Settings
            showOutlineHeader = EditorGUILayout.BeginFoldoutHeaderGroup(showOutlineHeader, "Outline Settings");
            if (showOutlineHeader)
            {
                materialEditor.ShaderProperty(_OutlineColour, "Outline Colour");
                materialEditor.ShaderProperty(_OutlineStrength, "Intensity");
                materialEditor.ShaderProperty(_DebugOn, "Debug View");
                materialEditor.ShaderProperty(_External, "External");
                materialEditor.ShaderProperty(_Convex, "Internal Convex");
                materialEditor.ShaderProperty(_Concave, "Internal Concave");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Cel Shading Settings
            showCelShadingHeader = EditorGUILayout.BeginFoldoutHeaderGroup(showCelShadingHeader, "Cel Shading Settings");
            if (showCelShadingHeader)
            {
                materialEditor.ShaderProperty(_DiffuseSpecularCelShader, "Diffuse-Specular Cel Shader");
                materialEditor.ShaderProperty(_DiffuseSteps, "Diffuse Lighting Steps");
                materialEditor.ShaderProperty(_FresnelSteps, "Fresnel Steps");
                materialEditor.ShaderProperty(_SpecularStep, "Specular Step Size");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Attenuation Steps", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_DistanceSteps, "Light Distance Steps");
                materialEditor.ShaderProperty(_ShadowSteps, "Shadow Steps");
                materialEditor.ShaderProperty(_ReflectionSteps, "Reflection Steps");

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();
        }
    }
}
