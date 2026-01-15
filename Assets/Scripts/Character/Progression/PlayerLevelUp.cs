using MyGame.Combat;
using MyGame.Save;
using UnityEngine;

namespace MyGame.Progression
{
    public static class PlayerLevelUp
    {
        // Replace with your real formula or config table later
        public static int GetXpRequiredForLevel(int level)
        {
            return Mathf.RoundToInt(50 * Mathf.Pow(1.25f, level - 1));
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

            while (true)
            {
                int required = GetXpRequiredForLevel(save.level);
                if (save.exp < required)
                    break;

                save.exp -= required;
                save.level += 1;
                gained += 1;

                // Optional: heal on level up
                save.currentHp = CombatStatCalculator.CalculateMaxHp(
                    save.finalStats,
                    save.level,
                    save.tier
                );
                save.currentMana = CombatStatCalculator.CalculateMaxMana(
                    save.finalStats,
                    save.level,
                    save.tier
                );
            }
            return gained;
        }
    }
}
