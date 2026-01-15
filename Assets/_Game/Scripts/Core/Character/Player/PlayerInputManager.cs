using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ledsna
{
	public class PlayerInputManager : MonoBehaviour {
		public static PlayerInputManager instance;

		public PlayerManager player;
		PlayerControls playerControls;
		
		[Header("SCENE TESTING")]
		[Tooltip("Enable to use input manager in scene without loading from menu")]
		public bool sceneTestingMode = false;
		
		[Header("PLAYER MOVEMENT INPUT")]
		[SerializeField] Vector2 movementInput;
		public float verticalInput;
		public float horizontalInput;
		public float moveAmount;

		[Header("PLAYER ACTION INPUT")] 
		[SerializeField] bool dodgeInput = false;
		[SerializeField] bool sprintInput = false;
		[SerializeField] bool jumpInput = false;
		
		public Vector2 cameraMovementInput;
		public bool moveUp = false;
		public bool moveDown = false;

		private void Awake() {
			if (instance == null) {
				instance = this;
			}
			else {
				Destroy(gameObject);
			}
		}

		private void OnSceneChange(Scene oldScene, Scene newScene)
		{
			// Skip scene change handling in scene testing mode
			if (sceneTestingMode)
				return;
				
			// IF WE ARE LOADING INTO THE WORLD SCENE, ENABLE CONTROLS
			if (WorldSaveGameManager.instance != null && newScene.buildIndex == WorldSaveGameManager.instance.GetWorldSceneIndex())
			{
				instance.enabled = true;
			}
			// DISABLE IN MENU
			else
			{
				instance.enabled = false;
			}
		}

		private void Start() 
		{
			DontDestroyOnLoad(gameObject);

			SceneManager.activeSceneChanged += OnSceneChange;

			// In scene testing mode, keep input enabled
			if (sceneTestingMode)
			{
				instance.enabled = true;
			}
			else
			{
				instance.enabled = false;
			}
		}

		private void OnEnable() 
		{
			if (playerControls == null) {
				playerControls = new PlayerControls();

				playerControls.PlayerMovement.Movement.performed += i => movementInput = i.ReadValue<Vector2>();
				playerControls.PlayerAction.Dodge.performed += i => dodgeInput = true;
				playerControls.PlayerAction.Jump.performed += i => jumpInput = true;
				
				playerControls.PlayerAction.Sprint.performed += i => sprintInput = true;
				playerControls.PlayerAction.Sprint.canceled += i => sprintInput = false;
				
				playerControls.CameraMovement.Movement.performed += i => cameraMovementInput = i.ReadValue<Vector2>();

			}

			playerControls.Enable();
		}

		private void OnDestroy() 
		{
			SceneManager.activeSceneChanged -= OnSceneChange;
		}

		private void OnApplicationFocus(bool focus)
		{
			if (!enabled)
				return;
			
			if (playerControls == null)
				return;
			if (!focus)
			{
				playerControls.Disable();
				return;
			}
			playerControls.Enable();
		}
		
		private void Update() {
			// Don't process input if player isn't assigned yet
			if (player == null)
				return;
				
			HandleALlInputs();
			horizontalInput = movementInput.x;
			verticalInput = movementInput.y;
			
			// Vertical Movement is a single axis: positive = up (E key), negative = down (Q key)
			float verticalMovement = playerControls.CameraMovement.VerticalMovement.ReadValue<float>();
			moveUp = verticalMovement > 0;
			moveDown = verticalMovement < 0;
		}
		
		private void HandleALlInputs()
		{
			HandleMovementInput();
			HandleDodgeInput();
			HandleSprintingInput();
			HandleJumpInput();
		}

		private void HandleMovementInput() 
		{
			verticalInput = movementInput.y;
			horizontalInput = movementInput.x;

			moveAmount = Mathf.Clamp01(Mathf.Abs(verticalInput) + Mathf.Abs(horizontalInput));

			if (moveAmount <= 0.5 && moveAmount > 0)
			{
				moveAmount = 0.5f;
			}
			else if (moveAmount > 0.5 && moveAmount <= 1)
			{
				moveAmount = 1;
			}
			
			if (player != null && player.playerAnimatorManager != null && player.playerNetworkManager != null)
			{
				player.playerAnimatorManager.UpdateAnimatorMovementParameters(0, moveAmount, player.playerNetworkManager.isSprinting.Value);
			}
		}

		private void HandleDodgeInput()
		{
			if (!dodgeInput || player == null)
				return;
			
			dodgeInput = false;
			
			// FUTURE NOTE: RETURN (DO NOTHING) IF MENU OR UI WINDOW IS OPEN, DO NOTHING
			
			// PERFORM A DODGE
			player.playerLocomotionManager.AttemptToPerformDodge();
		}

		private void HandleSprintingInput()
		{
			if (player == null)
				return;
				
			if (sprintInput)
			{
				player.playerLocomotionManager.HandleSprinting();
			}
			else
			{
				player.playerNetworkManager.isSprinting.Value = false;
			}
		}

		private void HandleJumpInput()
		{
			if (!jumpInput || player == null)
				return;
			jumpInput = false;

			player.playerLocomotionManager.AttemptToPerformJump();
		}
	}
}