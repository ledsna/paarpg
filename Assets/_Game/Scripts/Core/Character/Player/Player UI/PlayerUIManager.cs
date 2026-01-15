using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


namespace Ledsna
{
    public class PlayerUIManager : MonoBehaviour
    {
        public static PlayerUIManager instance;

        [Header("NETWORK JOIN")]
        [SerializeField] bool startGameAsClient;

        [HideInInspector] public PlayerUIHUDManager playerUIHUDManager;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
            playerUIHUDManager = GetComponentInChildren<PlayerUIHUDManager>();
        }
        
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (startGameAsClient)
            {
                startGameAsClient = false;
                // WE MUST FIRST SHUT DOWN THE NETWORK AS A HOST
                NetworkManager.Singleton.Shutdown();
                // WE THEN START THE NETWORK AS A CLIENT
                NetworkManager.Singleton.StartClient();
            }
        }
    }
}
