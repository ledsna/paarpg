using UnityEngine;
using UnityEngine.InputSystem;
using NaughtyAttributes;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem.Controls;

namespace Ledsna
{
    [ComponentInfo("Implements rotation and zoom logic ONLY")]
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera instance;

        public PlayerManager player;

        // Public accessor for the actual camera transform (not the pivot)
        public Transform ActualCameraTransform => cameraObject.transform;
        #region Rotation Settings

        enum RotationAxes
        {
            MouseX,
            Both
        }

        [Foldout("Rotation Settings")]
        [SerializeField] RotationAxes rotationAxes = RotationAxes.Both;

        [Foldout("Rotation Settings")]
        [SerializeField] private bool bringPositionToTargetGrid;

        [Foldout("Rotation Settings")]
        [SerializeField] float angleIncrement = 45f;

        [Foldout("Rotation Settings")]
        [Label("Clamp Delta Value (Hint)")]
        [Tooltip(
            "Change this value for clamping input value from Mouse/Delta X. If not clamp you can see buggy " +
            "rotation that appear with big values of delta."
        )]
        [SerializeField] float clampDeltaValue = 15f;

        [Foldout("Rotation Settings")]
        [SerializeField] private bool clampRotationAxisX = true;

        [Foldout("Rotation Settings")]
        [MinMaxSlider(-90.0f, 90.0f)]
        [ShowIf("clampRotationAxisX")]
        [SerializeField] Vector2 minMaxClampRotation;

        [Foldout("Rotation Settings")]
        [SerializeField] float rotationSensitivity = 2f;

        [Foldout("Rotation Settings")]
        [SerializeField] float rotationSpeed = 5f;
        [SerializeField] float cameraSmoothTime = 7;

        [SerializeField] float cameraSpeed = 25;
        float targetAngleY = 45f;
        float targetAngleX = 30f;

        #endregion

        #region Zoom settings

        [Foldout("Zoom settings")]
        [SerializeField] float zoomSpeed = 5000f; // Speed of zoom

        [Foldout("Zoom settings")]
        [SerializeField] float minZoom = 1f; // Minimum zoom level

        [Foldout("Zoom settings")]
        [SerializeField] float maxZoom = 5f; // Maximum zoom level

        [Foldout("Zoom settings")]
        [SerializeField] float zoomSmoothness = 10f; // Smoothness of the zoom transition

        #endregion

        private float targetZoom;
        private float zoomLerpRate;
        private float zoom = 1;
        private Vector3 currentVelocity;
        private float currentAngle;

        private PlayerControls playerControls;
        private InputAction rotationAction;
        private InputAction zoomAction;
        private InputAction unzoomAction;

        private bool isCursorLocked = true;
        private InputAction escAction; // Esc key
        private InputAction clickAction;


        [SerializeField] Camera cameraObject;
        [SerializeField] SphereCollider cameraCollider;
        [SerializeField] Rigidbody cameraRigidbody;
        [SerializeField] LayerMask collisionMask;

        private Vector3 defaultCameraPosition;
        private float targetCameraPositionZ;

        private bool ShouldMakeFreeRotation()
        {

            if (!isCursorLocked) return false;

            // Using the Input System's rotation action
            if (rotationAction == null)
                return false;

            Vector2 delta = rotationAction.ReadValue<Vector2>();
            // If the player is moving the mouse or right stick
            return delta != Vector2.zero;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isCursorLocked = true;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isCursorLocked = false;
        }

        private void OnEnable()
        {
            if (playerControls == null)
            {
                playerControls = new PlayerControls();
                rotationAction = playerControls.CameraMovement.Rotation;
                zoomAction = playerControls.CameraMovement.Zoom;
                unzoomAction = playerControls.CameraMovement.Unzoom;

                escAction = playerControls.UI.Esc;
                clickAction = playerControls.UI.Click;
            }
            playerControls.Enable();

            LockCursor(); // start locked


            escAction.performed += ctx => UnlockCursor();
            clickAction.performed += ctx =>
            {
                if (!isCursorLocked)
                    LockCursor();
            };
            zoomAction.performed += HandleZoom;
            unzoomAction.performed += HandleZoom;
        }

        private void OnDisable()
        {
            zoomAction.performed -= HandleZoom;
            unzoomAction.performed -= HandleZoom;

            if (playerControls != null)
            {
                playerControls.Disable();
            }
        }

        private void Awake()
        {
            if (instance != null)
                Destroy(gameObject);
            instance = this;

            playerControls = new PlayerControls();
            rotationAction = playerControls.CameraMovement.Rotation;
            zoomAction = playerControls.CameraMovement.Zoom;
            unzoomAction = playerControls.CameraMovement.Unzoom;

            escAction = playerControls.UI.Esc;
            clickAction = playerControls.UI.Click;

            targetAngleY = transform.rotation.eulerAngles.y;
            targetAngleX = transform.rotation.eulerAngles.x;
        }

        private void Start()
        {
            defaultCameraPosition = cameraObject.transform.localPosition;
            DontDestroyOnLoad(gameObject);
        }

        private Vector3 cameraVelocity = Vector3.zero;
        [SerializeField] private float springStiffness = 50f;
        [SerializeField] private float springDamping = 5f;

        private void HandlePhysicsCollision()
        {
            if (!cameraObject || !cameraCollider) return;

            Vector3 pivotPos = transform.position;
            Vector3 idealCameraWorldPos = transform.TransformPoint(defaultCameraPosition);

            Vector3 currentCameraPos = cameraObject.transform.position;

            Vector3 displacement = idealCameraWorldPos - currentCameraPos;
            Vector3 springForce = displacement * springStiffness - cameraVelocity * springDamping;

            cameraVelocity += springForce * Time.deltaTime;
            Vector3 newCameraPos = currentCameraPos + cameraVelocity * Time.deltaTime;

            // Collision detection and resolution
            float radius = cameraCollider.radius;
            Collider[] overlaps = Physics.OverlapSphere(newCameraPos, radius, collisionMask, QueryTriggerInteraction.Ignore);

            foreach (Collider col in overlaps)
            {
                if (col == cameraCollider) continue;

                if (Physics.ComputePenetration(
                    cameraCollider, newCameraPos, Quaternion.identity,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    newCameraPos += direction * distance;

                    // Remove velocity component going into the surface (for sliding)
                    cameraVelocity += Vector3.Dot(cameraVelocity, -direction) * direction;

                    // Debug.DrawRay(newCameraPos, direction * distance, Color.red, 0.1f);
                }
            }

            cameraObject.transform.position = newCameraPos;

            Vector3 directionToPivot = pivotPos - newCameraPos;
            cameraObject.transform.rotation = Quaternion.LookRotation(directionToPivot);

            // Debug visualization
            // Debug.DrawLine(pivotPos, idealCameraWorldPos, Color.cyan, 0.1f);
            // Debug.DrawLine(currentCameraPos, newCameraPos, Color.yellow, 0.1f);
        }


        private void HandleRotation(Vector2 input)
        {
            var clampedDeltaX = Mathf.Clamp(input.x, -clampDeltaValue, clampDeltaValue);
            var clampedDeltaY = Mathf.Clamp(input.y, -clampDeltaValue, clampDeltaValue);

            if (ShouldMakeFreeRotation())
            {
                targetAngleY += clampedDeltaX * rotationSensitivity;
                if (rotationAxes == RotationAxes.Both)
                    targetAngleX += clampedDeltaY * rotationSensitivity;
            }
            else if (bringPositionToTargetGrid)
            {
                // Snap to the closest whole increment angle
                targetAngleY = Mathf.Round(targetAngleY / angleIncrement);
                targetAngleY *= angleIncrement;
            }

            if (clampRotationAxisX)
            {
                targetAngleX = Mathf.Clamp(targetAngleX, minMaxClampRotation.x, minMaxClampRotation.y);
            }
            else
            {
                // apply default clamp
                targetAngleX = Mathf.Clamp(targetAngleX, -90, 90);
            }

            targetAngleY = ModAngle(targetAngleY);

            var currentAngleY = Mathf.LerpAngle(transform.eulerAngles.y, targetAngleY,
                rotationSpeed * Time.deltaTime);
            var currentAngleX = Mathf.LerpAngle(transform.eulerAngles.x, targetAngleX,
                rotationSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Euler(currentAngleX, currentAngleY, 0);
        }

        // return angle mod 360
        private float ModAngle(float angle)
        {
            if (angle >= 0)
                return angle % 360;

            var quotient = (int)(angle / 360);
            return (angle - (quotient - 1) * 360) % 360;
        }

        private void HandleFollowTarget()
        {
            // Locked camera
            if (player is not null)
            {
                Vector3 targetPivotPosition = Vector3.SmoothDamp
                (transform.position,
                    player.transform.position + new Vector3(0, 1.8f, 0), // + player height (1.7?) to pivot around player's head
                    ref currentVelocity,
                    cameraSmoothTime * Time.deltaTime);

                transform.position = targetPivotPosition;
                return;
            }

            // Unlocked camera

            // if (PlayerInputManager.instance.moveUp) {
            //     transform.position += Vector3.up * (Time.deltaTime * cameraSpeed);
            // }
            // else if (PlayerInputManager.instance.moveDown) {
            //     transform.position -= Vector3.up * (Time.deltaTime * cameraSpeed);
            // }
            //
            // if (PlayerInputManager.instance.cameraMovementInput != Vector2.zero) {
            //     // Normalize movement to ensure consistent speed
            //     Vector2 directionSS = PlayerInputManager.instance.cameraMovementInput;
            //     // localForwardVector.x = transform.up.x;
            //     // localForwardVector.z = transform.up.z;
            //     Vector3 directionWS = transform.right * directionSS.x + transform.up * directionSS.y;
            //     transform.position += (Time.deltaTime * cameraSpeed) * directionWS;
            // }

        }

        private void Zoom(float target_zoom)
        {
            // TODO: Do better
            // orthographicRectTransform.localScale = new Vector3(target_zoom, target_zoom, target_zoom);
        }

        private void HandleZoom(InputAction.CallbackContext context)
        {
            var deltaScroll = context.ReadValue<float>();
            if (deltaScroll == 0) return;
            targetZoom += deltaScroll * zoomSpeed; // Calculate target zoom level based on input
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom); // Clamp target zoom to min/max bounds
            zoomLerpRate = 1f - Mathf.Pow(1f - zoomSmoothness * Time.deltaTime, 3);
            Zoom(Mathf.Lerp(zoom, targetZoom, zoomLerpRate));
            zoom = targetZoom;
        }

        public void HandleAllCameraActions()
        {
            // https://www.reddit.com/r/Unity3D/comments/13worxt/im_having_problem_with_input_systems_mouse_delta
            var scaledDelta = rotationAction.ReadValue<Vector2>();
            HandleRotation(scaledDelta);
            HandleFollowTarget();
            HandlePhysicsCollision();
        }

        private void LateUpdate()
        {
            // If player is assigned, let the player control camera via HandleAllCameraActions()
            // Player's LateUpdate calls this in scene testing mode or when IsOwner
            if (player is not null)
                return;

            // If no player, allow free camera movement
            HandleAllCameraActions();
        }
    }
}
