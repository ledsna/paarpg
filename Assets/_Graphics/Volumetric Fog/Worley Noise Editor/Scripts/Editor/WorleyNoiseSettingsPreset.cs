using System;
using UnityEngine;

namespace WorleyNoise.Editor
{
    [CreateAssetMenu(fileName = "WorleyNoiseSettingsPreset", menuName = "Volumetric Fog/Worley Noise Preset")]
    public class WorleyNoiseSettingsPreset : ScriptableObject
    {
        public WorleyNoiseSettings channelR;

        public WorleyNoiseSettings channelG;

        public WorleyNoiseSettings channelB;

        public WorleyNoiseSettings channelA;

        public WorleyNoiseSettings this[WorleyNoiseEditor.TextureChannel channel]
        {
            get
            {
                switch (channel)
                {
                    case WorleyNoiseEditor.TextureChannel.R:
                        return channelR;
                    case WorleyNoiseEditor.TextureChannel.G:
                        return channelG;
                    case WorleyNoiseEditor.TextureChannel.B:
                        return channelB;
                    case WorleyNoiseEditor.TextureChannel.A:
                        return channelA;
                    default:
                        return null;
                }
            }
        }

        public event Action ValueWasChanged;

        public void OnValidate()
        {
            ValueWasChanged?.Invoke();
        }
    }
}