using UnityEngine;

namespace Grass.Core
{
    /// <summary>
    /// ScriptableObject wrapper for grass binary data
    /// This properly preserves binary data in builds, unlike TextAsset
    /// </summary>
    public class GrassDataAsset : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private byte[] binaryData;

        public byte[] Data
        {
            get => binaryData;
            set => binaryData = value;
        }

        public int Length => binaryData?.Length ?? 0;
    }
}
