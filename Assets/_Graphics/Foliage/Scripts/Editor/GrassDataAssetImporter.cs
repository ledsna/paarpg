using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Grass.Core;

namespace Grass.Editor
{
    /// <summary>
    /// Import any files with the .grassdata extension as GrassDataAsset
    /// </summary>
    [ScriptedImporter(2, "grassdata")]
    public sealed class GrassDataAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Read the binary file contents
            byte[] fileBytes = File.ReadAllBytes(ctx.assetPath);

            // Create GrassDataAsset ScriptableObject to hold the binary data
            var grassDataAsset = ScriptableObject.CreateInstance<GrassDataAsset>();
            grassDataAsset.Data = fileBytes;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("./Icons/grass_icon.png");
            ctx.AddObjectToAsset("Grass Data", grassDataAsset, icon);
            ctx.SetMainObject(grassDataAsset);
        }
    }
}