using UnityEngine;
using UnityEngine.Serialization;
using Vector3 = UnityEngine.Vector3;


namespace Ledsna
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        PlayerManager player;
        [HideInInspector] public float verticalMovement;
        [HideInInspector] public float horizontalMovement;
        [HideInInspector] public float moveAmount;

        [Header("Movement settings")]
        private Vector3 moveDirection;
        private Vector3 lookDirection;
        [SerializeField] float walkingSpeed = 2;
        [SerializeField] float runningSpeed = 5;
        [SerializeField] float sprintingSpeed = 7;
        [SerializeField] float rotationSpeed = 15;
        [SerializeField] int sprintingStaminaCost = 2;

        [Header("Jump")]
        private Vector3 jumpDirection;
        [SerializeField] float jumpHeight = 0.5f;
        [SerializeField] int jumpStaminaCost = 25;
        [SerializeField] float jumpForwardSpeed = 5;
        [FormerlySerializedAs("freeFallingSpeed")][SerializeField] float freeFallSpeed = 2;

        [Header("Dodge")]
        private Vector3 rollDirection;
        [SerializeField] int dodgeStaminaCost = 25;

        protected override void Awake()
        {
            base.Awake();

            player = GetComponent<PlayerManager>();
        }

        protected override void Update()
        {
            base.Update();

            bool isLocalOwner = player.sceneTestingMode || player.IsOwner;

            if (isLocalOwner)
            {
                player.characterNetworkManager.verticalMovement.Value = verticalMovement;
                player.characterNetworkManager.horizontalMovement.Value = horizontalMovement;
                player.characterNetworkManager.moveAmount.Value = moveAmount;
            }
            else
            {
                verticalMovement = player.characterNetworkManager.verticalMovement.Value;
                horizontalMovement = player.characterNetworkManager.horizontalMovement.Value;
                moveAmount = player.characterNetworkManager.moveAmount.Value;

                player.playerAnimatorManager.UpdateAnimatorMovementParameters(0, moveAmount, player.playerNetworkManager.isSprinting.Value);
            }
        }

        private void HandleRotation()
        {
            if (!player.canRotate)
                return;

            lookDirection = Vector3.zero;
            var forward = PlayerCamera.instance.ActualCameraTransform.forward;
            forward.y = 0;
            forward.Normalize();

            var right = PlayerCamera.instance.ActualCameraTransform.right;
            right.y = 0;
            right.Normalize();

            lookDirection = forward * verticalMovement + right * horizontalMovement;
            lookDirection.Normalize();
            // lookDirection.y = 0;

            if (lookDirection == Vector3.zero)
            {
                lookDirection = transform.forward;
            }

            Quaternion rotation = Quaternion.LookRotation(lookDirection);
            Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
            transform.rotation = targetRotation;
        }

        public void HandleAllMovement()
        {

            HandleGroundedMovement();
            HandleRotation();
            HandleJumpingMovement();
            HandleFreeFallMovement();
            // GROUNDED MOVEMENT
            // AERIAL MOVEMENT
        }

        private void GetMovementValues()
        {
            verticalMovement = PlayerInputManager.instance.verticalInput;
            horizontalMovement = PlayerInputManager.instance.horizontalInput;
            moveAmount = PlayerInputManager.instance.moveAmount;
            // CLAMP THE MOVEMENTS
        }

        private void HandleGroundedMovement()
        {
            if (!player.canMove)
                return;

            GetMovementValues();
            var forward = PlayerCamera.instance.ActualCameraTransform.forward;
            forward.y = 0;
            forward.Normalize();

            var right = PlayerCamera.instance.ActualCameraTransform.right;
            right.y = 0;
            right.Normalize();
            moveDirection = forward * verticalMovement +
                            right * horizontalMovement;
            moveDirection.Normalize();
            moveDirection.y = 0;

            if (player.playerNetworkManager.isSprinting.Value)
            {
                player.characterController.Move(Time.deltaTime * sprintingSpeed * moveDirection);
            }
            else
            {
                if (PlayerInputManager.instance.moveAmount > 0.5f)
                {
                    player.characterController.Move(Time.deltaTime * runningSpeed * moveDirection);
                    // Debug.Log(moveDirection * Time.deltaTime * runningSpeed);
                }
                else if (PlayerInputManager.instance.moveAmount <= 0.5f)
                {
                    player.characterController.Move(Time.deltaTime * walkingSpeed * moveDirection);
                    // Debug.Log(moveDirection * Time.deltaTime * runningSpeed);
                }
            }
        }

        private void HandleJumpingMovement()
        {
            if (player.isJumping)
            {
                player.characterController.Move(Time.deltaTime * jumpForwardSpeed * jumpDirection);
            }
        }

        private void HandleFreeFallMovement()
        {
            if (!player.isGrounded)
            {
                Vector3 freeFallDirection;
                freeFallDirection = PlayerCamera.instance.transform.forward *
                                    PlayerInputManager.instance.verticalInput +
                                    PlayerCamera.instance.transform.right *
                                    PlayerInputManager.instance.horizontalInput;
                freeFallDirection.y = 0;

                player.characterController.Move(Time.deltaTime * freeFallSpeed * freeFallDirection);
            }
        }

        public void HandleSprinting()
        {
            if (player.isPerformingAction)
            {
                player.playerNetworkManager.isSprinting.Value = false;
            }

            if (player.playerNetworkManager.currentStamina.Value <= 0)
            {
                player.playerNetworkManager.isSprinting.Value = false;
                return;
            }

            if (moveAmount >= 0.5)
            {
                player.playerNetworkManager.isSprinting.Value = true;
            }
            // IF WE ARE STATIONARY, SET SPRINTING TO FALSE
            else
            {
                player.playerNetworkManager.isSprinting.Value = false;
            }
            // IF WE ARE OUT OF STAMINA, SET SPRINTING TO FALSE
            if (player.playerNetworkManager.isSprinting.Value)
            {
                player.playerNetworkManager.currentStamina.Value -= Time.deltaTime * sprintingStaminaCost;
            }
        }

        public void AttemptToPerformDodge()
        {
            if (player.isPerformingAction)
                return;

            if (player.playerNetworkManager.currentStamina.Value <= 0)
                return;

            GetMovementValues();
            if (PlayerInputManager.instance.moveAmount > 0)
            {
                rollDirection = PlayerCamera.instance.transform.forward * verticalMovement +
                                PlayerCamera.instance.transform.right * horizontalMovement;
                rollDirection.Normalize();

                rollDirection.y = 0;

                if (rollDirection == Vector3.zero)
                {
                    rollDirection = transform.forward;
                }
                Quaternion rotation = Quaternion.LookRotation(rollDirection);
                player.transform.rotation = rotation;

                player.playerAnimatorManager.PlayTargetActionAnimation("Roll_Forward_01", true, true);
                // PERFORM A ROLL ANIMATION
            }
            else
            {
                player.playerAnimatorManager.PlayTargetActionAnimation("Back_Step_01", true, true);
                // PERFORM BACKSTEP ANIMATION
            }

            player.playerNetworkManager.currentStamina.Value -= dodgeStaminaCost;
        }

        public void AttemptToPerformJump()
        {
            if (player.isPerformingAction)
                return;

            if (player.playerNetworkManager.currentStamina.Value <= 0)
                return;

            if (player.isJumping || !player.isGrounded)
                return;

            player.playerAnimatorManager.PlayTargetActionAnimation("Main_Jump_01", false);

            player.isJumping = true;

            player.playerNetworkManager.currentStamina.Value -= jumpStaminaCost;

            jumpDirection = PlayerCamera.instance.transform.forward * PlayerInputManager.instance.verticalInput +
                            PlayerCamera.instance.transform.right * PlayerInputManager.instance.horizontalInput;
            jumpDirection.y = 0;

            if (jumpDirection != Vector3.zero)
            {
                if (player.playerNetworkManager.isSprinting.Value)
                {
                    jumpDirection *= 1;
                }
                else if (PlayerInputManager.instance.moveAmount >= 0.5f)
                {
                    jumpDirection *= 0.5f;
                }
                else if (PlayerInputManager.instance.moveAmount < 0.5f)
                {
                    jumpDirection *= 0.25f;
                }
            }
        }

        public void ApplyJumpingVelocity()
        {
            yVelocity.y = Mathf.Sqrt(jumpHeight * -2 * gravityForce);
        }
    }
}