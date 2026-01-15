using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureArrayCreator
{
    [MenuItem("Assets/Create/Texture2DArray from Selection (Smart)")]
    static void CreateTextureArray()
    {
        Object[] selected = Selection.objects;
        var textures = new System.Collections.Generic.List<Texture2D>();

        foreach (var obj in selected)
        {
            if (obj is Texture2D tex)
                textures.Add(tex);
        }

        if (textures.Count == 0)
        {
            Debug.LogError("❌ No Texture2Ds selected!");
            return;
        }

        int width = textures[0].width;
        int height = textures[0].height;
        bool mipmaps = textures[0].mipmapCount > 1;

        // Attempt to preserve GPU compression if possible
        TextureFormat format = textures[0].format;
        Texture2DArray texArray;

        try
        {
            texArray = new Texture2DArray(width, height, textures.Count, format, mipmaps, false);
            for (int i = 0; i < textures.Count; i++)
            {
                if (mipmaps)
                {
                    int mipCount = textures[i].mipmapCount;
                    for (int mip = 0; mip < mipCount; mip++)
                        Graphics.CopyTexture(textures[i], 0, mip, texArray, i, mip);
                }
                else
                {
                    Graphics.CopyTexture(textures[i], 0, 0, texArray, i, 0);
                }
            }

            texArray.Apply(false);
            Debug.Log("✅ Created GPU texture array with all mipmap levels copied.");
        }
        catch
        {
            // Fallback for unsupported formats: use RGBA32 + SetPixels
            Debug.LogWarning("⚠️ GPU copy failed, creating RGBA32 array instead (auto-generating mipmaps).");

            texArray = new Texture2DArray(width, height, textures.Count, TextureFormat.RGBA32, true, false);
            for (int i = 0; i < textures.Count; i++)
                texArray.SetPixels(textures[i].GetPixels(), i, 0);

            texArray.Apply(true);
        }

        string texPath = AssetDatabase.GetAssetPath(textures[^1]);
        string dir = Path.GetDirectoryName(texPath);
        string savePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, "TextureArray.asset"));

        AssetDatabase.CreateAsset(texArray, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ Texture2DArray saved at: {savePath}");
    }

    [MenuItem("Assets/Create/Texture2DArray from Selection (Smart)", true)]
    static bool Validate()
    {
        foreach (var obj in Selection.objects)
            if (obj is Texture2D) return true;
        return false;
    }
}
