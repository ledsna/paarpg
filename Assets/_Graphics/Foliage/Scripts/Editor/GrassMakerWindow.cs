using Grass.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;

namespace Grass.Editor
{
    public partial class GrassMakerWindow : EditorWindow
    {
        [FormerlySerializedAs("grassObject")] [SerializeField]
        private GameObject grassHolderObject;

        [SerializeField] private int grassCount = 1000;

        [HideInInspector] public int toolbarInt = 0;

        private GrassHolder _grassHolder;
        private Vector2 scrollPos;
        private int currentMainTabId;
        private readonly string[] mainTabBarStrings = { "Paint", "Generate" };
        private bool paintModeActive;
        private readonly string[] toolbarStrings = { "Add", "Remove" };
        private float brushSize = 0.2f;
        private Ray ray;
        private Vector3 hitPos;
        private Vector3 hitNormal;
        private Vector3 cachedPos;
        private RaycastHit[] terrainHit;
        private Vector3 mousePos;
        private Vector3 lastPosition = Vector3.zero;
        private float density = 0.1f;

        public LayerMask cullGrassMask;
        public LayerMask paintHitMask;


        [MenuItem("Tools/Grass Maker")]
        static void Init()
        {
            GrassMakerWindow window = (GrassMakerWindow)GetWindow(typeof(GrassMakerWindow), false, "Grass Maker", true);
            window.titleContent = new GUIContent("Grass Maker");
            window.Show();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.BeginHorizontal();
            currentMainTabId = GUILayout.Toolbar(currentMainTabId, mainTabBarStrings, GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            grassHolderObject = (GameObject)EditorGUILayout.ObjectField("Grass Handle Object",
                grassHolderObject,
                typeof(GameObject),
                true);
            
            if (grassHolderObject == null) {
                grassHolderObject = FindFirstObjectByType<GrassHolder>()?.gameObject;
            }
            
            if (grassHolderObject == null){
                if (GUILayout.Button("Create Grass Holder")) {
                    CreateNewGrassHolder();
                }

                EditorGUILayout.LabelField("No Grass Holder found, create a new one", EditorStyles.label);
                EditorGUILayout.EndScrollView();
                return;
            }
            
            _grassHolder = grassHolderObject?.GetComponent<GrassHolder>();

            if (_grassHolder is null)
            {
                EditorGUILayout.LabelField(
                    "One of necessary component are missing(GrassHolder). Creating grass is impossible",
                    EditorStyles.helpBox);
                EditorGUILayout.EndScrollView();
                return;
            }

            switch (currentMainTabId)
            {
                case 0:
                    ShowPaintPanel();
                    break;
                case 1:
                    ShowGeneratePanel();
                    break;
            }

            GUILayout.FlexibleSpace();


            EditorGUILayout.LabelField($"Total grass instances: {_grassHolder.grassData.Count}",
                EditorStyles.boldLabel);

            if (GUILayout.Button("Clear Grass"))
            {
                if (EditorUtility.DisplayDialog("Clear All Grass?",
                        "Are you sure you want to clear the grass?", "Clear", "Don't Clear"))
                    if (GrassDataManager.TryClearGrassData(_grassHolder))
                        Debug.Log($"Clear Grass Success");
                    else
                        Debug.LogError($"Clear Grass Failed");
            }

            if (GUILayout.Button("Save Positions"))
            {
                if (GrassDataManager.TrySaveGrassData(_grassHolder))
                    Debug.Log("Grass Data Saved");
                else
                    Debug.LogError("Grass Data Not Saved");
            }

            if (GUILayout.Button("Load Positions"))
            {
                if (GrassDataManager.TryLoadGrassData(_grassHolder))
                {
                    _grassHolder.Reinitialize();
                    Debug.Log("Grass Data Loaded and Reinitialized");
                }
                else
                {
                    Debug.LogError("Grass Data Not Loaded");
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowPaintPanel()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel);
            paintModeActive = EditorGUILayout.Toggle(paintModeActive);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
            paintHitMask = EditorGUILayout.MaskField("Paint Hit Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(paintHitMask),
                InternalEditorUtility.layers);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Paint Status (Right-Mouse Button to paint)", EditorStyles.boldLabel);
            toolbarInt = GUILayout.Toolbar(toolbarInt, toolbarStrings);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.1f, 50f);

            if (toolbarInt == 0)
            {
                density = EditorGUILayout.Slider("Density", density, 0.1f, 10f);
            }

            EditorGUILayout.Separator();
        }

        private void ShowGeneratePanel()
        {
            cullGrassMask = EditorGUILayout.MaskField("Cull Mask",
                cullGrassMask,
                UnityEditorInternal.InternalEditorUtility.layers);

            grassHolderObject ??= FindFirstObjectByType<GrassHolder>()?.gameObject;

            if (grassHolderObject != null)
            {
                EditorGUILayout.Space(5);
                _grassHolder.normalLimit = EditorGUILayout.Slider("Slope Limit", _grassHolder.normalLimit, 0f, 1f);

                EditorGUILayout.Space(5);
                grassCount = EditorGUILayout.IntField("Count Grass per Mesh", grassCount);

                EditorGUILayout.Space(10);

                // Info about material configuration
                EditorGUILayout.HelpBox(
                    "To configure materials, grass/flower groups, clustering, and other settings:\n" +
                    "Select the GrassHolder object in the hierarchy and edit its Material System in the inspector.",
                    MessageType.Info);

                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Generate Grass"))
                {
                    GameObject[] selectedObjects = Selection.gameObjects;
                    if (selectedObjects == null || selectedObjects.Length == 0)
                    {
                        Debug.LogError("GrassMaker: No objects selected!");
                        return;
                    }

                    int successCount = 0;
                    int failCount = 0;

                    foreach (var obj in selectedObjects)
                    {
                        // Check if the object itself can have grass (has MeshFilter or Terrain)
                        bool canGenerateOnObject = obj.GetComponent<MeshFilter>() != null || obj.GetComponent<Terrain>() != null;

                        if (canGenerateOnObject)
                        {
                            if (GrassCreator.TryGeneratePoints(_grassHolder,
                                    obj,
                                    grassCount,
                                    cullGrassMask,
                                    _grassHolder.normalLimit,
                                    _grassHolder.materialSystem))
                            {
                                successCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        else
                        {
                            // No mesh/terrain on parent, try all children
                            var childMeshes = obj.GetComponentsInChildren<MeshFilter>();
                            var childTerrains = obj.GetComponentsInChildren<Terrain>();

                            if (childMeshes.Length > 0 || childTerrains.Length > 0)
                            {
                                // Process child meshes
                                foreach (var childMesh in childMeshes)
                                {
                                    if (GrassCreator.TryGeneratePoints(_grassHolder,
                                            childMesh.gameObject,
                                            grassCount,
                                            cullGrassMask,
                                            _grassHolder.normalLimit,
                                            _grassHolder.materialSystem))
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        failCount++;
                                    }
                                }

                                // Process child terrains
                                foreach (var childTerrain in childTerrains)
                                {
                                    if (GrassCreator.TryGeneratePoints(_grassHolder,
                                            childTerrain.gameObject,
                                            grassCount,
                                            cullGrassMask,
                                            _grassHolder.normalLimit,
                                            _grassHolder.materialSystem))
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        failCount++;
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"GrassMaker: {obj.name} has no Mesh Filter or Terrain on itself or children!");
                                failCount++;
                            }
                        }
                    }
                    
                    if (GrassDataManager.TrySaveGrassData(_grassHolder))
                        Debug.Log("Grass Data Saved");
                    else
                        Debug.LogError("Grass Data Not Saved");
                }
            }
            else
            {
                if (GUILayout.Button("Create Grass Holder"))
                {
                    CreateNewGrassHolder();
                }

                EditorGUILayout.LabelField("No Grass Holder found, create a new one", EditorStyles.label);
            }
        }

        void CreateNewGrassHolder()
        {
            grassHolderObject = new GameObject();
            grassHolderObject.name = "Grass Holder";
            grassHolderObject.layer = LayerMask.NameToLayer("Grass");
            _grassHolder = grassHolderObject.AddComponent<GrassHolder>();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hasFocus && paintModeActive)
                DrawHandles();
        }
    }
}