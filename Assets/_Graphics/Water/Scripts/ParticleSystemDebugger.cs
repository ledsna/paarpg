using UnityEngine;

namespace Ledsna.Water
{
    /// <summary>
    /// Debug tool to diagnose why particle systems aren't rendering.
    /// Attach to a GameObject with a ParticleSystem to see diagnostic info.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleSystemDebugger : MonoBehaviour
    {
        private ParticleSystem ps;
        private ParticleSystemRenderer psRenderer;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            psRenderer = GetComponent<ParticleSystemRenderer>();
        }

        private void OnGUI()
        {
            if (!ps.gameObject.activeInHierarchy) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 500));
            GUILayout.Label("=== Particle System Debugger ===");
            GUILayout.Label($"Active: {ps.gameObject.activeSelf}");
            GUILayout.Label($"Playing: {ps.isPlaying}");
            GUILayout.Label($"Paused: {ps.isPaused}");
            GUILayout.Label($"Particle Count: {ps.particleCount}");
            GUILayout.Label($"Emission Enabled: {ps.emission.enabled}");

            var main = ps.main;
            GUILayout.Label($"Duration: {main.duration}");
            GUILayout.Label($"Start Lifetime: {main.startLifetime.constant}");
            GUILayout.Label($"Start Size: {main.startSize.constant}");
            GUILayout.Label($"Start Speed: {main.startSpeed.constant}");
            GUILayout.Label($"Max Particles: {main.maxParticles}");
            GUILayout.Label($"Simulation Space: {main.simulationSpace}");

            if (psRenderer != null)
            {
                GUILayout.Label("--- Renderer ---");
                GUILayout.Label($"Enabled: {psRenderer.enabled}");
                GUILayout.Label($"Render Mode: {psRenderer.renderMode}");
                GUILayout.Label($"Material: {(psRenderer.sharedMaterial != null ? psRenderer.sharedMaterial.name : "NULL")}");
                if (psRenderer.sharedMaterial != null)
                {
                    GUILayout.Label($"Shader: {psRenderer.sharedMaterial.shader.name}");
                    GUILayout.Label($"Render Queue: {psRenderer.sharedMaterial.renderQueue}");
                }
                GUILayout.Label($"Sorting Layer: {psRenderer.sortingLayerName}");
                GUILayout.Label($"Order in Layer: {psRenderer.sortingOrder}");
            }

            GUILayout.Label("--- Transform ---");
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Scale: {transform.localScale}");

            GUILayout.EndArea();
        }

        private void Update()
        {
            // Draw a debug sphere at particle system position
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 2f, Color.green, 0f);
            Debug.DrawLine(transform.position - Vector3.right, transform.position + Vector3.right, Color.red, 0f);
            Debug.DrawLine(transform.position - Vector3.forward, transform.position + Vector3.forward, Color.blue, 0f);
        }
    }
}
