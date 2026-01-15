using Grass.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grass.Editor
{
    public partial class GrassMakerWindow
    {
        void DrawHandles()
        {
            if (Physics.Raycast(ray.origin, ray.direction, out var hitInfo, 200f, paintHitMask.value))
            {
                hitPos = hitInfo.point;
                hitNormal = hitInfo.normal;
            }

            // base
            Color discColor = Color.green;
            Color discColor2 = new(0, 0.5f, 0, 0.4f);

            Handles.color = discColor;
            Handles.DrawWireDisc(hitPos, hitNormal, brushSize);
            Handles.color = discColor2;
            Handles.DrawSolidDisc(hitPos, hitNormal, brushSize);

            if (hitPos != cachedPos)
            {
                SceneView.RepaintAll();
                cachedPos = hitPos;
            }
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += this.OnScene;
            terrainHit = new RaycastHit[1];
        }

        private void RemoveDelegates()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui -= this.OnScene;
        }

        private void OnDisable()
        {
            RemoveDelegates();
        }

        private void OnDestroy()
        {
            RemoveDelegates();
        }

        private void OnScene(SceneView scene)
        {
            if (this != null && paintModeActive)
            {
                var e = Event.current;
                mousePos = e.mousePosition;
                var ppp = EditorGUIUtility.pixelsPerPoint;
                mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
                mousePos.x *= ppp;
                mousePos.z = 0;

                // ray for gizmo(disc)
                ray = scene.camera.ScreenPointToRay(mousePos);
                // undo system
                if (e.type == EventType.MouseDown && e.button == 1)
                {
                    e.Use();
                    switch (toolbarInt)
                    {
                        case 0:
                            Undo.RegisterCompleteObjectUndo(this, "Added Grass");
                            break;

                        case 1:
                            Undo.RegisterCompleteObjectUndo(this, "Removed Grass");
                            break;
                    }
                }

                if (e.type == EventType.MouseDrag && e.button == 1)
                {
                    switch (toolbarInt)
                    {
                        case 0:
                            AddGrassPainting(terrainHit, e);
                            break;
                        case 1:
                            RemoveAtPoint(terrainHit, e);
                            break;
                    }

                    RebuildMesh();
                }

                // on up
                if (e.type == EventType.MouseUp && e.button == 1)
                {
                    RebuildMesh();
                }
            }
        }

        private void RemovePositionsNearRaycastHit(Vector3 hitPoint, float radius)
        {
            // Remove positions within the specified radius
            _grassHolder.grassData.RemoveAll(pos => Vector3.Distance(pos.position, hitPoint) <= radius);
        }

        public void RemoveAtPoint(RaycastHit[] terrainHit, Event e)
        {
            int hits = (Physics.RaycastNonAlloc(ray, terrainHit, 100f, paintHitMask.value));
            for (int i = 0; i < hits; i++)
            {
                hitPos = terrainHit[i].point;
                // hitPosGizmo = hitPos;
                hitNormal = terrainHit[i].normal;
                RemovePositionsNearRaycastHit(hitPos, brushSize);
            }

            e.Use();
        }

        public void AddGrassPainting(RaycastHit[] terrainHit, Event e)
        {
            int hits = (Physics.RaycastNonAlloc(ray, terrainHit, 200f, paintHitMask.value));
            for (int i = 0; i < hits; i++)
            {
                if ((paintHitMask.value & (1 << terrainHit[i].transform.gameObject.layer)) > 0)
                {
                    int grassToPlace = (int)(density * brushSize);


                    for (int k = 0; k < grassToPlace; k++)
                    {
                        if (terrainHit[i].normal != Vector3.zero)
                        {
                            Vector2 randomOffset = Random.insideUnitCircle *
                                                   (brushSize * 10 / EditorGUIUtility.pixelsPerPoint);

                            Vector2 mousePosition = e.mousePosition;
                            Vector2 randomPosition = mousePosition + randomOffset;

                            Ray ray2 = HandleUtility.GUIPointToWorldRay(randomPosition);


                            int hits2 = (Physics.RaycastNonAlloc(ray2, terrainHit, 200f, paintHitMask.value));
                            for (int l = 0; l < hits2; l++)
                            {
                                if ((paintHitMask.value & (1 << terrainHit[l].transform.gameObject.layer)) > 0 &&
                                    terrainHit[l].normal.y <= (1 + _grassHolder.normalLimit) &&
                                    terrainHit[l].normal.y >= (1 - _grassHolder.normalLimit))
                                {
                                    hitPos = terrainHit[l].point;
                                    hitNormal = terrainHit[l].normal;

                                    // Helper to create grass data with material selection
                                    GrassData CreateGrassData()
                                    {
                                        GrassData newData = new GrassData();
                                        newData.position = hitPos;
                                        newData.normal = hitNormal;
                                        newData.lightmapUV = terrainHit[l].textureCoord;

                                        // Apply material system selection for variety
                                        if (_grassHolder.materialSystem != null && _grassHolder.materialSystem.IsValid())
                                        {
                                            newData.materialIndex = _grassHolder.materialSystem.SelectMaterialIndex(hitPos);
                                        }
                                        else
                                        {
                                            newData.materialIndex = 0;
                                        }

                                        return newData;
                                    }

                                    if (k != 0)
                                    {
                                        // can paint
                                        _grassHolder.grassData.Add(CreateGrassData());
                                    }
                                    else
                                    {
                                        // to not place everything at once, check if the first placed point far enough away from the last placed first one
                                        if (Vector3.Distance(terrainHit[l].point, lastPosition) > brushSize)
                                        {
                                            _grassHolder.grassData.Add(CreateGrassData());
                                            lastPosition = hitPos;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            e.Use();
        }

        private void RebuildMesh()
        {
            // Reset cleared flag since grass was painted
            _grassHolder.SetGrassClearedFlag(false);
            
            _grassHolder.FastSetup();
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
#endif
    }
}