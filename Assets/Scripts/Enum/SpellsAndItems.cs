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
}
