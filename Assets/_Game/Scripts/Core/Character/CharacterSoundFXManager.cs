using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ledsna
{
    public class CharacterSoundFXManager : MonoBehaviour
    {
        private AudioSource audioSource;
        
        protected virtual void Awake()
        {
            audioSource = GetComponent<AudioSource>(); 
        }

        public void PlayRollSoundFX()
        {
            if (audioSource != null && WorldSoundFXManager.instance != null && WorldSoundFXManager.instance.rollSFX != null)
            {
                audioSource.PlayOneShot(WorldSoundFXManager.instance.rollSFX);
            }
        }
    }
}
