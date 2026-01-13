using System;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Rewards
{
    public sealed class DefaultCombatRewardCalculator : ICombatRewardCalculator
    {
        public CombatRewardResult Calculate(MonsterDefinition monster, int monsterLevel)
        {
            int exp = CalculateExp(monster, monsterLevel);
            int gold = CalculateGold(monster, monsterLevel);
            return new CombatRewardResult(exp, gold, Array.Empty<CombatRewardResult.LootItem>());
        }

        private int CalculateExp(MonsterDefinition monster, int level)
        {
            int exp = monster.BaseExp;

            //exp += level * 2; // simple scaling
            //exp += TierToFlatBonus(monster.Tier, perTier: 10);

            exp *= monster.Rarity switch
            {
                MonsterRarity.Common => 1,
                MonsterRarity.Uncommon => 1,
                MonsterRarity.Rare => 1,
                MonsterRarity.Elite => 1,
                MonsterRarity.GrandLord => 1,
                _ => 1,
            };

            return Mathf.Max(1, exp);
        }

        private int CalculateGold(MonsterDefinition monster, int level)
        {
            int gold = monster.BaseGold;

            //gold += level; // simple scaling
            //gold += TierToFlatBonus(monster.Tier, perTier: 5);

            gold *= monster.Rarity switch
            {
                MonsterRarity.Common => 1,
                MonsterRarity.Uncommon => 1,
                MonsterRarity.Rare => 1,
                MonsterRarity.Elite => 1,
                MonsterRarity.GrandLord => 1,
                _ => 1,
            };

            return Mathf.Max(0, gold);
        }

        private static int TierToFlatBonus(Tier tier, int perTier)
        {
            int tierIndex = tier switch
            {
                Tier.Tier1 => 0,
                Tier.Tier2 => 1,
                Tier.Tier3 => 2,
                Tier.Tier4 => 3,
                Tier.Tier5 => 4,
                Tier.Tier6 => 5,
                _ => 0,
            };

            return tierIndex * perTier;
        }
    }
}
