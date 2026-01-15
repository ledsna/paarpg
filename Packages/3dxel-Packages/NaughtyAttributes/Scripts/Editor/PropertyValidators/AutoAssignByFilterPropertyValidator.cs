using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class AutoAssignByFilterPropertyValidator : AutoAssignPropertyValidatorBase
    {
        protected override void AutoAssign_Internal(SerializedProperty property,
            AutoAssignAttributeBase baseAttribute, Object targetObject, string propertyName)
        {
            var attr = baseAttribute as AutoAssignByFilterAttribute;

            if (!attr!.IsSearchInFoldersValid)
            {
                Debug.LogError($"{propertyName} contains null or empty folders path for search", targetObject);
                return;
            }

            var type = PropertyUtility.GetTargetTypeOfProperty(property);
            var asset = LoaderUtility.GetFirstAsset(type, attr.Filter, attr.SearchInFolders, out var message, propertyName);
            if (baseAttribute.Verbose && message is not null)
                Debug.LogWarning(message, targetObject);

            property.objectReferenceValue = asset;
        }
    }
}