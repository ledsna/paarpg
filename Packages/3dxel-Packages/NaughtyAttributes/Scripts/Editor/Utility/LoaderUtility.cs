using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    public static class LoaderUtility
    {
        public static T GetFirstAsset<T>(string filter, out string message, string[] searchInFolders = null) where T : Object
        {
            return GetFirstAsset(typeof(T), filter, searchInFolders, out message) as T;
        } 
        
        internal static Object GetFirstAsset(Type type, string filter, string[] searchInFolders, out string message, string propertyName = "")
        {
            var guids = searchInFolders is null ?
                AssetDatabase.FindAssets(filter) :
                AssetDatabase.FindAssets(filter, searchInFolders);
            
            message = null;
            if (guids.Length == 0)
            {
                message = $"Can't find {type.Name} by filter '{filter}'";
                if (propertyName != "")
                    message += " for " + propertyName;
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath(path, type);
            if (guids.Length > 1)
            {
                var stringBuilder = new StringBuilder($"Found {guids.Length} {type.Name} by filter '{filter}'");
                stringBuilder.Append(propertyName != "" ? $" for {propertyName}: \n\n" : ":\n\n");
                var count = Mathf.Min(guids.Length, 5);
                foreach (var giud in guids.Take(count))
                {
                    path = AssetDatabase.GUIDToAssetPath(giud);
                    stringBuilder.Append($"\t{giud} — {path}\n");
                }
                if (count < guids.Length)
                    stringBuilder.Append("\t...");
                message = stringBuilder.ToString();
            }

            return asset;
        } 
    }
}