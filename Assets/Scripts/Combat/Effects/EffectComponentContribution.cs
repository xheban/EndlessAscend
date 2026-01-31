using System;
using MyGame.Common;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class EffectComponentContribution
    {
        public int componentIndex;
        public EffectKind kind;
        public EffectTickTiming tickTiming;
        public int tickValue;
        public int instantValue;

        // Combat stat modifiers
        public int statFlatApplied;
        public int statPercentApplied;

        // Base / derived stat modifiers
        public bool hasBaseStatApplied;
        public BaseStatModifier baseStatApplied;

        public bool hasDerivedStatApplied;
        public DerivedStatModifier derivedStatApplied;
    }
}
