using Grass.Core;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassHolder))]
    public class GrassHolderEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty mesh;
        private SerializedProperty materialSystem;
        private SerializedProperty normalLimit;
        private SerializedProperty depthCullingTree;
        private SerializedProperty UseOctreeCulling;
        private SerializedProperty drawBounds;
        private SerializedProperty GrassDataSource;
        private SerializedProperty OrtographicCamera;
        private SerializedProperty renderingLayerMask;

        private void OnEnable()
        {
            // Initialize serialized properties
            mesh = serializedObject.FindProperty("mesh");
            materialSystem = serializedObject.FindProperty("materialSystem");
            normalLimit = serializedObject.FindProperty("normalLimit");
            depthCullingTree = serializedObject.FindProperty("depthCullingTree");
            UseOctreeCulling = serializedObject.FindProperty("UseOctreeCulling");
            drawBounds = serializedObject.FindProperty("drawBounds");
            GrassDataSource = serializedObject.FindProperty("GrassDataSource");
            OrtographicCamera = serializedObject.FindProperty("OrtographicCamera");
            renderingLayerMask = serializedObject.FindProperty("renderingLayerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update(); // Start updating serialized properties

            // Get reference to the target script
            var script = (GrassHolder)target;

            // Check if the TextAsset is null
            if (script.GrassDataSource == null)
            {
                // Grass Data Source
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Grass Data", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(GrassDataSource, new GUIContent("Grass Data Source"));

                EditorGUILayout.HelpBox("Grass Data Source missing! Create new or select existing .grassdata file",
                    MessageType.Error);

                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Grass Data Source
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grass Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(GrassDataSource, new GUIContent("Grass Data Source"));

            // Material System and Mesh
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(materialSystem, new GUIContent("Material System"));
            EditorGUILayout.PropertyField(mesh, new GUIContent("Mesh"));

            // Generation Settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(normalLimit, new GUIContent("Slope Limit"));

            // Rendering Options
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rendering Settings", EditorStyles.boldLabel);
            
            // Custom rendering layer mask dropdown
            DrawRenderingLayerMaskField();

            // Culling Options
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Culling Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(UseOctreeCulling, new GUIContent("Use Octree Culling"));

            if (UseOctreeCulling.boolValue)
            {
                EditorGUILayout.PropertyField(drawBounds, new GUIContent("Draw Bounds"));

                // Draw the int slider for Depth Culling Tree
                EditorGUILayout.IntSlider(depthCullingTree, 1, 6, new GUIContent("Depth Culling Tree"));
            }

            // Apply changes
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawRenderingLayerMaskField()
        {
            // Get the current mask value
            uint currentMask = (uint)renderingLayerMask.intValue;
            
            // Get rendering layer names from the current render pipeline
            string[] layerNames = GetRenderingLayerNames();
            
            // Create a proper mask field dropdown
            int newMask = EditorGUILayout.MaskField("Rendering Layer Mask", (int)currentMask, layerNames);
            
            // Update if changed
            if (newMask != (int)currentMask)
            {
                renderingLayerMask.intValue = newMask;
            }
        }
        
        private string[] GetRenderingLayerNames()
        {
            string[] layerNames = new string[32];
            
            // Try to get names from the current render pipeline
            var renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (renderPipeline != null)
            {
                // Check if it's URP
                if (renderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
                {
                    // For URP, try to get the layer names
                    try
                    {
                        var property = renderPipeline.GetType().GetProperty("renderingLayerMaskNames");
                        if (property != null)
                        {
                            var names = property.GetValue(renderPipeline) as string[];
                            if (names != null && names.Length > 0)
                            {
                                for (int i = 0; i < Mathf.Min(names.Length, 32); i++)
                                {
                                    layerNames[i] = !string.IsNullOrEmpty(names[i]) ? names[i] : $"Layer {i}";
                                }
                                return layerNames;
                            }
                        }
                    }
                    catch
                    {
                        // Fall through to default names
                    }
                }
            }
            
            // Fallback to default names
            for (int i = 0; i < 32; i++)
            {
                layerNames[i] = $"Layer {i}";
            }
            
            return layerNames;
        }
    }
}