using System;

namespace MyGame.Common
{
    [Serializable]
    public struct EffectInstanceScaledIntValues
    {
        public int chancePercent;
        public int durationTurns;

        public int magnitudeFlat;
        public int magnitudePercent;

        public EffectMagnitudeBasis magnitudeBasis;

        public bool stackable;
        public bool mergeable;

        public int maxStacks;
    }
}
