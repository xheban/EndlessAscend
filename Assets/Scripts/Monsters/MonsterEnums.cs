using System;

public enum MonsterRarity
{
    Common,
    Uncommon,
    Rare,
    Elite,
    SpecialElite,
    Lord,
    HighLord,
    GrandLord,
    Mythical,
    Primal,
    God,
}

[Flags]
public enum MonsterTag
{
    None = 0,

    // Creature types
    Beast = 1 << 0,
    Undead = 1 << 1,
    Humanoid = 1 << 2,
    Demon = 1 << 3,
    Dragon = 1 << 4,
    Construct = 1 << 5,
    Spirit = 1 << 6,
    Ghost = 1 << 7,

    // Elements
    Fire = 1 << 8,
    Water = 1 << 9,
    Earth = 1 << 10,
    Air = 1 << 11,
    Lightning = 1 << 12,
    Ice = 1 << 13,
    Poison = 1 << 14,
    Shadow = 1 << 15,
    Light = 1 << 16,

    // Special traits
    Elemental = 1 << 17,
    Mechanical = 1 << 18,
    Ancient = 1 << 19,
}
