namespace MyGame.Combat
{
    public enum DamageType
    {
        None,

        // elemental
        Fire,
        Ice,
        Lightning,
        Poison,
        Elemental,
        Earth,
        Nature,
        Holy,

        // physical subtypes
        Slashing,
        Piercing,
        Blunt,
    }

    public enum DamageKind
    {
        Physical,
        Magical,
    }

    public enum SpellIntent
    {
        Damage, // deals damage, may also apply effects
        Heal, // restores HP (or mana), may also apply effects
        Buff, // applies positive effects, no damage/heal
        Debuff, // applies negative effects, no damage/heal
        Utility, // later: cleanse, dispel, swap, shield-only etc.
    }

    public enum EffectKind
    {
        StatModifier, // modifies StatModifiers
        DamageOverTime, // periodic damage ticks
        HealOverTime, // periodic heal ticks
        // later: Stun, Silence, Shield, Dispel, etc.
    }

    public enum EffectTickTiming
    {
        None, // for StatModifier
        OnOwnerAction, // tick when the affected actor takes an action
        // later: OnAnyAction, OnTurnStart, OnTurnEnd
    }
}
