using System;
using MyGame.Common;

namespace MyGame.Combat
{
    [Serializable]
    public struct CombatStatModifier
    {
        public EffectStat stat;
        public EffectOp op;

        // Optional: used for *ByType stats.
        public DamageType damageType;

        // Flat = whole number, Percent = whole percent (ex: 10 => +10%).
        public int value;
    }
}
