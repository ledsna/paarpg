using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace  Ledsna
{
    public class CharacterStatsManager : MonoBehaviour
    {
        private CharacterManager character;
        [Header("Stamina Regeneration")] [SerializeField]
        private float staminaRegenerationAmount = 2;
        private float staminaRegenerationTimer = 0;
        private float staminaTickTimer = 0;
        [SerializeField] private float staminaRegenerationDelay = 2;
        
        public int CalculateStaminaBasedOnEnduranceLevel(int endurance)
        {
            float stamina = endurance * 10;

            return Mathf.RoundToInt(stamina);
        }

        public virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
        }
        
        public virtual void RegenerateStamina()
        {
            // Allow stamina regeneration in scene testing mode OR for owner
            bool canRegenerate = character.sceneTestingMode || character.IsOwner;
            if (!canRegenerate)
                return;

            if (character.characterNetworkManager.isSprinting.Value)
                return;

            if (character.isPerformingAction)
                return;

            staminaRegenerationTimer += Time.deltaTime;

            if (staminaRegenerationTimer >= staminaRegenerationDelay)
            {
                if (character.characterNetworkManager.currentStamina.Value < character.characterNetworkManager.maxStamina.Value)
                {
                    staminaTickTimer += Time.deltaTime;

                    if (staminaTickTimer >= 0.1)
                    {
                        staminaTickTimer = 0;
                        character.characterNetworkManager.currentStamina.Value += staminaRegenerationAmount;
                    }
                }
            }
        }

        public virtual void ResetStaminaRegenTimer(float previousStaminaAmount, float currentStaminaAmount)
        {
            if (currentStaminaAmount < previousStaminaAmount)
            {
                staminaRegenerationTimer = 0;
            }
        }
    }
}

