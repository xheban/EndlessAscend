namespace MyGame.Common
{
    public enum EffectMagnitudeBasis
    {
        None = 0,

        // % based on the attacker’s computed attack damage (your stat system)
        Power,

        // % based on the actual damage dealt by the triggering hit
        DamageDealt,

        // Optional future bases:
        // MaxHealth,
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
        Flat, // uses EffectInstance magnitudeFlat
        MorePercent, // uses magnitudePercent: +20 => *1.2
        LessPercent, // uses magnitudePercent: 20 => *0.8
    }

    public enum EffectStat
    {
        // Generic damage buckets
        None,
        DamageAll,
        DamagePhysical,
        DamageMagic,

        // Generic power buckets (if you use them in damage calc)
        PowerAll,
        PowerAttack,
        PowerMagic,

        // Spell base damage buckets
        SpellBaseAll,
        SpellBasePhysical,
        SpellBaseMagic,

        // Common combat buckets
        HitChance,
        AttackSpeed,
        CastingSpeed,

        DefenceAll,
        DefencePhysical,
        DefenceMagic,

        // Type-based layers (uses EffectDefinition.damageType)
        AttackerBonusByType, // outgoing MORE by type (your attackerBonus arrays)
        AttackerWeakenByType, // outgoing LESS by type (your attackerWeaken arrays)
        DefenderVulnerabilityByType, // damage taken MORE by type (your defenderVuln arrays)
        DefenderResistByType, // damage taken LESS by type (your defenderResist arrays)
    }
}
