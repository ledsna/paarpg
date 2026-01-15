using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ledsna
{
    public class CharacterAnimatorManager : MonoBehaviour
    {
        CharacterManager character;

        int horizontal;
        int vertical;

        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();

            vertical = Animator.StringToHash("Vertical");
            horizontal = Animator.StringToHash("Horizontal");
        }

        public void UpdateAnimatorMovementParameters(float horizontalMovement, float verticalMovement, bool isSprinting)
        {
            var horizontalAmount = horizontalMovement;
            var verticalAmount = verticalMovement;

            if (isSprinting)
            {
                verticalAmount = 2;
            }

            if (character.animator == null)
                return;
            
            character.animator.SetFloat(vertical, verticalAmount, 0.1f, Time.deltaTime);
            character.animator.SetFloat(horizontal, horizontalAmount, 0.1f, Time.deltaTime);
        }

        public virtual void PlayTargetActionAnimation(
            string targetAnimation,
            bool isPerformingAction,
            bool applyRootMotion = true,
            bool canRotate  = false,
            bool canMove = false)
        {
            character.applyRootMotion = applyRootMotion;
            character.animator.CrossFade(targetAnimation, 0.2f);
            character.isPerformingAction = isPerformingAction;

            character.canRotate = canRotate;
            character.canMove = canMove;
            
            // TELL THE SERVER WE WANT TO PERFORM ANIMATION (skip in scene testing mode)
            if (!character.sceneTestingMode && NetworkManager.Singleton != null)
            {
                character.characterNetworkManager.NotifyTheServerOfActionAnimationServerRpc(NetworkManager.Singleton.LocalClientId, targetAnimation, applyRootMotion);
            }
        }
    }
}
