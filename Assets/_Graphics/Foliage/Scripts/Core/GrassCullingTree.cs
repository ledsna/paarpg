using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grass.Core
{
    public class GrassCullingTree
    {
        public class ChunkInfo
        {
            public uint StartInstance { get; set; }
            public uint InstanceCount => (uint)GrassData.Count;

            public List<GrassData> GrassData = new List<GrassData>();
        }

        public Bounds bounds;
        public List<GrassCullingTree> children = new();
        public bool isDrawn = true;
        public List<ChunkInfo> Chunks = new();
        private GrassHolder grassHolder;
        private GrassCullingTree root;
        private ushort index;

        public IEnumerable<int> GetVisibleChunkIndices(Plane[] frustum)
        {
            isDrawn = false;
            if (GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                isDrawn = true;
                if (children.Count == 0)
                    yield return index;
                else
                {
                    foreach (var child in children)
                    foreach (var chunkIndex in child.GetVisibleChunkIndices(frustum))
                        yield return chunkIndex;
                }
            }
        }


        public GrassCullingTree(Bounds bounds, int depth, GrassHolder grassHolder, GrassCullingTree root = null)
        {
            children.Clear();
            this.bounds = bounds;
            this.grassHolder = grassHolder;
            this.root = root;
            if (depth > 0)
            {
                if (root == null)
                {
                    root = this;
                }

                var size = bounds.size;
                size /= 4.0f;
                var childSize = bounds.size / 2.0f;
                childSize.y *= 2;
                var center = bounds.center;

                childSize.y = bounds.size.y;
                Bounds topLeftSingle =
                    new Bounds(new Vector3(center.x - size.x, center.y, center.z - size.z), childSize);
                Bounds bottomRightSingle =
                    new Bounds(new Vector3(center.x + size.x, center.y, center.z + size.z), childSize);
                Bounds topRightSingle =
                    new Bounds(new Vector3(center.x - size.x, center.y, center.z + size.z), childSize);
                Bounds bottomLeftSingle =
                    new Bounds(new Vector3(center.x + size.x, center.y, center.z - size.z), childSize);

                children.Add(new GrassCullingTree(topLeftSingle, depth - 1, grassHolder, root));
                children.Add(new GrassCullingTree(topRightSingle, depth - 1, grassHolder, root));
                children.Add(new GrassCullingTree(bottomRightSingle, depth - 1, grassHolder, root));
                children.Add(new GrassCullingTree(bottomLeftSingle, depth - 1, grassHolder, root));
            }
            else
            {
                index = (ushort)this.root!.Chunks.Count;
                this.root.Chunks.Add(new ChunkInfo());
            }
        }

        public void SortGrassDataIntoChunks()
        {
            foreach (var grassData in grassHolder.grassData)
            {
                var chunkIndex = FindLeafIndex(grassData.position);
                Chunks[chunkIndex].GrassData.Add(grassData);
            }

            RecalculateBoundsHeight();
            grassHolder.grassData.Clear();
            var startInstance = 0u;
            foreach (var chunkInfo in Chunks)
            {
                chunkInfo.StartInstance = startInstance;
                startInstance += chunkInfo.InstanceCount;
                foreach (var grassData in chunkInfo.GrassData)
                {
                    grassHolder.grassData.Add(grassData);
                }
            }
        }

        public int FindLeafIndex(Vector3 point)
        {
            if (bounds.Contains(point))
                if (children.Count != 0)
                {
                    foreach (var child in children)
                        if (child.bounds.Contains(point))
                            return child.FindLeafIndex(point);
                }
                else
                {
                    return index;
                }

            throw new Exception($"Point {point} does not belong to any tree leaf!");
        }

        public void RecalculateBoundsHeight()
        {
            if (children.Count > 0)
            {
                foreach (var child in children)
                {
                    child.RecalculateBoundsHeight();
                }

                float highestY = float.NegativeInfinity, lowestY = float.PositiveInfinity;
                foreach (var child in children)
                {
                    highestY = Mathf.Max(highestY, child.bounds.size.y / 2 + child.bounds.center.y);
                    lowestY = Mathf.Min(lowestY, child.bounds.center.y - child.bounds.size.y / 2);
                }

                bounds.center = new Vector3(bounds.center.x, (highestY + lowestY) / 2, bounds.center.z);
                bounds.size = new Vector3(bounds.size.x, highestY - lowestY, bounds.size.z);
            }
            else
            {
                float highestY = float.NegativeInfinity, lowestY = float.PositiveInfinity;
                foreach (var grassData in root.Chunks[index].GrassData)
                {
                    var y = grassData.position.y;
                    highestY = Mathf.Max(highestY, y);
                    lowestY = Mathf.Min(lowestY, y);
                }

                bounds.center = new Vector3(bounds.center.x, (highestY + lowestY) / 2, bounds.center.z);
                bounds.size = new Vector3(bounds.size.x, highestY - lowestY + 0.5f, bounds.size.z);
            }
        }

        public void Release()
        {
            foreach (var child in children)
            {
                child.Release();
            }

            Chunks.Clear();
            children.Clear();
        }
    }
}