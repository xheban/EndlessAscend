using System;

namespace MyGame.Combat
{
    public enum SpellVariableOverrideType
    {
        DamageKind = 0,
        DamageRangeType = 1,
        DamageType = 2,
        IgnoreDefenseFlat = 3,
        IgnoreDefensePercent = 4,
    }

    [Serializable]
    public struct SpellVariableOverride
    {
        public SpellVariableOverrideType type;

        // Used when type == DamageKind
        public DamageKind damageKind;

        // Used when type == DamageRangeType
        public DamageRangeType damageRangeType;

        // Used when type == DamageType
        public DamageType damageType;

        // Used when type == IgnoreDefenseFlat
        public int ignoreDefenseFlat;

        // Used when type == IgnoreDefensePercent (0..100)
        public int ignoreDefensePercent;
    }
}
