using System;
using UnityEngine;

namespace WorleyNoise.Editor
{
    [Serializable]
    public class WorleyNoiseSettings
    {
        public int seed;

        [Range(1, 50)]
        public int gridSizeA = 5;

        [Range(1, 50)]
        public int gridSizeB = 10;

        [Range(1, 50)]
        public int gridSizeC = 15;

        public float persistence = .5f;
        public int tile = 1;
        public bool invert = true;

        public WorleyNoiseSettings(WorleyNoiseSettings settings)
        {
            seed = settings.seed;
            gridSizeA = settings.gridSizeA;
            gridSizeB = settings.gridSizeB;
            gridSizeC = settings.gridSizeC;
            persistence = settings.persistence;
            tile = settings.tile;
            invert = settings.invert;
        }

        public override bool Equals(object obj)
        {
            if (obj is WorleyNoiseSettings settings)
                return settings.seed == seed &&
                       settings.gridSizeA == gridSizeA &&
                       settings.gridSizeB == gridSizeB &&
                       settings.gridSizeC == gridSizeC &&
                       settings.tile == tile &&
                       settings.invert == invert &&
                       Mathf.Abs(settings.persistence - persistence) < float.Epsilon;
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + seed.GetHashCode();
                hash = hash * 23 + gridSizeA.GetHashCode();
                hash = hash * 23 + gridSizeB.GetHashCode();
                hash = hash * 23 + gridSizeC.GetHashCode();
                hash = hash * 23 + persistence.GetHashCode();
                hash = hash * 23 + tile.GetHashCode();
                hash = hash * 23 + invert.GetHashCode();
                return hash;
            }
        }
    }
}