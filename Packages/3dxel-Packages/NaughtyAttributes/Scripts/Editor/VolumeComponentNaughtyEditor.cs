using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NaughtyAttributes;
using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TestVolume
{
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [CustomEditor(typeof(VolumeComponent), true)]
    public class VolumeComponentNaughtyEditor : UniversalRenderPipelineVolumeComponentEditor
    {
        private List<SerializedProperty> _serializedProperties = new();
        private IEnumerable<MethodInfo> _methods;
        private Dictionary<string, SavedBool> _foldouts = new();
        
        // Cached Values
        List<SerializedProperty> nonGroupedProperties;
        List<IGrouping<string, SerializedProperty>> groupedProperties;
        List<IGrouping<string, SerializedProperty>> foldoutProperties;

        public override void OnEnable()
        {
            base.OnEnable();

            _methods = ReflectionUtility.GetAllMethods(
                target, m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0);
            
            GetSerializedProperties(ref _serializedProperties);
            nonGroupedProperties = GetNonGroupedProperties(_serializedProperties).ToList();
            groupedProperties = GetGroupedProperties(_serializedProperties).ToList();
            foldoutProperties = GetFoldoutProperties(_serializedProperties).ToList();
        }

        public override void OnDisable()
        {
            ReorderableListPropertyDrawer.Instance.ClearCache();
        }

        public override void OnInspectorGUI()
        {
            bool anyNaughtyAttribute =
                _serializedProperties.Any(p => PropertyUtility.GetAttribute<INaughtyAttribute>(p) != null);
            if (!anyNaughtyAttribute)
            {
                base.OnInspectorGUI();
            }
            else
            {
                DrawSerializedProperties();
            }

            DrawButtons();
        }

        protected void GetSerializedProperties(ref List<SerializedProperty> outSerializedProperties)
        {
            outSerializedProperties.Clear();
            using (var iterator = serializedObject.GetIterator())
            {
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (iterator.name == "active" || iterator.name == "m_Script")
                            continue;
                        outSerializedProperties.Add(serializedObject.FindProperty(iterator.name));
                    } while (iterator.NextVisible(false));
                }
            }
        }

        protected void DrawParameterOrPropertyField(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Generic)
            {
                var sdp = Unpack(property);
                if (sdp.value != null)
                {
                    NaughtyEditorGUI.ParameterField_Layout(property, sdp, PropertyField);
                    return;
                }
            }

            NaughtyEditorGUI.PropertyField_Layout(property, includeChildren: true);
        }

        protected void DrawSerializedProperties()
        {
            serializedObject.Update();

            // Draw non-grouped serialized properties
            foreach (var property in nonGroupedProperties)
            {
                if (property.name.Equals("m_Script", System.StringComparison.Ordinal))
                {
                    using (new EditorGUI.DisabledScope(disabled: true))
                    {
                        EditorGUILayout.PropertyField(property);
                    }
                }
                else
                {
                    DrawParameterOrPropertyField(property);
                }
            }

            // Draw grouped serialized properties
            foreach (var group in groupedProperties)
            {
                IEnumerable<SerializedProperty> visibleProperties = group.Where(p => PropertyUtility.IsVisible(p));
                if (!visibleProperties.Any())
                {
                    continue;
                }

                NaughtyEditorGUI.BeginBoxGroup_Layout(group.Key);
                foreach (var property in visibleProperties)
                {
                    DrawParameterOrPropertyField(property);
                }

                NaughtyEditorGUI.EndBoxGroup_Layout();
            }

            // Draw foldout serialized properties
            foreach (var group in foldoutProperties)
            {
                IEnumerable<SerializedProperty> visibleProperties = group.Where(p => PropertyUtility.IsVisible(p));
                if (!visibleProperties.Any())
                {
                    continue;
                }

                if (!_foldouts.ContainsKey(group.Key))
                {
                    _foldouts[group.Key] = new SavedBool($"{target.GetInstanceID()}.{group.Key}", false);
                }

                _foldouts[group.Key].Value = EditorGUILayout.Foldout(_foldouts[group.Key].Value, group.Key, true);
                if (_foldouts[group.Key].Value)
                {
                    foreach (var property in visibleProperties)
                    {
                        DrawParameterOrPropertyField(property);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // For working InfoBoxAttribute and HorizontalLineAttribute you add several lines into built-in unity packages
        // But if you don't need this attributes, you can just remove override or even remove entire method
        // Read more in README.md
        protected override void HandleCustomDecorators(SerializedDataParameter property)
        {
            foreach (var attr in property.attributes)
            {
                if (!(attr is PropertyAttribute))
                    continue;

                DecoratorDrawer drawer = null;
                switch (attr)
                {
                    case InfoBoxAttribute infoBoxAttribute:
                        drawer = new InfoBoxDecoratorDrawer(infoBoxAttribute);
                        break;
                    case HorizontalLineAttribute horizontalLineAttribute:
                        drawer = new HorizontalLineDecoratorDrawer(horizontalLineAttribute);
                        break;
                }

                if (drawer != null)
                {
                    var h = drawer.GetHeight();
                    var rect = EditorGUILayout.GetControlRect(false, h);
                    drawer.OnGUI(rect);
                }
            }
        }

        protected void DrawButtons(bool drawHeader = false)
        {
            if (_methods.Any())
            {
                if (drawHeader)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Buttons", GetHeaderGUIStyle());
                    NaughtyEditorGUI.HorizontalLine(
                        EditorGUILayout.GetControlRect(false), HorizontalLineAttribute.DefaultHeight,
                        HorizontalLineAttribute.DefaultColor.GetColor());
                }

                foreach (var method in _methods)
                {
                    NaughtyEditorGUI.Button(serializedObject.targetObject, method);
                }
            }
        }

        private static IEnumerable<SerializedProperty> GetNonGroupedProperties(
            IEnumerable<SerializedProperty> properties)
        {
            return properties.Where(p => PropertyUtility.GetAttribute<IGroupAttribute>(p) == null);
        }

        private static IEnumerable<IGrouping<string, SerializedProperty>> GetGroupedProperties(
            IEnumerable<SerializedProperty> properties)
        {
            return properties
                .Where(p => PropertyUtility.GetAttribute<BoxGroupAttribute>(p) != null)
                .GroupBy(p => PropertyUtility.GetAttribute<BoxGroupAttribute>(p).Name);
        }

        private static IEnumerable<IGrouping<string, SerializedProperty>> GetFoldoutProperties(
            IEnumerable<SerializedProperty> properties)
        {
            return properties
                .Where(p => PropertyUtility.GetAttribute<FoldoutAttribute>(p) != null)
                .GroupBy(p => PropertyUtility.GetAttribute<FoldoutAttribute>(p).Name);
        }

        private static GUIStyle GetHeaderGUIStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperCenter;

            return style;
        }
    }
}