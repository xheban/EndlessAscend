using System;
using MyGame.Common;

namespace MyGame.Combat
{
    public enum SpellCombatModifierTarget
    {
        None = 0,

        // ---------- Flats ----------
        AttackPowerFlat = 10,
        MagicPowerFlat = 11,
        PowerFlat = 12,

        SpellBaseFlat = 20,
        MagicSpellBaseFlat = 21,
        PhysicalSpellBaseFlat = 22,

        DamageFlat = 30,
        MagicDamageFlat = 31,
        AttackDamageFlat = 32,

        DefenceFlat = 40,
        PhysicalDefenseFlat = 41,
        MagicDefenseFlat = 42,

        AttackSpeedFlat = 50,
        CastingSpeedFlat = 51,

        PowerScalingFlat = 60,
        AttackPowerScalingFlat = 61,
        MagicPowerScalingFlat = 62,

        // ---------- More/Less buckets (Percent) ----------
        PowerMorePercent = 100,
        PowerLessPercent = 101,

        AttackPowerMorePercent = 110,
        AttackPowerLessPercent = 111,

        MagicPowerMorePercent = 120,
        MagicPowerLessPercent = 121,

        PowerScalingMorePercent = 130,
        PowerScalingLessPercent = 131,

        AttackPowerScalingMorePercent = 140,
        AttackPowerScalingLessPercent = 141,

        MagicPowerScalingMorePercent = 150,
        MagicPowerScalingLessPercent = 151,

        DamageMorePercent = 200,
        DamageLessPercent = 201,

        PhysicalDamageMorePercent = 210,
        PhysicalDamageLessPercent = 211,

        MagicDamageMorePercent = 220,
        MagicDamageLessPercent = 221,

        SpellBaseMorePercent = 230,
        SpellBaseLessPercent = 231,

        MagicSpellBaseMorePercent = 240,
        MagicSpellBaseLessPercent = 241,

        PhysicalSpellBaseMorePercent = 250,
        PhysicalSpellBaseLessPercent = 251,

        HitChanceMorePercent = 260,
        HitChanceLessPercent = 261,

        CastingSpeedMorePercent = 270,
        CastingSpeedLessPercent = 271,

        AttackSpeedMorePercent = 280,
        AttackSpeedLessPercent = 281,

        DefenceMorePercent = 290,
        DefenceLessPercent = 291,

        PhysicalDefenceMorePercent = 300,
        PhysicalDefenceLessPercent = 301,

        MagicDefenceMorePercent = 310,
        MagicDefenceLessPercent = 311,

        // ---------- Type-based arrays ----------
        AttackerBonusFlatByType = 400,
        AttackerBonusMorePercentByType = 401,

        DefenderVulnerabilityFlatByType = 410,
        DefenderVulnerabilityMorePercentByType = 411,

        DefenderResistanceFlatByType = 420,
        DefenderResistanceLessPercentByType = 421,

        AttackerWeakenFlatByType = 430,
        AttackerWeakenLessPercentByType = 431,

        // ---------- Range-based ----------
        AttackerRangeBonusFlatByRange = 500,
        AttackerRangeBonusMorePercentByRange = 501,
    }

    public enum SpellModifierScope
    {
        Any = 0,
        DamageKind = 1,
        DamageType = 2,
        DamageRangeType = 3,
    }

    [Serializable]
    public struct SpellCombatModifier
    {
        // Preferred (new): direct target mapping to StatModifiers.
        public SpellCombatModifierTarget target;

        // Legacy (kept for backwards compatibility).
        public SpellModifierScope scope;

        // Used when scope == DamageKind
        public DamageKind damageKind;

        // Used when scope == DamageType
        public DamageType damageType;

        // Used when scope == DamageRangeType
        public DamageRangeType damageRangeType;

        public ModOp op;

        // Flat: whole-number-ish. Percent: 10 means +10%.
        public float value;
    }
}
