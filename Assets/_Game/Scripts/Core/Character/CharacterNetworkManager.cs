using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ledsna
{
    public class CharacterNetworkManager : NetworkBehaviour
    {
        CharacterManager character;
        
        [Header("Position")]
        public NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>
            (Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>
            (Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public Vector3 networkPositionVelocity;
        public float networkPositionSmoothTime = 0.1f;
        public float networkRotationSmoothTime = 0.1f;
        
        [Header("Animation")]
        public NetworkVariable<float> horizontalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> verticalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> moveAmount = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Flags")]
        public NetworkVariable<bool> isSprinting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        [Header("Stats")]
        public NetworkVariable<int> endurance = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> currentStamina = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> maxStamina = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
        }
        
        // A SERVER RPC
        [ServerRpc]
        public void NotifyTheServerOfActionAnimationServerRpc(ulong clientId, string animationId, bool applyRootMotion)
        {
            if (IsServer)
            {
                PlayActionAnimationForAllClientsClientRpc(clientId, animationId, applyRootMotion);
            }
            
        }

        [ClientRpc]
        public void PlayActionAnimationForAllClientsClientRpc(ulong clientId, string animationId, bool applyRootMotion)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                PerformActionAnimationFromServer(animationId, applyRootMotion);
            }
        }

        private void PerformActionAnimationFromServer(string animationId, bool applyRootMotion)
        {
            character.applyRootMotion = applyRootMotion;
            character.animator.CrossFade(animationId, 0.2f);
        }
    }
}