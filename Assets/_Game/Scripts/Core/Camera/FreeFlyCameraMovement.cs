using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ledsna
{
    public class FreeFlyCameraMovement : MonoBehaviour
    {
        [SerializeField] private float defaultCameraSpeed = 15f;
        [SerializeField] private float fastCameraSpeed = 25f;

        [SerializeField] private bool inertness = true;

        [ShowIf("inertness"), BoxGroup("Inertness")]
        [SerializeField] private float slowdownForce = 10f;
        
        [ShowIf("inertness"), BoxGroup("Inertness")]
        [SerializeField] private float accelerationForce = 10f;

        private Vector3 velocityVector;

        private PlayerControls playerControls;
        private InputAction movementAction;
        private InputAction verticalMovementAction;
        private InputAction increaseSpeedAction;

        private bool speedIncreased;

        private float TargetSpeedFromInput => speedIncreased ? fastCameraSpeed : defaultCameraSpeed;
        private float currentSpeed;

        private void Awake()
        {
            playerControls = new PlayerControls();
            movementAction = playerControls.CameraMovement.Movement;
            verticalMovementAction = playerControls.CameraMovement.VerticalMovement;
            increaseSpeedAction = playerControls.CameraMovement.IncreaseSpeed;
        }

        private void OnEnable()
        {
            if (playerControls == null)
            {
                playerControls = new PlayerControls();
                movementAction = playerControls.CameraMovement.Movement;
                verticalMovementAction = playerControls.CameraMovement.VerticalMovement;
                increaseSpeedAction = playerControls.CameraMovement.IncreaseSpeed;
            }
            playerControls.Enable();
            
            increaseSpeedAction.performed += HandleIncreaseSpeedPerformed;
            increaseSpeedAction.canceled += HandleIncreaseSpeedCanceled;
        }

        private void OnDisable()
        {
            increaseSpeedAction.performed -= HandleIncreaseSpeedPerformed;
            increaseSpeedAction.canceled -= HandleIncreaseSpeedCanceled;
            
            if (playerControls != null)
            {
                playerControls.Disable();
            }
        }

        private void HandleIncreaseSpeedPerformed(InputAction.CallbackContext context) => speedIncreased = true;

        private void HandleIncreaseSpeedCanceled(InputAction.CallbackContext context) => speedIncreased = false;

        private void HandleFreeFly(Vector2 input, float upDownMovement)
        {
            var moveVector = transform.up * upDownMovement;

            var forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            moveVector += (transform.right * input.x + forward * input.y);

            if (!inertness)
            {
                transform.position += moveVector * (Time.deltaTime * TargetSpeedFromInput);
                return;
            }

            var delta = 0.01f;
            var targetSpeed = TargetSpeedFromInput;
            var force = accelerationForce;
            if (moveVector.sqrMagnitude < delta)
            {
                targetSpeed = 0;
                force = slowdownForce;
            }

            var targetVelocityVector = moveVector * (Time.deltaTime * targetSpeed);
            velocityVector = Vector3.Lerp(velocityVector, targetVelocityVector, force * Time.deltaTime);
            transform.position += velocityVector;
        }

        private void Update()
        {
            HandleFreeFly(movementAction.ReadValue<Vector2>(), verticalMovementAction.ReadValue<float>());
        }
    }
}