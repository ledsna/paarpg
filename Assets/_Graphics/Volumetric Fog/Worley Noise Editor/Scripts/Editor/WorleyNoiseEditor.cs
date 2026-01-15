using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace WorleyNoise.Editor
{
    public class WorleyNoiseEditor
    {
        public enum CloudNoiseType
        {
            Shape,
            Detail
        }

        public enum TextureChannel
        {
            R,
            G,
            B,
            A
        }

        private RenderTexture shapeRT;
        private RenderTexture detailRT;

        private ComputeBuffer pointsABuffer;
        private ComputeBuffer pointsBBuffer;
        private ComputeBuffer pointsCBuffer;
        private ComputeBuffer minMaxBuffer;

        private const string shapeNoiseName = "ShapeNoise";
        private const string detailNoiseName = "DetailNoise";

        private Vector3[] pointsA;
        private Vector3[] pointsB;
        private Vector3[] pointsC;
        private readonly int[] minMax = { int.MaxValue, 0 };

        // Outer references
        private ComputeShader utilsShader;
        private ComputeShader worleyShader;
        private Vector4 channelMask;
        private int shapeResolution;
        private int detailResolution;
        private CloudNoiseType activeTextureType;
        private FilterMode filterMode;

        public RenderTexture targetRT => activeTextureType == CloudNoiseType.Shape ? shapeRT : detailRT;

        public void SetUpReferences(ComputeShader utilsShader, ComputeShader worleyShader,
            Vector4 channelMask, int shapeResolution, int detailResolution, CloudNoiseType activeTextureType,
            FilterMode filterMode)
        {
            this.utilsShader = utilsShader;
            this.worleyShader = worleyShader;
            this.channelMask = channelMask;
            this.shapeResolution = shapeResolution;
            this.detailResolution = detailResolution;
            this.activeTextureType = activeTextureType;
            this.filterMode = filterMode;
        }

        public void Redraw(WorleyNoiseSettings settings)
        {
            CreateRenderTexture(ref shapeRT, shapeResolution, shapeNoiseName);
            CreateRenderTexture(ref detailRT, detailResolution, detailNoiseName);

            if (settings == null)
            {
                Debug.LogWarning("No settings set");
                return;
            }

            var activeTextureResolution = targetRT.width;
            if (activeTextureResolution <= 0 || activeTextureResolution % 8 != 0)
            {
                Debug.LogWarning($"Incorrect resolution: {activeTextureResolution}");
                return;
            }

            var threadGroups = activeTextureResolution / 8;

            DispatchWorley(threadGroups, settings);
            DispatchNormalize(threadGroups);

            ReleaseBuffers();
        }

        private void DispatchWorley(int threadGroups, WorleyNoiseSettings settings)
        {
            var kernelWorley = worleyShader.FindKernel("CSWorley");

            UpdateWorleyParams(settings);

            worleyShader.Dispatch(kernelWorley, threadGroups, threadGroups, threadGroups);
        }

        private void UpdateWorleyParams(WorleyNoiseSettings settings)
        {
            worleyShader.SetInt("gridSizeA", settings.gridSizeA);
            worleyShader.SetInt("gridSizeB", settings.gridSizeB);
            worleyShader.SetInt("gridSizeC", settings.gridSizeC);
            worleyShader.SetFloat("persistence", settings.persistence);
            worleyShader.SetInt("tile", settings.tile);
            worleyShader.SetBool("invertNoise", settings.invert);

            worleyShader.SetVector("channelMask", channelMask);
            worleyShader.SetInt("resolution", targetRT.width);

            pointsABuffer = CreateAndSetBuffer(pointsA, sizeof(float) * 3, "pointsA", worleyShader);
            pointsBBuffer = CreateAndSetBuffer(pointsB, sizeof(float) * 3, "pointsB", worleyShader);
            pointsCBuffer = CreateAndSetBuffer(pointsC, sizeof(float) * 3, "pointsC", worleyShader);
            minMaxBuffer = CreateAndSetBuffer(minMax, sizeof(int), "minMax", worleyShader);
            worleyShader.SetTexture(0, "Result", targetRT);
        }

        private void DispatchNormalize(int threadGroups)
        {
            var kernelNormalize = worleyShader.FindKernel("CSNormalize");

            worleyShader.SetVector("channelMask", channelMask);
            worleyShader.SetBuffer(kernelNormalize, "minMax", minMaxBuffer);
            worleyShader.SetTexture(kernelNormalize, "Result", targetRT);

            worleyShader.Dispatch(kernelNormalize, threadGroups, threadGroups, threadGroups);
        }

        private void CreateRenderTexture(ref RenderTexture texture, int resolution, string name)
        {
            var format = GraphicsFormat.R16G16B16A16_UNorm;
            var createTexture =
                texture == null || !texture.IsCreated() ||
                texture.width != resolution || texture.height != resolution ||
                texture.volumeDepth != resolution || texture.graphicsFormat != format;

            if (createTexture)
            {
                if (texture != null)
                    texture.Release();

                texture = new RenderTexture(resolution, resolution, 0);
                texture.graphicsFormat = format;
                texture.volumeDepth = resolution;
                texture.enableRandomWrite = true;
                texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                texture.name = name;

                texture.Create();
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = filterMode;
        }

        private void ReleaseBuffers()
        {
            pointsABuffer?.Release();
            pointsBBuffer?.Release();
            pointsCBuffer?.Release();
            minMaxBuffer?.Release();
        }


        public void Release()
        {
            // TODO: use IDisposable instead
            ReleaseBuffers();

            shapeRT?.Release();
            detailRT?.Release();
        }

        private ComputeBuffer CreateAndSetBuffer(System.Array data, int stride, string bufferName, ComputeShader shader,
            int kernel = 0)
        {
            var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
            buffer.SetData(data);
            shader.SetBuffer(kernel, bufferName, buffer);
            return buffer;
        }

        public void Clear()
        {
            var kernel = this.utilsShader.FindKernel("CSClear");

            var saveMask = Vector4.one - channelMask;
            utilsShader.SetVector("channelMask", saveMask);
            utilsShader.SetTexture(kernel, "renderTex", targetRT);

            var threadGroups = targetRT.width / 8;
            utilsShader.Dispatch(kernel, threadGroups, threadGroups, threadGroups);
        }

        #region Generate Points

        public void Generate(WorleyNoiseSettings settings)
        {
            var rnd = new System.Random(settings.seed);
            pointsA = GeneratePoints(rnd, settings.gridSizeA);
            pointsB = GeneratePoints(rnd, settings.gridSizeB);
            pointsC = GeneratePoints(rnd, settings.gridSizeC);
        }

        private Vector3[] GeneratePoints(System.Random rnd, int gridSize)
        {
            int G = gridSize, GW = G + 2;
            var step = 1f / G;
            var temp = new Vector3[G * G * G];
            var points = new Vector3[GW * GW * GW];

            // 1) interior cells [1..G]×[1..G]×[1..G]
            for (int i = 0; i < temp.Length; i++)
            {
                var randomX = (float)rnd.NextDouble();
                var randomY = (float)rnd.NextDouble();
                var randomZ = (float)rnd.NextDouble();
                temp[i] = new Vector3(randomX, randomY, randomZ) * step;
            }

            // 2) wrap lookup (0 → G, G+1 → 1)
            var wrap = new int[GW];
            wrap[0] = G;
            wrap[GW - 1] = 1;
            for (int i = 1; i < GW - 1; i++) wrap[i] = i;

            // 3) tile everything (including interior) into UV space
            for (int z = 0; z < GW; z++)
            for (int y = 0; y < GW; y++)
            for (int x = 0; x < GW; x++)
            {
                int sx = wrap[x] - 1, sy = wrap[y] - 1, sz = wrap[z] - 1;

                var src = sx + G * (sy + G * sz);
                var dst = x + GW * (y + GW * z);

                var offset = new Vector3(x - 1, y - 1, z - 1) * step;
                points[dst] = temp[src] + offset;
            }

            return points;
        }

        #endregion
    }
}