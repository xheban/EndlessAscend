using System;
using System.Collections.Generic;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;

namespace MyGame.Towers
{
    public static class TowerSaveMapper
    {
        // Your 6 tower IDs in one place
        private static readonly string[] AllTowers =
        {
            "TowerOfBeginnings",
            "TowerOfWisdom",
            "TowerOfLife",
            "TowerOfHardship",
            "TowerOfDeath",
            "EndlessTower",
        };

        /// <summary>
        /// Builds runtime tower progress from SaveData.
        /// Ensures all towers exist; only Beginnings unlocked by default.
        /// </summary>
        public static TowerRunProgress LoadFromSave(SaveData save)
        {
            var run = new TowerRunProgress();

            // Defaults
            foreach (var id in AllTowers)
            {
                run.SetFloor(id, 1);
                run.SetUnlocked(id, id == "TowerOfBeginnings");
            }

            if (save?.towers == null)
                return run;

            foreach (var t in save.towers)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.towerId))
                    continue;

                run.SetFloor(t.towerId, Mathf.Max(1, t.currentFloor));
                run.SetUnlocked(t.towerId, t.unlocked);
            }

            return run;
        }

        /// <summary>
        /// Writes runtime tower progress back into SaveData.
        /// </summary>
        public static void WriteToSave(TowerRunProgress run, SaveData save)
        {
            if (save == null)
                return;

            if (save.towers == null)
                save.towers = new List<SavedTowerProgress>();
            else
                save.towers.Clear();

            if (run == null)
                return;

            // Save all known towers (including defaults)
            foreach (var id in AllTowers)
            {
                save.towers.Add(
                    new SavedTowerProgress
                    {
                        towerId = id,
                        currentFloor = run.GetFloor(id),
                        unlocked = run.IsUnlocked(id),
                    }
                );
            }

            // If you ever add custom towers dynamically later,
            // you can also write any extra ids found in run.floors here.
        }
    }
}
