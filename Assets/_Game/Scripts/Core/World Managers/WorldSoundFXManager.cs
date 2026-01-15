using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ledsna
{
    public class WorldSoundFXManager : MonoBehaviour
    {
        public static WorldSoundFXManager instance;

        [Header("Action SFX")]
        public AudioClip rollSFX;
        
        private void Awake()
        {
            if (instance is null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
