using UnityEditor;
using Grass.Core;
using System.IO;

namespace Grass.Editor
{
    public class GrassDataMenu
    {
        [MenuItem("Assets/Create/Grass Data File", false)]
        private static void CreateGrassDataFile()
        {
            // Get selected path in Project window
            string path = "Assets";
            if (Selection.activeObject != null)
            {
                path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(path)) // If selecting a file, use its parent directory
                {
                    path = Path.GetDirectoryName(path);
                }
            }

            GrassDataManager.CreateGrassDataAsset(path);

            // Refresh the AssetDatabase
            AssetDatabase.Refresh();
        }
    }
}