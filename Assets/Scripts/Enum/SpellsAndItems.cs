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

    public enum SpellType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
    }
}
