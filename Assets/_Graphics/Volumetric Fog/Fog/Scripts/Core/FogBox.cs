using NaughtyAttributes;
using UnityEngine;

namespace VolumetricFog.Core
{
    [ExecuteAlways]
    public class FogBox : MonoBehaviour
    {
        public bool DrawFogBox = true;

        [ShowIf("DrawFogBox")]
        public Color gizmoColor = new(0f, 1f, 1f, 0.25f);

        private void OnEnable()
        {
            Push();
        }

        private void Update()
        {
            Push();
        }

        private void Push()
        {
            var boundsMin = transform.localToWorldMatrix * new Vector4(-0.5f, -0.5f, -0.5f, 1f);
            var boundsMax = transform.localToWorldMatrix * new Vector4(0.5f, 0.5f, 0.5f, 1f);

            Shader.SetGlobalVector("_BoundsMin", boundsMin);
            Shader.SetGlobalVector("_BoundsMax", boundsMax);
        }

        private void OnDrawGizmos()
        {
            if (!DrawFogBox) return;

            Gizmos.color = gizmoColor;

            // Draw a solid cube
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one); // unit cube scaled by transform

            // Draw a wireframe outline (slightly brighter)
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}