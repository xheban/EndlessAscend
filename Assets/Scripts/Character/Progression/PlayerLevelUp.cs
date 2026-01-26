using System;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Save;
using UnityEngine;

namespace MyGame.Progression
{
    public static class PlayerLevelUp
    {
        // Replace with your real formula or config table later
        public static long GetXpRequiredForLevel(int level)
        {
            level = Mathf.Max(1, level);

            // Tunable constants
            const double baseXp = 30.0; // baseline
            const double linear = 20.0; // early growth
            const double quadratic = 8.0; // mid growth
            const double cubic = 0.01; // late growth

            double l = level;

            double xp = baseXp + linear * l + quadratic * l * l + cubic * l * l * l * l; // quartic tail (VERY gentle)

            return (long)Math.Round(xp);
        }

        /// <summary>
        /// Applies level-ups if the save has enough XP. ..
        /// Returns how many levels were gained.
        /// </summary>
        public static int ApplyLevelUps(SaveData save)
        {
            if (save == null)
                return 0;

            int gained = 0;

            int pointsPerLevel = 4 * HelperFunctions.TierToFlatBonusMultiplier(save.tier);
            pointsPerLevel = Mathf.Max(0, pointsPerLevel);

            while (true)
            {
                long required = GetXpRequiredForLevel(save.level);
                if (save.exp < required)
                    break;

                save.exp -= required;
                save.level += 1;
                gained += 1;

                // Grant free stat points each level-up (scales with player tier)
                save.unspentStatPoints += pointsPerLevel;

                // Optional: heal on level up (use fully bonused max values)
                var derived = PlayerDerivedStatsResolver.BuildEffectiveDerivedStats(save);
                save.currentHp = Mathf.Max(1, derived.maxHp);
                save.currentMana = Mathf.Max(0, derived.maxMana);
            }
            return gained;
        }
    }
}
