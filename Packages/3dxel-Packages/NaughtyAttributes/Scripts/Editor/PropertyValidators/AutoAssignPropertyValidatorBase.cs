using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public abstract class AutoAssignPropertyValidatorBase : PropertyValidatorBase
    {
        private static readonly HashSet<string> validated = new();
        
        public override void ValidateProperty(SerializedProperty property)
        {
            var so = property.serializedObject;
            so.UpdateIfRequiredOrScript();
            
            // TODO: check key for correctness
            var key = property.serializedObject.targetObject.GetInstanceID() + "|" + property.propertyPath;
            if (validated.Contains(key))
                return;
            validated.Add(key);
            
            if (property.objectReferenceValue is not null)
                return;
            
            var baseAttribute = PropertyUtility.GetAttribute<AutoAssignAttributeBase>(property);
            var targetObject = property.serializedObject.targetObject; 
            var propertyName = $"{property.serializedObject.targetObject.GetType().FullName}.{property.displayName}";
            AutoAssign_Internal(property, baseAttribute, targetObject, propertyName);
            
            so.ApplyModifiedProperties();
        }

        protected abstract void AutoAssign_Internal(SerializedProperty property, 
            AutoAssignAttributeBase baseAttribute,
            Object targetObject,
            string propertyName);
    }
}