using UnityEditor;
using UnityEngine;

namespace __Project.Shared.Editor
{
    public static class Utils
    {
        public static void LoadFirstAssetIfNull<T>(ref T asset, string searchString, bool log = true) where T : Object
        {
            if (asset != null)
                return;

            asset = GetFirstAsset<T>(searchString, log);
        }

        public static T GetFirstAsset<T>(string searchString, bool log = true) where T : Object
        {
            var guids = AssetDatabase.FindAssets(searchString);
            if (guids.Length == 0 && log)
            {
                Debug.LogWarning($"Can't find {typeof(T).Name} by search string {searchString}");
                return null;
            }
            
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (guids.Length > 1 && asset != null && log)
                Debug.LogWarning($"Found several {asset.GetType().Name} by search string {searchString}");
            return asset;
        } 
    }
}