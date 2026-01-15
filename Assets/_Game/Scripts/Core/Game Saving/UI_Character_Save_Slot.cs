using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Ledsna
{
    public class UI_Character_Save_Slot : MonoBehaviour
    {
        SaveFileDataWriter saveFileWriter;

        [Header("Game Slot")] 
        public CharacterSlot characterSlot;
        
        [Header("Character Info")]
        public TextMeshProUGUI characterName;
        public TextMeshProUGUI timePlayed;

        private void OnEnable()
        {
            LoadSaveSlots();
        }

        private void LoadSaveSlots()
        {
            saveFileWriter = new SaveFileDataWriter();
            saveFileWriter.saveDataDirectoryPath = Application.persistentDataPath;

            switch (characterSlot)
            {
                case CharacterSlot.CharacterSlot_01:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot01.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_02:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot02.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_03:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot03.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_04:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot04.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_05:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot05.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_06:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot06.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_07:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot07.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_08:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot08.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_09:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot09.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                case CharacterSlot.CharacterSlot_10:
                    saveFileWriter.saveFileName =
                        WorldSaveGameManager.instance.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(
                            characterSlot);

                    if (saveFileWriter.CheckToSeeIfFileExists())
                    {
                        characterName.text = WorldSaveGameManager.instance.characterSlot10.characterName;
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    break;
                default:
                    break;
            }
        }

        public void LoadGameFromCharacterSlot()
        {
            WorldSaveGameManager.instance.currentCharacterSlotBeingUsed = characterSlot;
            WorldSaveGameManager.instance.LoadGame();
        }

        public void SelectCurrentSlot()
        {
            TitleScreenManager.instance.SelectCharacterSlot(characterSlot);
        }
    }
}
