namespace MyGame.Common
{
    public enum EffectMagnitudeBasis
    {
        None = 0,

        // % based on the attacker’s computed attack damage (your stat system)
        Power,

        // % based on the actual damage dealt by the triggering hit
        DamageDealt,

        // % based on target max values
        MaxHealth,
        MaxMana,

        // % based on target's most recent damage taken
        LastDamageTaken,

        // Optional future bases:
        // CurrentHealth,
        // SpellBaseDamage,
    }

    public enum DurationStackMode
    {
        Refresh, // set remaining to max(remaining, newDuration) OR just newDuration (choose)
        Prolong, // remaining += newDuration (or += prolongAmountPerStack)
        None, // do not change remaining duration when re-applied
    }

    public enum EffectReapplyRule
    {
        DoNothingIfPresent, // if Burn already active, ignore this application
        AddOnTop, // keep existing AND also add another "layer" of Burn
        OverwriteIfStronger, // replace the existing Burn if this one is stronger
    }

    public enum EffectStrengthCompareMode
    {
        ByStrengthRating, // use the number you set in the instance (safe without combat data)
        ByComputedMagnitude, // later: compare actual computed initial magnitude (when you build combat)
    }

    public enum ScalingType
    {
        None,
        Step,
        Milestone,
    }

    public enum RemoveWhenType
    {
        DurationOfLastStackEnds,
        DurationEnds,
    }

    public enum EffectPolarity
    {
        Buff,
        Debuff,
    }

    public enum EffectTarget
    {
        Enemy, // apply to spell target (defender)
        Self, // apply to caster (attacker)
    }

    public enum EffectOp
    {
        Flat, // uses component magnitudeFlat
        Percent, // uses component magnitudePercent: +20 => *1.2
    }

    public enum EffectKind
    {
        StatModifier, // modifies StatModifiers
        DamageOverTime, // periodic damage ticks
        HealOverTime, // periodic heal ticks
        DirectDamage, // immediate damage on apply
        DirectHeal, // immediate heal on apply
        BaseStatModifier, // modifies base stats (STR/AGI/etc)
        DerivedStatModifier, // modifies derived stats (maxHP, power, speeds, etc)
        Composite, // multiple components bundled under one icon
        // later: Stun, Silence, Shield, Dispel, etc.
    }

    public enum EffectTickTiming
    {
        None, // for StatModifier
        OnOwnerAction, // tick when the affected actor takes an action
        // later: OnAnyAction, OnTurnStart, OnTurnEnd
    }

    public enum EffectStat
    {
        // Generic damage buckets
        None = 0,
        DamageAll = 1,
        DamagePhysical = 2,
        DamageMagic = 3,

        // Generic power buckets (if you use them in damage calc)
        PowerAll = 4,

        // Spell base damage buckets
        SpellBaseAll = 5,
        SpellBasePhysical = 6,
        SpellBaseMagic = 7,

        // Common combat buckets
        DefenceAll = 8,

        // Type-based layers (uses EffectDefinition.damageType)
        AttackerBonusByType = 9, // outgoing MORE by type (your attackerBonus arrays)
        AttackerWeakenByType = 10, // outgoing LESS by type (your attackerWeaken arrays)
        DefenderVulnerabilityByType = 11, // damage taken MORE by type (your defenderVuln arrays)
        DefenderResistByType = 12, // damage taken LESS by type (your defenderResist arrays)

        // Power scaling (flat uses percent-points, e.g. 20 => +0.20)
        PowerScalingAll = 13,
        PowerScalingPhysical = 14,
        PowerScalingMagic = 15,

        // Range-based flat/% bonuses (applied by spell damage range)
        MeleeDamageBonus = 16,
        RangedDamageBonus = 17,
    }
}
