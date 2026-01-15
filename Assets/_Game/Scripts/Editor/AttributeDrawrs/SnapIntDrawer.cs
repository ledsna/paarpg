using UnityEditor;
using UnityEngine;
using __Project.Shared.Attributes;

namespace __Project.Editor.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(SnapAttribute))]
    public class SnapIntDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var snap = (SnapAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            var val = EditorGUI.IntField(position, label, property.intValue);

            var snapped = Mathf.RoundToInt((float)val / snap.Step) * snap.Step;

            property.intValue = snapped;

            EditorGUI.EndProperty();
        }
    }
}