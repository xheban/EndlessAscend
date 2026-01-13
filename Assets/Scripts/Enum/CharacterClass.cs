using System;

namespace MyGame.Common
{
    [Flags]
    public enum CharacterClass
    {
        None = 0,
        Mage = 1 << 0,
        Warrior = 1 << 1,
        Ranger = 1 << 2,

        All = Mage | Warrior | Ranger,
    }
}
