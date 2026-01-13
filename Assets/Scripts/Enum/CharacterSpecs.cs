using System;

namespace MyGame.Common
{
    [Flags]
    public enum CharacterSpec
    {
        None = 0,

        Elementalist = 1 << 0,
        Summoner = 1 << 1,
        Berserker = 1 << 2,
        ShieldBearer = 1 << 3,
        Assassin = 1 << 4,
        Sharpshooter = 1 << 5,

        All = ~0,
    }
}
