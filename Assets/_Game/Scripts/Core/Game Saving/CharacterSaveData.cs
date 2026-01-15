using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Ledsna
{
    [System.Serializable]
    public class CharacterSaveData
    {
        [Header("SCENE INDEX")] 
        public int sceneIndex = 1;
        
        [Header("Character Name")]
        public string characterName = "Character";
        
        [Header("Time Played")] 
        public int secondsPlayed;
        
        [Header("World Coordinates")] 
        public float xPosition;
        public float yPosition = 5;
        public float zPosition;
    }
}