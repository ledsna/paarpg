using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Ledsna
{
    public class TitleScreenLoadMenuInputManager : MonoBehaviour
    {
        PlayerControls playerControls;
        
        [Header("Title Screen Inputs")]
        [SerializeField] bool deleteCharacterSlot = false;

        private void Update()
        {
            if (deleteCharacterSlot)
            {
                deleteCharacterSlot = false;
                TitleScreenManager.instance.AttemptToDeleteCharacterSlot();
            }
        }

        private void OnEnable()
        {
            if (playerControls == null)
            {
                playerControls = new PlayerControls();
                playerControls.UI.Delete.performed += i => deleteCharacterSlot = true;
            }

            playerControls.Enable();
        }
        
        private void OnDisable()
        {
            playerControls.Disable();
        }
    }
}
