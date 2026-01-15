using UnityEngine;
using NaughtyAttributes;

namespace Ledsna
{
    [RequireComponent(typeof(Camera))]
    public class ConstantPlayerSizeController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool enableConstantSize = true;
        [SerializeField] private Transform playerTransform;

        [Header("Reference Values")]
        [SerializeField] private float referenceDistance = 20f;
        [SerializeField] private float referenceFOV = 60f;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (!enableConstantSize || playerTransform == null)
                return;

            // Calculate current distance
            float currentDistance = Vector3.Distance(transform.position, playerTransform.position);

            // Formula: newFOV = referenceFOV * (referenceDistance / currentDistance)
            float newFOV = referenceFOV * (referenceDistance / currentDistance);

            mainCamera.fieldOfView = newFOV;
        }

        [Button("Copy from Current Setup")]
        private void CopyFromTransform()
        {
            if (mainCamera == null)
            {
                mainCamera = GetComponent<Camera>();
            }

            // Get local Z and negate if negative
            float localZ = transform.localPosition.z;
            referenceDistance = Mathf.Abs(localZ);

            // Get current VERTICAL FOV (Unity only stores vertical FOV)
            referenceFOV = mainCamera.fieldOfView;

            Debug.Log($"[ConstantSize] Copied: Distance={referenceDistance:F2} (from localZ={localZ:F2}), Vertical FOV={referenceFOV:F2}");
            Debug.Log($"[ConstantSize] NOTE: Unity uses VERTICAL FOV internally. If you want horizontal FOV 60°, vertical FOV is ~35° at 16:9");
        }
    }
}
