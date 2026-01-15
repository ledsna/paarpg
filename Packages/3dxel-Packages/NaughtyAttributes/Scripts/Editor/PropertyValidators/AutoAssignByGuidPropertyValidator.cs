using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class AutoAssignByGuidPropertyValidator : AutoAssignPropertyValidatorBase
    {
        protected override void AutoAssign_Internal(SerializedProperty property,
            AutoAssignAttributeBase baseAttribute, Object targetObject, string propertyName)
        {
            var attr = baseAttribute as AutoAssignByGuidAttribute;
            var type = PropertyUtility.GetTargetTypeOfProperty(property);
            var path = AssetDatabase.GUIDToAssetPath(attr.Guid);
            var asset = AssetDatabase.LoadAssetAtPath(path, type);
            property.objectReferenceValue = asset;
            if (baseAttribute.Verbose && asset is null)
                Debug.LogWarning($"Can't find {type.Name} by guid ({attr.Guid})", targetObject);
        }
    }
}