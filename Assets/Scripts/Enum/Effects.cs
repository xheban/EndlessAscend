namespace MyGame.Common
{
    public enum EffectMagnitudeBasis
    {
        None = 0,

        // % based on the attacker’s computed attack damage (your stat system)
        AttackDamage,

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

    public enum MagnitudeStackMode
    {
        None, // stacks only exist for rules/visuals, magnitude doesn't change
        AddPercentOfInitialPerStack, // your example: +X% of initial magnitude per additional stack
        AddFlatPerStack, // optional: +N per stack (handy for some effects)
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
}
