using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grass.Core
{
    public static class GrassDataManager
    {
        public static bool TryLoadGrassData(GrassHolder grassHolder)
        {
            var grassData = new List<GrassData>();

            try
            {
                // Debug.Log($"[GrassDataManager] Attempting to load grass data...");

                var data = new GrassData();

                // Check if data source exists
                if (grassHolder.GrassDataSource == null)
                {
                    Debug.LogError("Error: Grass data source is null.");
                    return false;
                }

                // Get binary data from GrassDataAsset
                byte[] binaryData = grassHolder.GrassDataSource.Data;
                if (binaryData == null || binaryData.Length == 0)
                {
                    Debug.LogError("Error: Grass data source has no data.");
                    return false;
                }

                // Debug.Log($"[GrassDataManager] Loading {binaryData.Length} bytes of grass data...");

                using (Stream dataStream = new MemoryStream(binaryData))
                using (BinaryReader binaryReader = new BinaryReader(dataStream))
                {
                    // Old format: 3 floats (pos) + 3 floats (normal) + 2 floats (UV) = 32 bytes
                    // New format: 32 bytes + 4 bytes (int materialIndex) = 36 bytes
                    const int newRecordSize = 36;

                    while (dataStream.Position < dataStream.Length)
                    {
                        long bytesRemaining = dataStream.Length - dataStream.Position;

                        data.position = ReadVector3(binaryReader);
                        data.normal = ReadVector3(binaryReader);
                        data.lightmapUV = ReadVector2(binaryReader);

                        // Read materialIndex if this is new format (has 4 more bytes)
                        // Check if we have at least 4 bytes left OR if we're at a new-format boundary
                        if (bytesRemaining >= newRecordSize)
                        {
                            // New format with materialIndex
                            data.materialIndex = binaryReader.ReadInt32();
                        }
                        else
                        {
                            // Old format without materialIndex
                            data.materialIndex = 0;
                        }

                        grassData.Add(data);
                    }
                }

                grassHolder.grassData.Clear();
                grassHolder.grassData = grassData;

                // Reset cleared flag since grass was successfully loaded
                grassHolder.SetGrassClearedFlag(false);

                // Debug.Log($"[GrassDataManager] Successfully loaded {grassData.Count} grass instances");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load grass data: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
#if UNITY_EDITOR
        public static bool TrySaveGrassData(GrassHolder grassHolder)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(grassHolder.GrassDataSource);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogError("Error: Grass data source path is invalid or missing.");
                    return false;
                }

                // Write to the .grassdata file
                using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    foreach (var data in grassHolder.grassData)
                    {
                        SaveVector3(data.position, binaryWriter);
                        SaveVector3(data.normal, binaryWriter);
                        SaveVector2(data.lightmapUV, binaryWriter);
                        binaryWriter.Write(data.materialIndex); // Save material index
                    }
                }

                // Force Unity to reimport the asset so GrassDataAsset gets updated
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save grass data: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool TryClearGrassData(GrassHolder grassHolder)
        {
            try
            {
                // First, properly release all resources and disable
                grassHolder.OnDisable();

                // Clear the in-memory grass data ONLY
                // DO NOT touch the saved file - user might want to reload it later
                grassHolder.grassData.Clear();
                
                // Set flag to prevent auto-loading after clear
                grassHolder.SetGrassClearedFlag(true);
                
                // Don't call OnEnable() - let Unity handle re-enabling naturally
                // Debug.Log("Grass data cleared from memory. Saved file is preserved - use 'Load Positions' to restore.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to clear grass data: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }


        public static void CreateGrassDataAsset(string folderPath, GrassHolder grassHolder = null)
        {
            string baseName = "New Grass Data";

            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            int index = 0;
            string filePath;
            string fileName;
            do
            {
                fileName = index == 0 ? $"{baseName}.grassdata" : $"{baseName}({index}).grassdata";
                filePath = Path.Combine(folderPath, fileName);
                index++;
            } while (File.Exists(filePath));

            // Create empty file
            using (File.Create(filePath))
            {
            }

            // Refresh Unity's asset database to trigger the importer
            AssetDatabase.Refresh();

            // Load the new GrassDataAsset
            if (grassHolder != null)
            {
                grassHolder.GrassDataSource = AssetDatabase.LoadAssetAtPath<GrassDataAsset>(filePath);
                EditorUtility.SetDirty(grassHolder);
            }
        }

        private static void SaveVector3(Vector3 vector, BinaryWriter writer)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
            writer.Write(vector.z);
        }

        private static void SaveVector2(Vector2 vector, BinaryWriter writer)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
        }
#endif
        private static Vector2 ReadVector2(BinaryReader binaryReader)
        {
            Vector2 res;
            res.x = binaryReader.ReadSingle();
            res.y = binaryReader.ReadSingle();
            return res;
        }

        private static Vector3 ReadVector3(BinaryReader binaryReader)
        {
            Vector3 res;
            res.x = binaryReader.ReadSingle();
            res.y = binaryReader.ReadSingle();
            res.z = binaryReader.ReadSingle();
            return res;
        }
    }
}