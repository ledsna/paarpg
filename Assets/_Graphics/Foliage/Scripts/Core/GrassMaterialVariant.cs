using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Grass.Core
{
    [Serializable]
    public class TextureGroup
    {
        public Material material;

        [Range(0.001f, 10f)]
        public float weight = 1f;

        [Tooltip("Enable this to use the texture's own colors (for flowers). Disabling uses mesh color tinting (for grass).")]
        public bool useTextureColor = false;

        public TextureGroup(float w = 1f, bool isFlower = false)
        {
            weight = w;
            useTextureColor = isFlower;
        }

        // Constructor for flower groups with much lower default weight
        public static TextureGroup CreateFlowerGroup()
        {
            return new TextureGroup(0.01f, true); // Much rarer by default, uses texture color
        }

        public bool IsValid => material != null;
    }

    [Serializable]
    public class GrassMaterialSystem
    {
        public List<TextureGroup> grassGroups = new List<TextureGroup>();
        public List<TextureGroup> flowerGroups = new List<TextureGroup>();

        [Header("Clustering")]
        [Range(0.01f, 1.0f)]
        public float clusterScale = 0.1f;

        [Range(0.01f, 1.0f)]
        public float flowerClusterScale = 0.05f;

        [Header("Grass Normal Variation")]
        [Range(0.0f, 1.0f)]
        public float grassNormalNudgeProbability = 0.3f;


        // Cache flower ratio to avoid recalculating every grass instance
        [NonSerialized]
        private float _cachedFlowerRatio = -1f;
        [NonSerialized]
        private int _cachedFlowerRatioHash;

        // Get total count of all material groups
        public int TotalMaterialCount
        {
            get
            {
                int count = 0;
                foreach (var group in grassGroups)
                    if (group.IsValid) count++;
                foreach (var group in flowerGroups)
                    if (group.IsValid) count++;
                return count;
            }
        }

        // Get all materials from groups and configure their keywords
        public Material[] GetAllMaterials()
        {
            var materials = new List<Material>();

            // Collect grass materials and configure keywords
            foreach (var group in grassGroups)
            {
                if (group.IsValid)
                {
                    materials.Add(group.material);
                    // Configure USE_TEXTURE_COLOR keyword based on group setting
                    if (group.useTextureColor)
                        group.material.EnableKeyword("_USE_TEXTURE_COLOR");
                    else
                        group.material.DisableKeyword("_USE_TEXTURE_COLOR");
                }
            }

            // Collect flower materials and configure keywords
            foreach (var group in flowerGroups)
            {
                if (group.IsValid)
                {
                    materials.Add(group.material);
                    // Configure USE_TEXTURE_COLOR keyword based on group setting
                    if (group.useTextureColor)
                        group.material.EnableKeyword("_USE_TEXTURE_COLOR");
                    else
                        group.material.DisableKeyword("_USE_TEXTURE_COLOR");
                }
            }

            return materials.ToArray();
        }
        
        // Invalidate caches when materials change
        public void InvalidateCache()
        {
            _groupCacheValid = false;
            _cachedFlowerRatio = -1f;
        }

        // Cached data for SelectMaterialIndex to avoid allocations
        [NonSerialized] private List<(int index, float cumulativeWeight)> _cachedGrassGroups;
        [NonSerialized] private List<(int index, float cumulativeWeight)> _cachedFlowerGroups;
        [NonSerialized] private float _cachedGrassTotalWeight;
        [NonSerialized] private float _cachedFlowerTotalWeight;
        [NonSerialized] private bool _groupCacheValid = false;

        private void BuildGroupCache()
        {
            _cachedGrassGroups = new List<(int index, float cumulativeWeight)>();
            _cachedFlowerGroups = new List<(int index, float cumulativeWeight)>();

            float grassWeight = 0f;
            for (int i = 0; i < grassGroups.Count; i++)
            {
                if (grassGroups[i].IsValid)
                {
                    grassWeight += grassGroups[i].weight;
                    _cachedGrassGroups.Add((i, grassWeight));
                }
            }
            _cachedGrassTotalWeight = grassWeight;

            float flowerWeight = 0f;
            for (int i = 0; i < flowerGroups.Count; i++)
            {
                if (flowerGroups[i].IsValid)
                {
                    flowerWeight += flowerGroups[i].weight;
                    _cachedFlowerGroups.Add((i, flowerWeight));
                }
            }
            _cachedFlowerTotalWeight = flowerWeight;

            _groupCacheValid = true;
        }

        // Select material index based on world position (noise-based for clustering)
        // OPTIMIZED: Caches group data, flower ratio, avoids allocations
        public int SelectMaterialIndex(Vector3 worldPosition)
        {
            if (TotalMaterialCount == 0) return 0;

            // Rebuild cache if needed
            if (!_groupCacheValid)
                BuildGroupCache();

            // Determine if this position should be a flower
            bool isFlower = false;
            int selectedFlowerGroupIndex = 0;

            if (_cachedFlowerGroups.Count > 0)
            {
                float flowerRatio = GetFlowerRatioCached();

                // Each flower group gets its own clump pattern
                float bestGroupScore = float.MinValue;

                for (int i = 0; i < _cachedFlowerGroups.Count; i++)
                {
                    var cachedGroup = _cachedFlowerGroups[i];
                    var group = flowerGroups[cachedGroup.index];

                    // Each group gets its own noise with unique offset to create separate clumps
                    float groupNoise = SimplexNoise2D(
                        worldPosition.x * flowerClusterScale + i * 1000f,
                        worldPosition.z * flowerClusterScale + i * 1000f);

                    // Map noise from [0,1] to create distinct clumps
                    // Use threshold to create small clumps instead of one big region
                    // Higher noise value = in a clump, lower = not in a clump
                    float clumpThreshold = 0.6f; // Only high noise values create clumps
                    float clumpStrength = Mathf.Max(0, groupNoise - clumpThreshold) / (1.0f - clumpThreshold);

                    // Score based on being in a clump and group weight
                    float groupScore = clumpStrength * group.weight;

                    if (groupScore > bestGroupScore)
                    {
                        bestGroupScore = groupScore;
                        selectedFlowerGroupIndex = cachedGroup.index;
                    }
                }

                // Check if we should spawn a flower based on:
                // 1. Being in a clump (bestGroupScore > 0)
                // 2. Meeting the overall flower ratio threshold
                float randomValue = GetRandomHash(worldPosition);
                isFlower = bestGroupScore > 0 && randomValue < (flowerRatio * 2.5f); // Multiply to compensate for clump threshold
            }

            // If it's a flower, return the material index for the selected flower group
            if (isFlower)
            {
                // Calculate global material index for flower
                int flowerGlobalIndex = 0;
                // Count all grass groups first
                foreach (var group in grassGroups)
                    if (group.IsValid) flowerGlobalIndex++;
                // Then add flower groups up to the selected one
                for (int j = 0; j < selectedFlowerGroupIndex; j++)
                    if (flowerGroups[j].IsValid) flowerGlobalIndex++;
                return flowerGlobalIndex;
            }

            // Not a flower, select grass

            // Select pre-cached group data
            var validGroups = isFlower ? _cachedFlowerGroups : _cachedGrassGroups;
            float totalWeight = isFlower ? _cachedFlowerTotalWeight : _cachedGrassTotalWeight;
            
            if (validGroups.Count == 0)
            {
                // Fallback to the other type
                isFlower = !isFlower;
                validGroups = isFlower ? _cachedFlowerGroups : _cachedGrassGroups;
                totalWeight = isFlower ? _cachedFlowerTotalWeight : _cachedGrassTotalWeight;
                if (validGroups.Count == 0) return 0;
            }

            // Use noise-based group selection with proper weight respect
            float noiseValue = SimplexNoise2D(worldPosition.x * clusterScale, worldPosition.z * clusterScale);
            
            // For flower groups, add group-specific offset to create separate clumps
            if (isFlower && _cachedFlowerGroups.Count > 1)
            {
                // Add a position-based offset to create different clump patterns per group
                // This creates separate clumps while still respecting group weights
                float groupOffset = Mathf.Sin(worldPosition.x * 0.1f + worldPosition.z * 0.1f) * 1000f;
                noiseValue = SimplexNoise2D(
                    worldPosition.x * clusterScale + groupOffset, 
                    worldPosition.z * clusterScale + groupOffset);
            }
            
            // Select group based on noise (creates clustering) while respecting weights
            float selection = Mathf.Clamp01(noiseValue) * totalWeight;
            int selectedGroupIndex = validGroups[validGroups.Count - 1].index; // Default to last
            
            for (int i = 0; i < validGroups.Count; i++)
            {
                if (selection <= validGroups[i].cumulativeWeight)
                {
                    selectedGroupIndex = validGroups[i].index;
                    break;
                }
            }
            
            // Calculate global material index for selected grass group
            int globalIndex = 0;

            // Count valid groups up to the selected one
            for (int i = 0; i < selectedGroupIndex; i++)
            {
                if (grassGroups[i].IsValid)
                    globalIndex++;
            }

            return globalIndex;
        }
        
        // Fast random hash function for scattered flower spawning
        private float GetRandomHash(Vector3 worldPosition)
        {
            uint hash = (uint)Mathf.Abs(worldPosition.x * 73856093f) ^ (uint)Mathf.Abs(worldPosition.z * 19349663f);
            return (hash % 10000) * 0.0001f; // Faster than division
        }
        
        // Simplified 2D noise (much faster than Perlin, good enough for clustering)
        private float SimplexNoise2D(float x, float y)
        {
            // Simple hash-based noise (no trig functions!)
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            
            // Smoothstep
            float u = fx * fx * (3.0f - 2.0f * fx);
            float v = fy * fy * (3.0f - 2.0f * fy);
            
            // Simple hash function (no trig!)
            float Hash2D(int x, int y)
            {
                int n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7fffffff) / 2147483647.0f;
            }
            
            // Corner values
            float a = Hash2D(ix, iy);
            float b = Hash2D(ix + 1, iy);
            float c = Hash2D(ix, iy + 1);
            float d = Hash2D(ix + 1, iy + 1);
            
            // Bilinear interpolation
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        
        // OLD: Kept for reference but not used (too slow)
        private float PerlinNoise2D_OLD(float x, float y)
        {
            // Integer coordinates
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            // Fractional part
            float fx = x - x0;
            float fy = y - y0;
            
            // Smoothstep for smoother interpolation
            float u = fx * fx * (3.0f - 2.0f * fx);
            float v = fy * fy * (3.0f - 2.0f * fy);
            
            // Get gradients at corners
            float g00 = DotGridGradient(x0, y0, x, y);
            float g10 = DotGridGradient(x1, y0, x, y);
            float g01 = DotGridGradient(x0, y1, x, y);
            float g11 = DotGridGradient(x1, y1, x, y);
            
            // Bilinear interpolation
            float x0y = Mathf.Lerp(g00, g10, u);
            float x1y = Mathf.Lerp(g01, g11, u);
            float result = Mathf.Lerp(x0y, x1y, v);
            
            // Map from [-1, 1] to [0, 1]
            return (result + 1.0f) * 0.5f;
        }
        
        // Dot product of gradient vector and distance vector
        private float DotGridGradient(int ix, int iy, float x, float y)
        {
            // Get pseudo-random gradient vector
            float angle = Hash2D(ix, iy) * 6.28318530718f; // 0 to 2*PI
            float gx = Mathf.Cos(angle);
            float gy = Mathf.Sin(angle);
            
            // Distance vector
            float dx = x - ix;
            float dy = y - iy;
            
            // Dot product
            return dx * gx + dy * gy;
        }
        
        // 2D hash function for gradient generation
        private float Hash2D(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            n = n ^ (n >> 16);
            return (n & 0x7fffffff) / 2147483647.0f; // Normalize to [0, 1]
        }

        // OPTIMIZED: Cached version to avoid recalculating for every grass instance
        private float GetFlowerRatioCached()
        {
            // Calculate hash of current configuration
            int currentHash = grassGroups.Count * 73 + flowerGroups.Count * 37;
            foreach (var g in grassGroups)
                currentHash ^= (int)(g.weight * 1000);
            foreach (var g in flowerGroups)
                currentHash ^= (int)(g.weight * 1000);
            
            // Return cached value if still valid
            if (_cachedFlowerRatio >= 0 && _cachedFlowerRatioHash == currentHash)
                return _cachedFlowerRatio;
            
            // Recalculate and cache
            _cachedFlowerRatio = CalculateFlowerRatio();
            _cachedFlowerRatioHash = currentHash;
            return _cachedFlowerRatio;
        }

        private float CalculateFlowerRatio()
        {
            if (flowerGroups.Count == 0 || flowerGroups.All(g => !g.IsValid)) return 0f;
            if (grassGroups.Count == 0 || grassGroups.All(g => !g.IsValid)) return 1f;

            float grassTotal = 0f;
            float flowerTotal = 0f;

            foreach (var group in grassGroups)
                if (group.IsValid)
                    grassTotal += group.weight;

            foreach (var group in flowerGroups)
                if (group.IsValid)
                    flowerTotal += group.weight;

            return flowerTotal / (grassTotal + flowerTotal);
        }

        // Debug method to check flower group distribution
        public void DebugFlowerGroups()
        {
            // Debug.Log($"=== Flower Group Debug ===");
            // Debug.Log($"Total flower groups: {flowerGroups.Count}");
            // for (int i = 0; i < flowerGroups.Count; i++)
            // {
            //     var group = flowerGroups[i];
            //     Debug.Log($"  Group {i}: Weight={group.weight}, Textures={group.TextureCount}");
            // }
            //
            // float flowerRatio = GetFlowerRatioCached();
            // Debug.Log($"Flower ratio: {flowerRatio:P2}");
            //
            // if (_cachedFlowerGroups != null)
            // {
            //     Debug.Log($"Cached flower groups: {_cachedFlowerGroups.Count}");
            //     foreach (var cached in _cachedFlowerGroups)
            //     {
            //         Debug.Log($"  Cached Group {cached.index}: Cumulative weight={cached.cumulativeWeight}");
            //     }
            // }
        }

        // Validation
        public bool IsValid()
        {
            return TotalMaterialCount > 0;
        }
    }
}

