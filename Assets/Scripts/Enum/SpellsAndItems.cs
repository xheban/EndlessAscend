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

    public enum DamageRangeType
    {
        Melee,
        Ranged,
    }

    public enum SpellIntent
    {
        Damage, // deals damage, may also apply effects
        Heal, // restores HP (or mana), may also apply effects
        Buff, // applies positive effects, no damage/heal
        Debuff, // applies negative effects, no damage/heal
        Utility, // later: cleanse, dispel, swap, shield-only etc.
    }
}
