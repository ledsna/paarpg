using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Ledsna.Water
{
    /// <summary>
    /// Manages water ripple effects by tracking ripple centers and sending them to the water shader.
    /// Attach this to your water plane GameObject.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterRippleManager : MonoBehaviour
    {
        [Header("Ripple Settings")]
        [SerializeField, Tooltip("Maximum number of active ripples")]
        private int maxRipples = 64;

        [SerializeField, Tooltip("Base lifetime of a ripple in seconds")]
        private float baseRippleLifetime = 1.5f;

        [SerializeField, Tooltip("Additional lifetime based on speed")]
        private float speedLifetimeMultiplier = 1.0f;

        [SerializeField, Tooltip("Time between spawning ripples (lower = denser trail)")]
        private float rippleSpawnInterval = 0.05f;

        [SerializeField, Tooltip("Minimum distance between ripples to avoid clumping")]
        private float minSpawnDistance = 0.2f;

        [SerializeField, Tooltip("Minimum movement speed to generate ripples (units/sec)")]
        private float minRippleSpeed = 0.3f;

        [Header("Ripple Appearance")]
        [SerializeField] private float rippleSpeed = 0.7f;
        [SerializeField] private float rippleFrequency = 10.0f;
        [SerializeField] private float rippleAmplitude = 0.035f;

        [Header("Auto-Detection")]
        [SerializeField, Tooltip("Layer mask for objects that can create ripples")]
        private LayerMask rippleObjectLayers = -1;

        [Header("VFX")]
        [SerializeField, Tooltip("Optional: VFX component for spawning splash particles")]
        private WaterSplashVFX splashVFX;

        [Header("Simulation")]
        [SerializeField] private int simulationResolution = 256;
        [SerializeField] private float simulationSize = 50.0f;
        [SerializeField] private float cameraRearPadding = 2.0f;

        // Private data
        private Renderer waterRenderer;
        private Material waterMaterial;
        private List<RippleData> activeRipples = new List<RippleData>();
        private Dictionary<GameObject, float> lastRippleTime = new Dictionary<GameObject, float>();
        private Dictionary<GameObject, Vector3> lastPosition = new Dictionary<GameObject, Vector3>();

        private RenderTexture rippleMap;
        private Material stampMaterial;
        private Mesh quadMesh;
        private CommandBuffer cmd;

        private class RippleData
        {
            public Vector3 position;
            public float spawnTime;
            public Vector3 direction;
            public float speed;
            public float maxAge;
        }

        private void Awake()
        {
            waterRenderer = GetComponent<Renderer>();
            waterMaterial = waterRenderer.material; // Creates instance

            Shader stampShader = Shader.Find("Hidden/RippleStamp");
            if (stampShader == null)
            {
                Debug.LogError("Could not find shader 'Hidden/RippleStamp'. Please ensure it is included in the build.");
                enabled = false;
                return;
            }
            stampMaterial = new Material(stampShader);

            // Create quad mesh for stamping
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempQuad);

            InitializeRippleMap();
        }

        private void InitializeRippleMap()
        {
            if (rippleMap != null) rippleMap.Release();
            rippleMap = new RenderTexture(simulationResolution, simulationResolution, 0, RenderTextureFormat.RHalf);
            rippleMap.name = "RippleMap";
            rippleMap.wrapMode = TextureWrapMode.Clamp;
            rippleMap.filterMode = FilterMode.Bilinear;
            rippleMap.Create();
        }

        private void Update()
        {
            // Remove expired wake points based on their individual maxAge
            // Add a small buffer (0.1s) to ensure they fully fade out visually
            activeRipples.RemoveAll(ripple => Time.time - ripple.spawnTime > ripple.maxAge + 0.1f);

            // Update shader arrays
            UpdateRippleMap();
        }

        /// <summary>
        /// Manually create a ripple at a specific world position with direction and speed
        /// </summary>
        public void CreateRipple(Vector3 worldPosition, Vector3 direction, float speed)
        {
            // Only create if moving fast enough
            if (speed < minRippleSpeed) return;

            // Remove oldest ripple if at capacity
            if (activeRipples.Count >= maxRipples)
            {
                activeRipples.RemoveAt(0);
            }

            // Calculate max age based on speed
            float speedNorm = speed * 0.2f;
            float calculatedMaxAge = baseRippleLifetime + speedNorm * speedLifetimeMultiplier;

            activeRipples.Add(new RippleData
            {
                position = worldPosition,
                spawnTime = Time.time,
                direction = direction.normalized,
                speed = speed,
                maxAge = calculatedMaxAge
            });

            // Spawn VFX particle if available
            if (splashVFX != null)
            {
                splashVFX.SpawnSplash(worldPosition, direction, speed);
            }
        }

        /// <summary>
        /// Create a ripple from a specific GameObject (with cooldown and velocity tracking)
        /// </summary>
        public void CreateRippleFromObject(GameObject obj, Vector3 worldPosition)
        {
            // Check cooldown
            if (lastRippleTime.TryGetValue(obj, out float lastTime))
            {
                if (Time.time - lastTime < rippleSpawnInterval)
                    return;
            }

            // Check distance (Optimization: Squared distance check avoids Sqrt)
            if (lastPosition.TryGetValue(obj, out Vector3 lastPos))
            {
                if ((worldPosition - lastPos).sqrMagnitude < minSpawnDistance * minSpawnDistance)
                    return;
            }

            // Calculate velocity from position change
            Vector3 currentPos = worldPosition;
            Vector3 velocity = Vector3.zero;
            float speed = 0f;

            if (lastPosition.TryGetValue(obj, out Vector3 prevPos))
            {
                float deltaTime = Time.time - lastTime;
                if (deltaTime > 0.001f)
                {
                    velocity = (currentPos - prevPos) / deltaTime;
                    speed = velocity.magnitude;
                }
            }

            lastRippleTime[obj] = Time.time;
            lastPosition[obj] = currentPos;

            // Create ripple if moving - spawn slightly ahead based on velocity to reduce perceived lag
            if (speed >= minRippleSpeed)
            {
                // Predict where player will be by compensating for spawn delay
                Vector3 compensatedPos = worldPosition + velocity.normalized * (speed * 0.05f);
                compensatedPos.y = worldPosition.y; // Keep at water surface
                CreateRipple(compensatedPos, velocity, speed);
            }
        }

        private void UpdateRippleMap()
        {
            if (waterMaterial == null || rippleMap == null) return;

            // 1. Calculate Simulation Area (Offset from Camera, Snapped)
            Vector3 camPos = Vector3.zero;
            Vector3 camForward = Vector3.forward;

            if (Camera.main != null)
            {
                camPos = Camera.main.transform.position;
                camForward = Camera.main.transform.forward;
            }

            // Project forward to XZ plane
            Vector3 forwardXZ = new Vector3(camForward.x, 0, camForward.z).normalized;
            if (forwardXZ.sqrMagnitude < 0.001f) forwardXZ = Vector3.forward;

            // Calculate target center position
            // We want the camera to be 'cameraRearPadding' units away from the back edge.
            // Center is (Size/2 - Margin) units in front of camera.
            float offsetDist = (simulationSize * 0.5f) - cameraRearPadding;
            Vector3 targetCenter = camPos + forwardXZ * offsetDist;

            // Remove snapping to prevent teleporting/gliding artifacts
            // Snapping is only needed if we are scrolling the texture, but here we are regenerating it every frame.
            Vector3 origin = new Vector3(targetCenter.x, 0, targetCenter.z);

            // Center the area: origin is the center
            // So the bounds are [origin.x - size/2, origin.x + size/2]

            // 3. Draw Ripples using CommandBuffer for better control
            if (cmd == null) cmd = new CommandBuffer { name = "UpdateRippleMap" };
            cmd.Clear();

            // Scope for Frame Debugger
            cmd.BeginSample("Render Ripples");

            // Set Render Target
            cmd.SetRenderTarget(rippleMap);
            cmd.ClearRenderTarget(false, true, Color.black);

            // Set View/Projection Matrices
            float halfSize = simulationSize * 0.5f;
            // Revert Y-flip. Standard Ortho maps Bottom->-1, Top->+1.
            // With Camera looking Down (Rot 90 X), View Y aligns with World Z.
            // So World -Z -> View -Y -> Bottom -> -1 (V=0).
            // World +Z -> View +Y -> Top -> +1 (V=1).
            // This matches Shader: uv.y = (pos.z - origin.z)/size + 0.5
            Matrix4x4 proj = Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, -100, 100);
            Matrix4x4 viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(new Vector3(origin.x, 10, origin.z), Quaternion.Euler(90, 0, 0), Vector3.one));
            cmd.SetViewProjectionMatrices(viewMatrix, proj);

            float currentTime = Time.time;

            // Use local settings
            float rSpeed = rippleSpeed;
            float rFreq = rippleFrequency;
            float rAmp = rippleAmplitude;

            foreach (var ripple in activeRipples)
            {
                float age = currentTime - ripple.spawnTime;
                if (age < 0) continue;

                float speed = ripple.speed;
                float speedNorm = speed * 0.2f;
                float effectiveSpeed = rSpeed * (0.7f + 0.6f * Mathf.Min(speedNorm * 1.5f, 1.0f));

                float radius = age * effectiveSpeed + 2.0f;
                float size = radius * 2.0f;

                // Use MaterialPropertyBlock if possible, but for now we set global properties on the material
                // Note: CommandBuffer.DrawMesh doesn't support setting material properties directly on the material object easily per draw call
                // without a PropertyBlock.
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetFloat("_Age", age);
                props.SetFloat("_MaxAge", ripple.maxAge);
                props.SetFloat("_Speed", effectiveSpeed);
                props.SetFloat("_Amplitude", rAmp * (0.5f + Mathf.Min(speedNorm * 1.5f, 1.0f)));
                props.SetFloat("_Frequency", rFreq);
                props.SetFloat("_QuadRadius", radius);

                Matrix4x4 model = Matrix4x4.TRS(ripple.position, Quaternion.Euler(90, 0, 0), new Vector3(size, size, 1));

                cmd.DrawMesh(quadMesh, model, stampMaterial, 0, 0, props);
            }

            cmd.EndSample("Render Ripples");
            Graphics.ExecuteCommandBuffer(cmd);

            // 4. Update Water Material
            waterMaterial.SetTexture("_RippleMap", rippleMap);
            waterMaterial.SetVector("_RippleMapOrigin", new Vector4(origin.x, origin.z, simulationSize, 0));
        }

        private void OnTriggerEnter(Collider other)
        {
            // Fast Layer Check
            if (((1 << other.gameObject.layer) & rippleObjectLayers) == 0) return;

            // Initialize position tracking
            // Optimization: Use transform.position for simple objects instead of ClosestPoint
            // ClosestPoint is expensive on MeshColliders.
            Vector3 contactPoint = other.transform.position;
            contactPoint.y = transform.position.y; // Keep at water surface

            lastPosition[other.gameObject] = contactPoint;
            lastRippleTime[other.gameObject] = Time.time;
        }

        private void OnTriggerStay(Collider other)
        {
            // Fast Layer Check
            if (((1 << other.gameObject.layer) & rippleObjectLayers) == 0) return;

            // Optimization: Don't calculate physics contact points every frame.
            // Just use the object's center projected onto the water plane.
            Vector3 contactPoint = other.transform.position;
            contactPoint.y = transform.position.y;

            CreateRippleFromObject(other.gameObject, contactPoint);
        }

        private void OnDestroy()
        {
            if (rippleMap != null) rippleMap.Release();
            if (cmd != null) cmd.Release();

            // Clean up material instance
            if (waterMaterial != null && waterRenderer.sharedMaterial != waterMaterial)
            {
                if (Application.isPlaying)
                    Destroy(waterMaterial);
                else
                    DestroyImmediate(waterMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize active ripples
            foreach (var ripple in activeRipples)
            {
                float age = Time.time - ripple.spawnTime;
                float alpha = 1f - (age / ripple.maxAge);

                // Draw ripple center
                Gizmos.color = new Color(0f, 1f, 1f, alpha);
                Gizmos.DrawWireSphere(ripple.position, 0.2f);

                // Draw direction arrow (still useful for debug even if ripples are radial)
                Gizmos.color = new Color(1f, 1f, 0f, alpha);
                Vector3 arrowEnd = ripple.position + ripple.direction * 1f;
                Gizmos.DrawLine(ripple.position, arrowEnd);

                // Draw expanding circle
                float speedNorm = ripple.speed * 0.2f;
                float effectiveSpeed = rippleSpeed * (0.7f + 0.6f * Mathf.Min(speedNorm * 1.5f, 1.0f));
                float radius = age * effectiveSpeed + 2.0f;

                Gizmos.color = new Color(0f, 1f, 1f, alpha * 0.5f);
                // Draw circle on XZ plane
                int segments = 32;
                float angleStep = 360f / segments;
                Vector3 prevPoint = ripple.position + new Vector3(radius, 0, 0);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 nextPoint = ripple.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    Gizmos.DrawLine(prevPoint, nextPoint);
                    prevPoint = nextPoint;
                }
            }
        }
    }
}
