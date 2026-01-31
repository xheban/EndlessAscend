using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;

namespace MyGame.Progression
{
    public static class PlayerLevelUp
    {
        public static event Action<int, List<string>> PlayerLeveledUp;

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
            List<string> newlyUnlocked = null;

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

                var unlockedThisLevel = ApplyLevelUnlocks(save);
                if (unlockedThisLevel != null && unlockedThisLevel.Count > 0)
                {
                    newlyUnlocked ??= new List<string>();
                    newlyUnlocked.AddRange(unlockedThisLevel);
                }
            }
            if (gained > 0)
                PlayerLeveledUp?.Invoke(gained, newlyUnlocked);
            return gained;
        }

        private static List<string> ApplyLevelUnlocks(SaveData save)
        {
            if (save == null)
                return null;

            var db = GameConfigProvider.Instance?.UnlockDatabase;
            if (db == null || db.All == null)
                return null;

            save.unlockedIds ??= new List<string>();
            List<string> added = null;

            var all = db.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrWhiteSpace(def.unlockId))
                    continue;

                if (def.requiredLevel > save.level)
                    continue;

                if (HasUnlockId(save.unlockedIds, def.unlockId))
                    continue;

                save.unlockedIds.Add(def.unlockId);
                added ??= new List<string>();
                added.Add(def.unlockId);
            }
            return added;
        }

        private static bool HasUnlockId(List<string> ids, string unlockId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(unlockId))
                return false;

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], unlockId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
