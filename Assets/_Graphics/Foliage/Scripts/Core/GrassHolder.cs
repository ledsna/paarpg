using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using __Project.Shared.Attributes;

namespace Grass.Core
{

    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 lightmapUV;
        public int materialIndex;

        public GrassData(Vector3 pos, Vector3 norm, Vector2 lmUV, int matIndex = 0)
        {
            position = pos;
            normal = norm;
            lightmapUV = lmUV;
            materialIndex = matIndex;
        }
    }

    [ExecuteAlways]
    public class GrassHolder : MonoBehaviour
    {
        private static readonly int SourcePositionGrass = Shader.PropertyToID("_SourcePositionGrass");
        [NonSerialized] public List<GrassData> grassData = new();
        [HideInInspector] public Material _rootMeshMaterial;

        // Lightmapping
        [HideInInspector] public int lightmapIndex;

        // Properties
        [SerializeField] private Mesh mesh;

        [Header("Material Variants")]
        [SerializeField]
        public GrassMaterialSystem materialSystem = new GrassMaterialSystem();

        [Header("Generation Settings")]
        [SerializeField, Range(0f, 1f)]
        public float normalLimit = 0.1f;

        [Header("Rendering")]
        [SerializeField]
        public uint renderingLayerMask = 1;

        [Header("Culling")]
        [SerializeField, Range(1, 6)] private int depthCullingTree = 3;
        [SerializeField] public bool UseOctreeCulling;
        [SerializeField] public bool UseGPUCulling = true; // GPU compute shader culling
        [SerializeField] private bool drawBounds;
        [SerializeField] private ComputeShader frustumCullingCompute;

        [FileAsset(".grassdata"), SerializeField]
        public GrassDataAsset GrassDataSource;

        [SerializeField, HideInInspector] private string lastAttachedGrassDataSourcePath;
        [SerializeField, HideInInspector] private bool lastValueUseOctreeCulling;
        [SerializeField, HideInInspector] private int lastDepthCullingTree;
        [SerializeField, HideInInspector] private bool grassWasCleared = false;

        // Material of the surface on which the grass is being instanced

        // Material-specific rendering data
        private class MaterialRenderData
        {
            public Material material;
            public List<GrassData> grassDataForMaterial;
            public ComputeBuffer buffer;
            public GraphicsBuffer commandBuffer;
            public GraphicsBuffer.IndirectDrawIndexedArgs[] commandBufferData;
            public RenderParams renderParams;
            public MaterialPropertyBlock materialPropertyBlock;
        }

        // Buffers And GPU Instance Components (for single material or fallback)
        private ComputeBuffer _sourcePositionGrass;
        private GraphicsBuffer _commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _bufferData;
        private RenderParams _renderParams;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Bounds grassBounds;

        // Multi-material rendering
        private List<MaterialRenderData> _materialRenderDataList = new List<MaterialRenderData>();
        private bool _useMultiMaterial = false;

        // Precomputed Rotation * Scale Matrix 
        private Matrix4x4 _rotationScaleMatrix;

        // Main Camera
        private Camera _mainCamera = null;

        // For no reason Camera.Main always zero. So made field for inspector to plug 

        // Stride For Grass Data Buffer
        private const int GrassDataStride = sizeof(float) * (3 + 3 + 2) + sizeof(int); // position(3) + normal(3) + lightmapUV(2) + materialIndex(1)

        // Initialized State
        private bool _initialized;

        // Grass Culling Tree
        // ------------------
        [NonSerialized] private GrassCullingTree cullingTree;
        Plane[] cameraFrustumPlanes = new Plane[6];
        float cameraOriginalFarPlane;
        Vector3 cachedCamPos;
        Quaternion cachedCamRot;

        // GPU Culling Buffers
        private ComputeBuffer _chunkDataBuffer;
        private ComputeBuffer _visibleChunksBuffer;
        private ComputeBuffer _visibleChunkCountBuffer;
        private int _frustumCullKernel;

        private int maxBufferSize = 2500000;
        // ------------------

        #region Setup and Rendering

        public void FastSetup()
        {
#if UNITY_EDITOR
            SceneView.duringSceneGui += OnScene;
            if (!Application.isPlaying)
            {
                if (_view is not null)
                {
                    _mainCamera = _view.camera;
                }
            }
#endif
            if (Application.isPlaying)
            {
                _mainCamera = Camera.main;
            }

            if (_initialized)
                Release(false);

            if (grassData.Count == 0)
            {
                return;
            }

            InitBuffers();
        }

        private void InitBuffers()
        {
            grassBounds = GetGrassBound();
            _rotationScaleMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));

            // Material system is now required
            if (materialSystem == null || !materialSystem.IsValid())
            {
                Debug.LogError($"GrassHolder on {gameObject.name}: Material system is not configured! Please set up material variants in the Material Variants section.", this);
                return;
            }

            _useMultiMaterial = true;
            InitMultiMaterialBuffers();
            _initialized = true;
        }

        private void InitMultiMaterialBuffers()
        {
            _materialRenderDataList.Clear();

            Material[] materials = materialSystem.GetAllMaterials();

            if (materials.Length == 0)
            {
                Debug.LogError($"GrassHolder on {gameObject.name}: Material system is valid but returned no materials!", this);
                return;
            }

            // Debug.Log($"GrassHolder on {gameObject.name}: Setting up {materials.Length} materials for {grassData.Count} grass instances");

            // OPTIMIZED: Group grass data by material index in ONE pass (O(n) instead of O(n*m))
            var grassByMaterial = new Dictionary<int, List<GrassData>>();

            // Single pass through all grass data
            foreach (var grass in grassData)
            {
                if (!grassByMaterial.ContainsKey(grass.materialIndex))
                    grassByMaterial[grass.materialIndex] = new List<GrassData>();
                grassByMaterial[grass.materialIndex].Add(grass);
            }

            // Create buffers for each material that has instances
            for (int matIndex = 0; matIndex < materials.Length; matIndex++)
            {
                if (!grassByMaterial.TryGetValue(matIndex, out var grassForMaterial) || grassForMaterial.Count == 0)
                {
                    // Debug.Log($"  Material {matIndex} ({materials[matIndex].name}): 0 instances (skipped)");
                    continue;
                }

                // Debug.Log($"  Material {matIndex} ({materials[matIndex].name}): {grassForMaterial.Count} instances");

                var renderData = new MaterialRenderData
                {
                    material = materials[matIndex],
                    grassDataForMaterial = grassForMaterial
                };

                // Setup buffer for this material
                renderData.buffer = new ComputeBuffer(
                    Mathf.Max(1, grassForMaterial.Count),
                    GrassDataStride,
                    ComputeBufferType.Structured,
                    ComputeBufferMode.Immutable);
                renderData.buffer.SetData(grassForMaterial);

                // Setup material property block
                renderData.materialPropertyBlock = new MaterialPropertyBlock();
                renderData.materialPropertyBlock.SetBuffer(SourcePositionGrass, renderData.buffer);

                // Copy only specific properties from root material
                CopySelectiveProperties(renderData.material, _rootMeshMaterial);

                if (_rootMeshMaterial != null)
                {
                    if (_rootMeshMaterial.IsKeywordEnabled("_REFLECTION_PROBE_BLENDING"))
                        renderData.material.EnableKeyword("_REFLECTION_PROBE_BLENDING");
                    else
                        renderData.material.DisableKeyword("_REFLECTION_PROBE_BLENDING");

                    if (_rootMeshMaterial.IsKeywordEnabled("_REFLECTION_PROBE_BOX_PROJECTION"))
                        renderData.material.EnableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");
                    else
                        renderData.material.DisableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");
                }

                renderData.material.EnableKeyword("_ALPHATEST_ON");

                if (lightmapIndex >= 0 && LightmapSettings.lightmaps.Length > 0)
                {
                    renderData.material.EnableKeyword("LIGHTMAP_ON");
                    if (LightmapSettings.lightmapsMode == LightmapsMode.CombinedDirectional)
                        renderData.material.EnableKeyword("DIRLIGHTMAP_COMBINED");
                    else
                        renderData.material.DisableKeyword("DIRLIGHTMAP_COMBINED");
                    renderData.material.EnableKeyword("MAIN_LIGHT_CALCULATE_SHADOWS");

                    // CRITICAL: Bind lightmap and shadowmask textures for instanced rendering
                    var lightmapData = LightmapSettings.lightmaps[lightmapIndex];
                    if (lightmapData.lightmapColor != null)
                    {
                        // Set lightmap textures via MaterialPropertyBlock for instanced rendering
                        renderData.materialPropertyBlock.SetTexture("unity_Lightmap", lightmapData.lightmapColor);
                        if (lightmapData.lightmapDir != null)
                            renderData.materialPropertyBlock.SetTexture("unity_LightmapInd", lightmapData.lightmapDir);

                        // Set lightmap scale/offset (critical for correct UV sampling)
                        renderData.materialPropertyBlock.SetVector("unity_LightmapST", new Vector4(1, 1, 0, 0));

                        // THIS IS THE KEY ONE FOR SHADOWMASK!
                        if (lightmapData.shadowMask != null)
                        {
                            renderData.materialPropertyBlock.SetTexture("unity_ShadowMask", lightmapData.shadowMask);

                            // Enable shadowmask keywords based on quality settings
                            renderData.material.EnableKeyword("SHADOWS_SHADOWMASK");

                            // LIGHTMAP_SHADOW_MIXING: Distance Shadowmask mode (blends realtime shadows at distance)
                            // SHADOWS_SHADOWMASK: Pure shadowmask mode (fully baked)
                            if (QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask)
                                renderData.material.EnableKeyword("LIGHTMAP_SHADOW_MIXING");
                            else
                                renderData.material.DisableKeyword("LIGHTMAP_SHADOW_MIXING");
                        }
                    }
                }
                else
                {
                    renderData.material.DisableKeyword("LIGHTMAP_ON");
                    renderData.material.DisableKeyword("DIRLIGHTMAP_COMBINED");
                }


                // Setup render params
                renderData.renderParams = new RenderParams(renderData.material)
                {
                    layer = gameObject.layer,
                    renderingLayerMask = renderingLayerMask,
                    worldBounds = grassBounds,
                    matProps = renderData.materialPropertyBlock,
                    reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes
                };

                // Setup command buffer (do this once, not every frame!)
                renderData.commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
                renderData.commandBufferData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                renderData.commandBufferData[0].indexCountPerInstance = 6;
                renderData.commandBufferData[0].instanceCount = (uint)grassForMaterial.Count;
                renderData.commandBuffer.SetData(renderData.commandBufferData);

                _materialRenderDataList.Add(renderData);
            }

            // Set global scale from first material
            if (materials.Length > 0)
                Shader.SetGlobalFloat("_Scale", materials[0].GetFloat("_Scale"));

            // Debug.Log($"GrassHolder: Successfully initialized {_materialRenderDataList.Count} material groups");
        }

        private void Setup()
        {
#if UNITY_EDITOR
            SceneView.duringSceneGui += OnScene;
            if (!Application.isPlaying)
            {
                if (_view is not null)
                {
                    _mainCamera = _view.camera;
                }
            }
#endif

            if (Application.isPlaying)
            {
                _mainCamera = Camera.main;
            }

            // Skip loading if no grass data source is assigned (e.g. newly created component)
            if (GrassDataSource == null)
                return;

            // Only load from file if grass data is empty AND grass was not explicitly cleared
            // This prevents auto-loading after grass was cleared by user
            if (grassData.Count == 0 && !grassWasCleared)
            {
                // Debug.Log("[GrassHolder] FastSetup: Attempting to load grass data from file...");
                if (!GrassDataManager.TryLoadGrassData(this) || grassData.Count == 0)
                {
                    // Debug.LogWarning("[GrassHolder] FastSetup: Failed to load grass data or no grass instances found");
                    return;
                }
            }
            else
            {
                // Debug.Log($"[GrassHolder] FastSetup: Skipping load (already have {grassData.Count} instances or was cleared)");
            }

            if (UseOctreeCulling)
            {
                CreateGrassCullingTree(depthCullingTree);
                cullingTree.SortGrassDataIntoChunks();

                // Initialize GPU culling if enabled
                if (UseGPUCulling && frustumCullingCompute != null)
                {
                    InitGPUCulling();
                }
            }

            InitBuffers();
        }

        [ExecuteAlways]
        private void Update()
        {
            if (!_initialized)
                return;

            UpdateMultiMaterial();
        }

        private void UpdateMultiMaterial()
        {
            if (_materialRenderDataList == null || _materialRenderDataList.Count == 0)
                return;

            // Render each material group with its own scale
            foreach (var renderData in _materialRenderDataList)
            {
                if (renderData.material == null || renderData.commandBuffer == null)
                    continue;

                // Get the scale for this specific material
                float materialScale = renderData.material.GetFloat("_Scale");

                // Update rotation matrix for this material's scale
                UpdateRotationScaleMatrix(materialScale);
                renderData.material.SetMatrix("m_RS", _rotationScaleMatrix);

                // Set flower properties globally for Setup() function to access
                if (renderData.material.HasProperty("_FlowerSizeMultiplier"))
                    Shader.SetGlobalFloat("_FlowerSizeMultiplier", renderData.material.GetFloat("_FlowerSizeMultiplier"));
                if (renderData.material.HasProperty("_FlowerSizeVariation"))
                    Shader.SetGlobalFloat("_FlowerSizeVariation", renderData.material.GetFloat("_FlowerSizeVariation"));

                Graphics.RenderMeshIndirect(renderData.renderParams, mesh, renderData.commandBuffer, 1);
            }
        }

        private void PrepareCommandBuffer()
        {
            if (_mainCamera == null)
                return;

            // if the camera didnt move, we dont need to change the culling;
            if (cachedCamRot == _mainCamera.transform.rotation && cachedCamPos == _mainCamera.transform.position &&
                Application.isPlaying)
            {
                return;
            }


            _commandBuffer?.Release();
            _commandBuffer = null;
            // Octree culling work only in build, but this behaviour can be changed
            if (Application.isPlaying)
            {
                if (UseOctreeCulling)
                {
                    // Use GPU culling if enabled
                    if (UseGPUCulling && frustumCullingCompute != null && _chunkDataBuffer != null)
                    {
                        PerformGPUCulling();

                        // cache camera position to skip culling when not moved
                        cachedCamPos = _mainCamera.transform.position;
                        cachedCamRot = _mainCamera.transform.rotation;
                        return;
                    }

                    // Fallback to CPU culling
                    var buffers = new List<GraphicsBuffer.IndirectDrawIndexedArgs>();
                    GeometryUtility.CalculateFrustumPlanes(_mainCamera, cameraFrustumPlanes);
                    foreach (var chunkIndex in cullingTree.GetVisibleChunkIndices(cameraFrustumPlanes))
                    {
                        var buffer = new GraphicsBuffer.IndirectDrawIndexedArgs();
                        buffer.instanceCount = cullingTree.Chunks[chunkIndex].InstanceCount;
                        buffer.startInstance = cullingTree.Chunks[chunkIndex].StartInstance;
                        buffer.indexCountPerInstance = 6;
                        buffers.Add(buffer);
                    }

                    if (buffers.Count == 0)
                        return;

                    _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, buffers.Count,
                        GraphicsBuffer.IndirectDrawIndexedArgs.size);
                    _commandBuffer.SetData(buffers.ToArray());
                    return;
                }
            }

            _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _bufferData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _bufferData[0].indexCountPerInstance = 6;
            _bufferData[0].instanceCount = (uint)grassData.Count;
            _commandBuffer.SetData(_bufferData);

            // cache camera position to skip culling when not moved
            cachedCamPos = _mainCamera.transform.position;
            cachedCamRot = _mainCamera.transform.rotation;
        }

        #endregion

        private void CreateGrassCullingTree(int depth = 3)
        {
            if (cullingTree != null)
            {
                cullingTree.Release();
            }

            // Init culling tree
            cullingTree =
                new GrassCullingTree(
                    GetGrassBound(),
                    depth, this
                );
        }

        private Bounds GetGrassBound(float extrude = 0.5f)
        {
            var mostLeftBottom = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mostRightTop = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var data in grassData)
            {
                var position = data.position;
                mostLeftBottom.x = Mathf.Min(mostLeftBottom.x, position.x);
                mostLeftBottom.y = Mathf.Min(mostLeftBottom.y, position.y);
                mostLeftBottom.z = Mathf.Min(mostLeftBottom.z, position.z);

                mostRightTop.x = Mathf.Max(mostRightTop.x, position.x);
                mostRightTop.y = Mathf.Max(mostRightTop.y, position.y);
                mostRightTop.z = Mathf.Max(mostRightTop.z, position.z);
            }

            return new Bounds((mostLeftBottom + mostRightTop) / 2,
                mostRightTop - mostLeftBottom + Vector3.one * extrude);
        }

        // GPU Culling struct to match compute shader
        private struct GPUChunkData
        {
            public Vector3 boundsCenter;
            public Vector3 boundsExtents;
            public uint startInstance;
            public uint instanceCount;
        }

        private struct GPUVisibleChunk
        {
            public uint chunkIndex;
            public uint startInstance;
            public uint instanceCount;
        }

        private void InitGPUCulling()
        {
            if (cullingTree == null || frustumCullingCompute == null)
                return;

            // Get kernel
            _frustumCullKernel = frustumCullingCompute.FindKernel("FrustumCull");

            int chunkCount = cullingTree.Chunks.Count;

            // Create chunk data buffer
            GPUChunkData[] chunkData = new GPUChunkData[chunkCount];

            // Flatten octree to linear array for GPU
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = cullingTree.Chunks[i];
                var bounds = GetChunkBounds(i);

                chunkData[i] = new GPUChunkData
                {
                    boundsCenter = bounds.center,
                    boundsExtents = bounds.extents,
                    startInstance = chunk.StartInstance,
                    instanceCount = chunk.InstanceCount
                };
            }

            // Create GPU buffers
            _chunkDataBuffer = new ComputeBuffer(chunkCount, sizeof(float) * 6 + sizeof(uint) * 2);
            _chunkDataBuffer.SetData(chunkData);

            _visibleChunksBuffer = new ComputeBuffer(chunkCount, sizeof(uint) * 3);
            _visibleChunkCountBuffer = new ComputeBuffer(1, sizeof(uint));

            // Bind buffers to compute shader
            frustumCullingCompute.SetBuffer(_frustumCullKernel, "_ChunkBuffer", _chunkDataBuffer);
            frustumCullingCompute.SetBuffer(_frustumCullKernel, "_VisibleChunks", _visibleChunksBuffer);
            frustumCullingCompute.SetBuffer(_frustumCullKernel, "_VisibleChunkCount", _visibleChunkCountBuffer);
        }

        private Bounds GetChunkBounds(int chunkIndex)
        {
            // Traverse octree to find the leaf node for this chunk
            return TraverseFindChunkBounds(cullingTree, chunkIndex);
        }

        private Bounds TraverseFindChunkBounds(GrassCullingTree node, int targetIndex)
        {
            if (node.children.Count == 0)
            {
                // Leaf node - check if it matches our target index
                // Note: We need to match against the chunk list position
                foreach (var chunk in node.Chunks)
                {
                    if (cullingTree.Chunks.IndexOf(chunk) == targetIndex)
                        return node.bounds;
                }
            }
            else
            {
                // Recurse into children
                foreach (var child in node.children)
                {
                    var result = TraverseFindChunkBounds(child, targetIndex);
                    if (result.size != Vector3.zero)
                        return result;
                }
            }

            return new Bounds(); // Not found
        }

        private void PerformGPUCulling()
        {
            if (_chunkDataBuffer == null || _mainCamera == null)
                return;

            // Reset visible count
            uint[] zeroCount = new uint[] { 0 };
            _visibleChunkCountBuffer.SetData(zeroCount);

            // Calculate frustum planes
            GeometryUtility.CalculateFrustumPlanes(_mainCamera, cameraFrustumPlanes);

            // Convert Unity Plane format to float4 for compute shader
            Vector4[] frustumPlanes = new Vector4[6];
            for (int i = 0; i < 6; i++)
            {
                frustumPlanes[i] = new Vector4(
                    cameraFrustumPlanes[i].normal.x,
                    cameraFrustumPlanes[i].normal.y,
                    cameraFrustumPlanes[i].normal.z,
                    cameraFrustumPlanes[i].distance
                );
            }

            frustumCullingCompute.SetVectorArray("_FrustumPlanes", frustumPlanes);

            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(cullingTree.Chunks.Count / 64f);
            frustumCullingCompute.Dispatch(_frustumCullKernel, threadGroups, 1, 1);

            // Read back visible chunks
            uint[] visibleCount = new uint[1];
            _visibleChunkCountBuffer.GetData(visibleCount);

            if (visibleCount[0] == 0)
                return;

            GPUVisibleChunk[] visibleChunks = new GPUVisibleChunk[visibleCount[0]];
            _visibleChunksBuffer.GetData(visibleChunks, 0, 0, (int)visibleCount[0]);

            // Build command buffer from visible chunks
            var buffers = new List<GraphicsBuffer.IndirectDrawIndexedArgs>((int)visibleCount[0]);
            for (int i = 0; i < visibleCount[0]; i++)
            {
                var visibleChunk = visibleChunks[i];
                var buffer = new GraphicsBuffer.IndirectDrawIndexedArgs();
                buffer.instanceCount = visibleChunk.instanceCount;
                buffer.startInstance = visibleChunk.startInstance;
                buffer.indexCountPerInstance = 6;
                buffers.Add(buffer);
            }

            _commandBuffer?.Release();
            _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, buffers.Count,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commandBuffer.SetData(buffers.ToArray());
        }

        #region F1Soda magic pls document

        public void Release(bool full = true)
        {
            try
            {
                _sourcePositionGrass?.Release();
            }
            catch { }

            try
            {
                _commandBuffer?.Release();
            }
            catch { }

            _commandBuffer = null;
            _materialPropertyBlock?.Clear();
            _bufferData = null;

            try
            {
                cullingTree?.Release();
            }
            catch { }
            cullingTree = null;

            // Release GPU culling buffers
            try
            {
                _chunkDataBuffer?.Release();
                _visibleChunksBuffer?.Release();
                _visibleChunkCountBuffer?.Release();
            }
            catch { }

            _chunkDataBuffer = null;
            _visibleChunksBuffer = null;
            _visibleChunkCountBuffer = null;

            // Release multi-material buffers
            if (_materialRenderDataList != null)
            {
                foreach (var renderData in _materialRenderDataList)
                {
                    try
                    {
                        renderData.buffer?.Release();
                    }
                    catch { }

                    try
                    {
                        renderData.commandBuffer?.Release();
                    }
                    catch { }

                    renderData.materialPropertyBlock?.Clear();
                }
                _materialRenderDataList.Clear();
            }

            // Clear material cache to prevent memory leaks
            try
            {
                materialSystem?.InvalidateCache();
            }
            catch { }

            if (full && grassData != null)
                grassData.Clear();
        }

        private void UpdateRotationScaleMatrix(float scale)
        {
            if (_mainCamera == null || _mainCamera.transform.rotation == cachedCamRot)
            {
                return;
            }

            _rotationScaleMatrix.SetColumn(0, _mainCamera.transform.right * scale);
            _rotationScaleMatrix.SetColumn(1, _mainCamera.transform.up * scale);
            _rotationScaleMatrix.SetColumn(2, _mainCamera.transform.forward * scale);
        }

        #endregion

        #region Event Functions

#if UNITY_EDITOR
        SceneView _view;

        void OnDestroy()
        {
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
            SceneView.duringSceneGui -= this.OnScene;
        }

        void OnScene(SceneView scene)
        {
            _view = scene;
            if (!Application.isPlaying)
            {
                if (_view.camera != null)
                {
                    _mainCamera = _view.camera;
                }
            }
            else
            {
                _mainCamera = Camera.main;
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (_view != null)
                {
                    _mainCamera = _view.camera;
                }
            }
            else
            {
                _mainCamera = Camera.main;
            }

            if (lastAttachedGrassDataSourcePath != AssetDatabase.GetAssetPath(GrassDataSource))
            {
                OnEnable();
                lastAttachedGrassDataSourcePath = AssetDatabase.GetAssetPath(GrassDataSource);
            }


            if (UseOctreeCulling != lastValueUseOctreeCulling)
            {
                if (UseOctreeCulling)
                {
                    CreateGrassCullingTree(depthCullingTree);
                }
                else
                {
                    cullingTree?.Release();
                }

                lastValueUseOctreeCulling = UseOctreeCulling;
            }

            if (depthCullingTree != lastDepthCullingTree)
            {
                CreateGrassCullingTree(depthCullingTree);

                lastDepthCullingTree = depthCullingTree;
            }
        }
#endif
        public void OnEnable()
        {
            // Safety check: Don't initialize during script recompilation or if no data source
            if (GrassDataSource == null)
            {
                Debug.LogWarning("[GrassHolder] OnEnable aborted: GrassDataSource is null");
                return;
            }

            // Reset cleared flag when entering play mode - always load grass at runtime
            if (Application.isPlaying)
            {
                // Debug.Log($"[GrassHolder] Resetting grassWasCleared flag (was: {grassWasCleared})");
                grassWasCleared = false;
            }

            if (_initialized)
            {
                OnDisable();
            }

            try
            {
                Setup();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize GrassHolder on {gameObject.name}: {e.Message}", this);
                _initialized = false;
            }
        }

        /// <summary>
        /// Reinitialize the grass holder after loading data. Call this after manually loading grass data.
        /// </summary>
        public void Reinitialize()
        {
            try
            {
                OnDisable();
                OnEnable();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to reinitialize GrassHolder on {gameObject.name}: {e.Message}", this);
            }
        }

        /// <summary>
        /// Set the grass cleared flag to control auto-loading behavior
        /// </summary>
        public void SetGrassClearedFlag(bool cleared)
        {
            grassWasCleared = cleared;
        }

        /// <summary>
        /// Copy only specific properties from source to destination material:
        /// - Cel Shading Settings
        /// - Surface Options
        /// - Surface Inputs
        /// </summary>
        private void CopySelectiveProperties(Material dest, Material source)
        {
            if (source == null || dest == null) return;

            // Cel Shading Settings
            CopyPropertyIfExists(dest, source, "_DiffuseSpecularCelShader");
            CopyPropertyIfExists(dest, source, "_DiffuseSteps");
            CopyPropertyIfExists(dest, source, "_FresnelSteps");
            CopyPropertyIfExists(dest, source, "_SpecularStep");
            CopyPropertyIfExists(dest, source, "_DistanceSteps");
            CopyPropertyIfExists(dest, source, "_ShadowSteps");
            CopyPropertyIfExists(dest, source, "_ReflectionSteps");

            // Surface Options
            CopyPropertyIfExists(dest, source, "_Surface");
            CopyPropertyIfExists(dest, source, "_Blend");
            CopyPropertyIfExists(dest, source, "_Cull");
            CopyPropertyIfExists(dest, source, "_Cutoff");
            // Don't copy _AlphaClip - billboards always need alpha clipping
            CopyPropertyIfExists(dest, source, "_SrcBlend");
            CopyPropertyIfExists(dest, source, "_DstBlend");
            CopyPropertyIfExists(dest, source, "_SrcBlendAlpha");
            CopyPropertyIfExists(dest, source, "_DstBlendAlpha");
            CopyPropertyIfExists(dest, source, "_ZWrite");
            CopyPropertyIfExists(dest, source, "_BlendModePreserveSpecular");
            CopyPropertyIfExists(dest, source, "_AlphaToMask");

            // Surface Inputs
            CopyPropertyIfExists(dest, source, "_BaseColor");
            CopyPropertyIfExists(dest, source, "_BaseMap");
            CopyPropertyIfExists(dest, source, "_SpecColor");
            CopyPropertyIfExists(dest, source, "_WorkflowMode");
            CopyPropertyIfExists(dest, source, "_Smoothness");
            CopyPropertyIfExists(dest, source, "_Metallic");
            CopyPropertyIfExists(dest, source, "_MetallicGlossMap");
            CopyPropertyIfExists(dest, source, "_SpecGlossMap");
            CopyPropertyIfExists(dest, source, "_BumpScale");
            CopyPropertyIfExists(dest, source, "_BumpMap");
            CopyPropertyIfExists(dest, source, "_OcclusionStrength");
            CopyPropertyIfExists(dest, source, "_OcclusionMap");
            CopyPropertyIfExists(dest, source, "_EmissionColor");
            CopyPropertyIfExists(dest, source, "_EmissionMap");
        }

        private void CopyPropertyIfExists(Material dest, Material source, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !dest.HasProperty(propertyName))
                return;

            // Check property type
            var propertyType = source.shader.GetPropertyType(source.shader.FindPropertyIndex(propertyName));

            switch (propertyType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    dest.SetColor(propertyName, source.GetColor(propertyName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    dest.SetVector(propertyName, source.GetVector(propertyName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    dest.SetFloat(propertyName, source.GetFloat(propertyName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    dest.SetTexture(propertyName, source.GetTexture(propertyName));
                    dest.SetTextureOffset(propertyName, source.GetTextureOffset(propertyName));
                    dest.SetTextureScale(propertyName, source.GetTextureScale(propertyName));
                    break;
            }
        }

        public void OnDisable()
        {
            try
            {
                if (_initialized)
                    Release();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error during GrassHolder.OnDisable on {gameObject.name}: {e.Message}");
            }
            finally
            {
                _initialized = false;
            }
        }

        // draw the bounds gizmos
        void OnDrawGizmos()
        {
            void RecursivelyDrawTreeBounds(GrassCullingTree tree, Color color)
            {
                foreach (var child in tree.children)
                {
                    if (child.isDrawn)
                    {
                        RecursivelyDrawTreeBounds(child, color * 2);
                        Gizmos.color = color;
                        Gizmos.DrawWireCube(child.bounds.center, child.bounds.size);
                    }
                }
            }

            if (drawBounds && cullingTree != null)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 0.9f, 1f) / 4;
                Gizmos.DrawWireCube(cullingTree.bounds.center, cullingTree.bounds.size);
                RecursivelyDrawTreeBounds(cullingTree, Gizmos.color);
            }
        }

        private void Reset()
        {
#if UNITY_EDITOR
            if (GrassDataSource == null)
            {
                // Determine save path based on current scene
                string savePath = "Assets";
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(currentScene.path))
                {
                    // Save in the same folder as the scene
                    savePath = System.IO.Path.GetDirectoryName(currentScene.path);
                }
                else
                {
                    // If scene is not saved, use Assets/Scenes/ as fallback
                    savePath = "Assets/Scenes";
                }

                GrassDataManager.CreateGrassDataAsset(savePath, this);
                lastAttachedGrassDataSourcePath = AssetDatabase.GetAssetPath(GrassDataSource);
            }
#endif
            mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        }

        #endregion
    }
}