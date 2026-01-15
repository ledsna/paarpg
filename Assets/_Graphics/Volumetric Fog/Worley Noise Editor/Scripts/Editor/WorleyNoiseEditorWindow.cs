using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorleyNoise.Editor
{
    public class DropDownWroleyNoiseSettingsPreset
    {
        private WorleyNoiseSettingsPreset preset;
        private string name;
        private bool showPresetFoldout;


        public DropDownWroleyNoiseSettingsPreset(WorleyNoiseSettingsPreset preset, string name)
        {
            this.preset = preset;
            this.name = name;
        }

        public WorleyNoiseSettingsPreset OnGUI()
        {
            // Foldout with object field on same line
            EditorGUILayout.BeginHorizontal();
            if (preset != null)
            {
                showPresetFoldout = EditorGUILayout.Foldout(showPresetFoldout, name, true);
                preset = (WorleyNoiseSettingsPreset)EditorGUILayout.ObjectField(preset,
                    typeof(WorleyNoiseSettingsPreset), false);
            }
            else
                preset = (WorleyNoiseSettingsPreset)EditorGUILayout.ObjectField(name, preset,
                    typeof(WorleyNoiseSettingsPreset), false);


            EditorGUILayout.EndHorizontal();

            if (showPresetFoldout && preset != null)
            {
                EditorGUI.indentLevel++;

                var serializedPreset = new SerializedObject(preset);
                serializedPreset.Update();

                var prop = serializedPreset.GetIterator();
                var enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    if (prop.name == "m_Script") continue;
                    EditorGUILayout.PropertyField(prop, true);
                    enterChildren = false;
                }

                if (serializedPreset.ApplyModifiedProperties())
                    EditorUtility.SetDirty(preset);

                EditorGUI.indentLevel--;
            }

            return preset;
        }
    }

    public class WorleyNoiseEditorWindow : EditorWindow
    {
        Vector2 scroll;
        private EditorSettings es;

        private DropDownWroleyNoiseSettingsPreset shapeSettings;
        private DropDownWroleyNoiseSettingsPreset detailSettings;

        private WorleyNoiseSettings lastNoiseSettings;
        private bool showEditorSettings;

        private Saver3D saver;

        private WorleyNoiseSettings activeSettings
        {
            get
            {
                var settings =
                    es.activeTextureType == WorleyNoiseEditor.CloudNoiseType.Shape
                        ? es.shapeSettings
                        : es.detailSettings;
                return settings[es.activeChannel];
            }
        }

        private Vector4 channelMask =>
            new(
                (es.activeChannel == WorleyNoiseEditor.TextureChannel.R) ? 1 : 0,
                (es.activeChannel == WorleyNoiseEditor.TextureChannel.G) ? 1 : 0,
                (es.activeChannel == WorleyNoiseEditor.TextureChannel.B) ? 1 : 0,
                (es.activeChannel == WorleyNoiseEditor.TextureChannel.A) ? 1 : 0
            );

        private static WorleyNoiseEditor worleyNoiseEditor;

        private Material previewMaterial;
        private bool[] foldouts;

        [MenuItem("Tools/Worley Noise Editor")]
        public static void Init()
        {
            var window = GetWindow<WorleyNoiseEditorWindow>("Worley Noise Editor");
            window.minSize = new Vector2(400, 620);
        }

        private void LoadOrCreateSettings()
        {
            __Project.Shared.Editor.Utils.LoadFirstAssetIfNull(ref es, "t:EditorSettings", true);

            if (es == null)
            {
                var path = "Assets/Temp/Settings.asset";
                Debug.Log($"Can't Load Settings, create a new one in: {path}");
                es = CreateInstance<EditorSettings>();
                AssetDatabase.CreateAsset(es, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();
            es.SetUpReferences();

            worleyNoiseEditor?.Release();
            worleyNoiseEditor = new WorleyNoiseEditor();
            saver = new Saver3D(es.utilsShader);
            shapeSettings = new DropDownWroleyNoiseSettingsPreset(es.shapeSettings, "Shape Preset");
            detailSettings = new DropDownWroleyNoiseSettingsPreset(es.detailSettings, "Detail Preset");

            if (es.shapeSettings != null)
                es.shapeSettings.ValueWasChanged -= RedrawNoise;
            if (es.detailSettings != null)
                es.shapeSettings.ValueWasChanged += RedrawNoise;

            if (es.detailSettings != null)
                es.detailSettings.ValueWasChanged -= RedrawNoise;
            if (es.detailSettings != null)
                es.detailSettings.ValueWasChanged += RedrawNoise;
        }

        private void OnDisable()
        {
            worleyNoiseEditor?.Release();
            worleyNoiseEditor = null;

            if (es.shapeSettings != null)
                es.shapeSettings.ValueWasChanged -= RedrawNoise;

            if (es.detailSettings != null)
                es.detailSettings.ValueWasChanged -= RedrawNoise;
        }

        private void OnGUI()
        {
            if (es == null)
            {
                EditorGUILayout.HelpBox("Editor settings is null.", MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            var header = new GUIContent(
                "  Editor Settings",
                EditorGUIUtility.IconContent("d_Settings").image
            );

            showEditorSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showEditorSettings, header);
            if (showEditorSettings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    es.worleyShader =
                        (ComputeShader)EditorGUILayout.ObjectField("Worley Shader", es.worleyShader,
                            typeof(ComputeShader),
                            false);
                    es.utilsShader =
                        (ComputeShader)EditorGUILayout.ObjectField("Copy Shader", es.utilsShader,
                            typeof(ComputeShader),
                            false);

                    es.filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", es.filterMode);
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            es.activeTextureType =
                (WorleyNoiseEditor.CloudNoiseType)EditorGUILayout.EnumPopup("Texture Type", es.activeTextureType);
            es.activeChannel =
                (WorleyNoiseEditor.TextureChannel)EditorGUILayout.EnumPopup("Channel", es.activeChannel);

            es.shapeResolution =
                SnapToMultipleOf(EditorGUILayout.IntSlider("Shape Resolution", es.shapeResolution, 8, 256), 8);
            es.detailResolution =
                SnapToMultipleOf(EditorGUILayout.IntSlider("Detail Resolution", es.detailResolution, 8, 256), 8);

            es.viewerGreyscale = EditorGUILayout.Toggle("Viewer Greyscale", es.viewerGreyscale);
            es.viewerShowAllChannels = EditorGUILayout.Toggle("Show All Channels", es.viewerShowAllChannels);

            es.shapeSettings = shapeSettings.OnGUI();
            es.detailSettings = detailSettings.OnGUI();

            GUILayout.Space(10);

            worleyNoiseEditor.SetUpReferences(es.utilsShader, es.worleyShader, channelMask,
                es.shapeResolution, es.detailResolution, es.activeTextureType, es.filterMode);

            if (GUILayout.Button("Generate"))
                Generate();

            GUILayout.Space(10);
            DrawTexturePreview();

            EditorGUILayout.EndScrollView();
        }

        private int SnapToMultipleOf(int value, int multiple) => Mathf.RoundToInt((float)value / multiple) * multiple;

        private void DrawTexturePreview()
        {
            if (worleyNoiseEditor.targetRT != null)
            {
                if (previewMaterial == null)
                {
                    var shader = Shader.Find("Hidden/Texture3DPreview");
                    if (shader != null)
                        previewMaterial = new Material(shader);
                    else
                    {
                        EditorGUILayout.HelpBox("Preview shader not found.", MessageType.Warning);
                        return;
                    }
                }

                PreparePreviewMaterial();

                EditorGUILayout.LabelField("Noise Field Preview:", EditorStyles.boldLabel);

                var previewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));

                EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f)); // dark background

                EditorGUI.DrawPreviewTexture(previewRect, Texture2D.whiteTexture, previewMaterial,
                    ScaleMode.ScaleToFit);

                var buttonWidth = 60f;
                var buttonHeight = 20f;
                var padding = 5f;

                var clearButtonRect = new Rect(
                    previewRect.xMax - buttonWidth - padding,
                    previewRect.y + padding,
                    buttonWidth,
                    buttonHeight
                );

                if (GUI.Button(clearButtonRect, "Clear"))
                    Clear();

                clearButtonRect.x = padding;

                if (GUI.Button(clearButtonRect, "Export"))
                    ExportTexture();

                GUILayout.Space(10);

                es.previewDepthSlice = EditorGUILayout.Slider("Slice", es.previewDepthSlice, 0, 1);
            }
            else
                EditorGUILayout.HelpBox("Generate texture to see preview", MessageType.Info);
        }

        private void PreparePreviewMaterial()
        {
            previewMaterial.SetTexture("_VolumeTex", worleyNoiseEditor.targetRT);
            previewMaterial.SetFloat("_ZSlice", es.previewDepthSlice);
            previewMaterial.SetInteger("_GrayScale", es.viewerGreyscale ? 1 : 0);
            previewMaterial.SetVector("_ChannelMask", es.viewerShowAllChannels ? Vector4.one : channelMask);
        }

        private void RedrawNoise()
        {
            if (worleyNoiseEditor == null)
            {
                Debug.LogError("Worley Noise Generator is null");
                return;
            }

            var currentSettings = activeSettings;

            if (currentSettings.Equals(lastNoiseSettings))
                return;

            if (lastNoiseSettings == null ||
                lastNoiseSettings.seed != currentSettings.seed ||
                lastNoiseSettings.gridSizeA != currentSettings.gridSizeA ||
                lastNoiseSettings.gridSizeB != currentSettings.gridSizeB ||
                lastNoiseSettings.gridSizeC != currentSettings.gridSizeC)
                worleyNoiseEditor.Generate(currentSettings);

            worleyNoiseEditor.SetUpReferences(es.utilsShader, es.worleyShader, channelMask,
                es.shapeResolution, es.detailResolution, es.activeTextureType, es.filterMode);

            worleyNoiseEditor.Redraw(currentSettings);

            lastNoiseSettings = new WorleyNoiseSettings(currentSettings);
        }

        private void Generate()
        {
            if (ValidateParameters())
            {
                worleyNoiseEditor.Generate(activeSettings);
                worleyNoiseEditor.Redraw(activeSettings);
            }
            else
                Debug.LogError("Please, Set up all references in editor.");
        }

        private void Clear()
        {
            if (ValidateParameters())
                worleyNoiseEditor.Clear();
            else
                Debug.LogError("Please, Set up all references in editor. Clear skipped.");
        }

        private void ExportTexture()
        {
            var directory = es.lastSaveDirectory ?? Application.dataPath;

            var path = EditorUtility.SaveFilePanel(
                "Save 3D Noise Texture",
                directory,
                "WorleyNoise",
                "asset");

            if (path.Length == 0)
                return;

            var relativeToAssetFolderpath = path.Substring(path.LastIndexOf("Assets/", StringComparison.Ordinal));
            if (relativeToAssetFolderpath.Length != 0 && saver != null && worleyNoiseEditor != null)
            {
                saver.Save(worleyNoiseEditor.targetRT, relativeToAssetFolderpath);
                es.lastSaveDirectory = Path.GetDirectoryName(relativeToAssetFolderpath);
            }
        }

        private bool ValidateParameters()
        {
            return worleyNoiseEditor != null && activeSettings != null &&
                   es != null && es.worleyShader != null && es.utilsShader != null;
        }
    }
}