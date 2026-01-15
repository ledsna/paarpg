using UnityEditor;
using UnityEngine;
using __Project.Shared.Extensions;

namespace WorleyNoise.Editor
{
    public class Saver3D
    {
        private readonly ComputeShader utilsShader;
        private readonly int sliceKernel;

        public Saver3D(ComputeShader utilsShader)
        {
            this.utilsShader = utilsShader;
            sliceKernel = utilsShader.FindKernel("CSSlice");
        }

        public void Save(RenderTexture volumeTexture, string savePath)
        {
            var resolution = volumeTexture.width;
            var slices = new Texture2D[resolution];

            utilsShader.SetInt("resolution", resolution);
            utilsShader.SetTexture(sliceKernel, "renderTex", volumeTexture);

            var threadGroupSize = 8;

            for (int layer = 0; layer < resolution; layer++)
            {
                var slice = new RenderTexture(resolution, resolution, 0);
                slice.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                slice.enableRandomWrite = true;
                slice.Create();

                utilsShader.SetTexture(sliceKernel, "slice", slice);
                utilsShader.SetInt("layer", layer);
                var numThreadGroups = Mathf.CeilToInt(resolution / (float)threadGroupSize);
                utilsShader.Dispatch(sliceKernel, numThreadGroups, numThreadGroups, 1);

                slices[layer] = slice.ToTexture2D();
            }

            var x = Tex3DFromTex2DArray(slices, resolution);
            AssetDatabase.CreateAsset(x, savePath);
        }

        public Texture3D Tex3DFromTex2DArray(Texture2D[] slices, int resolution)
        {
            var tex3D = new Texture3D(resolution, resolution, resolution, TextureFormat.ARGB32, false);
            tex3D.filterMode = FilterMode.Trilinear;
            var outputPixels = tex3D.GetPixels();

            for (int z = 0; z < resolution; z++)
            {
                // var c = slices[z].GetPixel(0, 0);
                var layerPixels = slices[z].GetPixels();
                for (int x = 0; x < resolution; x++)
                for (int y = 0; y < resolution; y++)
                    outputPixels[x + resolution * (y + z * resolution)] = layerPixels[x + y * resolution];
            }

            tex3D.SetPixels(outputPixels);
            tex3D.Apply();

            return tex3D;
        }
    }
}