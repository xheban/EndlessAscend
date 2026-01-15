using System;
using MyGame.Common;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class EffectContributor
    {
        public string sourceSpellId;
        public CombatActorType sourceActor;
        public int remainingTurns;
        public int totalTickValue;
        public int strengthRating;
        public RemoveWhenType removedWhenType;
        public int statFlatApplied;
        public int statPercentApplied;
    }
}
