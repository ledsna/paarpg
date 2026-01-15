using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

namespace Ledsna
{
    public class SaveFileDataWriter
    {
        public string saveDataDirectoryPath = "";
        public string saveFileName = "";

        public bool CheckToSeeIfFileExists()
        {
            if (File.Exists(Path.Combine(saveDataDirectoryPath, saveFileName)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void DeleteSaveFile()
        {
            File.Delete(Path.Combine(saveDataDirectoryPath, saveFileName));
        }

        public void CreateNewCharacterSaveFile(CharacterSaveData characterData)
        {
            string savePath = Path.Combine(saveDataDirectoryPath, saveFileName);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                Debug.Log("CREATING SAVE FILE, AT SAVE PATH: " + savePath);

                string dataToStore = JsonUtility.ToJson(characterData, true);

                using (FileStream stream = new FileStream(savePath, FileMode.Create))
                {
                    using (StreamWriter fileWriter = new StreamWriter(stream))
                    {
                        fileWriter.Write(dataToStore);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("ERROR WHILE TRYING TO SAVE CHARACTER DATA, GAME NOT SAVED " + Path.GetDirectoryName(savePath) + "\n" + ex);
            }
        }

        public CharacterSaveData LoadSaveFile()
        {
            CharacterSaveData characterData = null;
            
            string loadPath = Path.Combine(saveDataDirectoryPath, saveFileName);
            if (File.Exists(loadPath))
            {
                try
                {
                    string dataToLoad = "";

                    using (FileStream stream = new FileStream(loadPath, FileMode.Open))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            dataToLoad = reader.ReadToEnd();
                        }
                    }
                    characterData = JsonUtility.FromJson<CharacterSaveData>(dataToLoad);
                }
                catch (Exception)
                {
                    characterData = null;
                }
            }
            
            return characterData;
        }
    }
}