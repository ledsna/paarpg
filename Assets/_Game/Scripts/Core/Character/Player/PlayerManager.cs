using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ledsna
{
    public class PlayerManager : CharacterManager
    {
        [HideInInspector] public PlayerAnimatorManager playerAnimatorManager;
        [HideInInspector] public PlayerLocomotionManager playerLocomotionManager;
        [HideInInspector] public PlayerNetworkManager playerNetworkManager;
        [HideInInspector] public PlayerStatsManager playerStatsManager;

        protected override void Awake()
        {
            base.Awake();

            playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            playerNetworkManager = GetComponent<PlayerNetworkManager>();
            playerStatsManager = GetComponent<PlayerStatsManager>();
        }

        protected override void Update()
        {
            base.Update();

            bool isLocalOwner = sceneTestingMode || IsOwner;
            if (!isLocalOwner)
                return;

            playerLocomotionManager.HandleAllMovement();

            playerStatsManager.RegenerateStamina();

            // Update UI manually in scene testing mode (since network callbacks won't fire)
            if (sceneTestingMode && PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHUDManager != null)
            {
                PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue(0, playerNetworkManager.currentStamina.Value);
            }
        }

        protected override void LateUpdate()
        {
            bool isLocalOwner = sceneTestingMode || IsOwner;
            if (!isLocalOwner)
                return;

            base.LateUpdate();

            if (PlayerCamera.instance != null)
            {
                PlayerCamera.instance.HandleAllCameraActions();
                // if (PixelPerfectCamera.PixelPerfectCamera.instance != null)
                //     PixelPerfectCamera.PixelPerfectCamera.instance.ApplyPixelPerfect();
            }
        }

        protected void Start()
        {
            // In scene testing mode, set up connections immediately
            if (sceneTestingMode)
            {
                SetupSceneTestingMode();
            }
        }

        private void SetupSceneTestingMode()
        {
            // Connect camera
            if (PlayerCamera.instance != null)
            {
                PlayerCamera.instance.player = this;
                // Debug.Log("Scene Testing: Camera connected to player");
            }
            else
            {
                Debug.LogWarning("Scene Testing: PlayerCamera.instance is NULL! Make sure camera has PlayerCamera component.");
            }

            // Connect input manager
            if (PlayerInputManager.instance != null)
            {
                PlayerInputManager.instance.player = this;
                // Debug.Log("Scene Testing: Input manager connected to player");
            }
            else
            {
                Debug.LogError("Scene Testing: PlayerInputManager.instance is NULL! Make sure PlayerInputManager exists in scene and is enabled.");
            }

            // Initialize stamina without network callbacks
            if (playerNetworkManager != null && playerStatsManager != null)
            {
                playerNetworkManager.maxStamina.Value = playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(playerNetworkManager.endurance.Value);
                playerNetworkManager.currentStamina.Value = playerNetworkManager.maxStamina.Value;
                // Debug.Log($"Scene Testing: Stamina initialized - {playerNetworkManager.currentStamina.Value}/{playerNetworkManager.maxStamina.Value}");
            }

            // Initialize UI in scene testing mode
            if (PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHUDManager != null)
            {
                PlayerUIManager.instance.playerUIHUDManager.SetMaxStaminaValue(playerNetworkManager.maxStamina.Value);
                PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue(0, playerNetworkManager.currentStamina.Value);
                // Debug.Log("Scene Testing: UI initialized");
            }
            else
            {
                Debug.LogWarning("Scene Testing: PlayerUIManager or playerUIHUDManager is NULL!");
            }

            // Debug.Log("Scene Testing Mode: Player initialized without networking");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                PlayerCamera.instance.player = this;
                PlayerInputManager.instance.player = this;
                WorldSaveGameManager.instance.player = this;

                playerNetworkManager.currentStamina.OnValueChanged +=
                    PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue;
                playerNetworkManager.currentStamina.OnValueChanged += playerStatsManager.ResetStaminaRegenTimer;

                // THIS WILL BE MOVED WHEN SAVING IS ADDED
                playerNetworkManager.maxStamina.Value = playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(playerNetworkManager.endurance.Value);
                playerNetworkManager.currentStamina.Value = playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(playerNetworkManager.endurance.Value);
                PlayerUIManager.instance.playerUIHUDManager.SetMaxStaminaValue(playerNetworkManager.maxStamina.Value);
            }
        }

        public void SaveGameDataToCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            currentCharacterData.sceneIndex = SceneManager.GetActiveScene().buildIndex;
            currentCharacterData.characterName = playerNetworkManager.characterName.Value.ToString();
            currentCharacterData.xPosition = transform.position.x;
            currentCharacterData.yPosition = transform.position.y;
            currentCharacterData.zPosition = transform.position.z;
        }

        public void LoadGameDataFromCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            playerNetworkManager.characterName.Value = currentCharacterData.characterName;
            var myPosition = new Vector3(currentCharacterData.xPosition, currentCharacterData.yPosition, currentCharacterData.zPosition);
            transform.position = myPosition;
        }
    }
}