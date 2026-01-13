using System;

namespace MyGame.Rewards
{
    public readonly struct CombatRewardResult
    {
        public readonly int exp;
        public readonly int gold;
        public readonly LootItem[] loot;

        public CombatRewardResult(int exp, int gold, LootItem[] items)
        {
            this.exp = exp;
            this.gold = gold;
            this.loot = items;
        }

        public static CombatRewardResult None()
        {
            return new CombatRewardResult(0, 0, Array.Empty<LootItem>());
        }

        public sealed class LootItem
        {
            public string lootId; // e.g. "potion_small"
            public int stackCount; // e.g. 3
        }
    }
}
