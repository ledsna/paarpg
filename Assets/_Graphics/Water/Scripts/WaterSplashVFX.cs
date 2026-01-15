using UnityEngine;

namespace Ledsna.Water
{
    /// <summary>
    /// Spawns particle effects for water splashes at wake points.
    /// Works alongside WaterRippleManager to add visual particle effects.
    /// </summary>
    public class WaterSplashVFX : MonoBehaviour
    {
        [Header("Particle System")]
        [SerializeField, Tooltip("Particle system prefab to spawn at splash points")]
        private ParticleSystem splashPrefab;

        [Header("Spawning Rules")]
        [SerializeField, Tooltip("Minimum time between particle spawns (seconds)")]
        private float spawnInterval = 0.25f;

        [SerializeField, Tooltip("Minimum speed to spawn particles")]
        private float minimumSpeed = 0.5f;

        [SerializeField, Tooltip("Maximum number of active particle systems")]
        private int maxActiveParticles = 8;

        [Header("Particle Scaling")]
        [SerializeField, Tooltip("Scale particles based on speed")]
        private bool scaleBySpeed = true;

        [SerializeField, Tooltip("Minimum particle scale at low speeds")]
        private float minScale = 0.5f;

        [SerializeField, Tooltip("Maximum particle scale at high speeds")]
        private float maxScale = 1.5f;

        [SerializeField, Tooltip("Speed at which particles reach max scale")]
        private float maxSpeedForScale = 5.0f;

        private float lastSpawnTime;
        private ParticleSystem[] particlePool;
        private int poolIndex;

        private void Awake()
        {
            if (splashPrefab == null)
            {
                Debug.LogWarning("WaterSplashVFX: No splash prefab assigned!");
                enabled = false;
                return;
            }

            // Create particle pool
            particlePool = new ParticleSystem[maxActiveParticles];
            for (int i = 0; i < maxActiveParticles; i++)
            {
                particlePool[i] = Instantiate(splashPrefab, transform);
                particlePool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Spawn a splash particle effect at the given position with speed-based scaling
        /// </summary>
        public void SpawnSplash(Vector3 worldPosition, Vector3 velocity, float speed)
        {
            // Check spawn interval
            if (Time.time - lastSpawnTime < spawnInterval)
                return;

            // Check minimum speed
            if (speed < minimumSpeed)
                return;

            lastSpawnTime = Time.time;

            // Get next particle system from pool
            ParticleSystem ps = particlePool[poolIndex];
            poolIndex = (poolIndex + 1) % maxActiveParticles;

            // Position at splash point
            ps.transform.position = worldPosition;

            // Rotate to face movement direction (optional)
            if (velocity.sqrMagnitude > 0.001f)
            {
                ps.transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }

            // Scale based on speed
            if (scaleBySpeed)
            {
                float speedRatio = Mathf.Clamp01(speed / maxSpeedForScale);
                float scale = Mathf.Lerp(minScale, maxScale, speedRatio);
                ps.transform.localScale = Vector3.one * scale;
            }

            // Activate and play
            ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();
        }

        private void OnDestroy()
        {
            // Clean up pooled particles
            if (particlePool != null)
            {
                foreach (var ps in particlePool)
                {
                    if (ps != null)
                        Destroy(ps.gameObject);
                }
            }
        }
    }
}
