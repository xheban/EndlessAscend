using System;
using System.Collections.Generic;
using System.IO;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Save
{
    public static class SaveService
    {
        private const int MaxSlots = 5;
        private const string FilePrefix = "save_slot_";
        private const string FileExt = ".json";

        private static string GetSlotPath(int slot)
        {
            slot = Mathf.Clamp(slot, 1, MaxSlots);
            var fileName = $"{FilePrefix}{slot}{FileExt}";
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        public static bool HasSaveInSlot(int slot) => File.Exists(GetSlotPath(slot));

        public static SaveData LoadSlotOrNull(int slot)
        {
            var path = GetSlotPath(slot);
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                    return null;

                // --------------------
                // Migration / defaults
                // --------------------

                // v2 -> v3: introduced unspentStatPoints
                if (data.version < 3)
                {
                    data.unspentStatPoints = 0;
                    data.version = 3;
                }

                // v3 -> v4: introduced totalPlayTimeSeconds
                if (data.version < 4)
                {
                    data.totalPlayTimeSeconds = 0;
                    data.version = 4;
                }

                // v4 -> v5: introduced level/exp
                if (data.version < 5)
                {
                    data.level = 1;
                    data.exp = 0;
                    data.expToNextLevel = 100;
                    data.version = 5;
                }

                if (data.version < 6)
                {
                    if (data.spells == null)
                        data.spells = new List<SavedSpellEntry>();

                    data.version = 6;
                }

                if (data.version < 7)
                {
                    if (data.spells == null)
                        data.spells = new List<SavedSpellEntry>();

                    // activeSlotIndex defaults to -1 automatically (int default is 0, but our field is -1 in new saves)
                    // For old saves, force all spells to inactive to avoid accidental "slot 0 active" behavior.
                    foreach (var s in data.spells)
                    {
                        if (s != null)
                            s.activeSlotIndex = -1;
                    }

                    data.version = 7;
                }

                if (data.version < 8)
                {
                    if (data.towers == null)
                        data.towers = new List<SavedTowerProgress>();

                    // Ensure all 6 towers exist
                    EnsureTower(data, "TowerOfBeginnings", unlocked: true, startFloor: 1);
                    EnsureTower(data, "TowerOfWisdom", unlocked: false, startFloor: 1);
                    EnsureTower(data, "TowerOfLife", unlocked: false, startFloor: 1);
                    EnsureTower(data, "TowerOfHardship", unlocked: false, startFloor: 1);
                    EnsureTower(data, "TowerOfDeath", unlocked: false, startFloor: 1);
                    EnsureTower(data, "EndlessTower", unlocked: false, startFloor: 1);

                    data.version = 8;
                }

                if (data.version < 9)
                {
                    data.currentHp = 0;
                    data.currentMana = 0;
                    data.version = 9;
                }

                if (data.version < 10)
                {
                    data.gold = new Economy.Currency(0);
                    data.version = 10;
                }

                if (data.version < 11)
                {
                    data.playerIconId = "default";
                    data.version = 11;
                }

                if (data.version < 12)
                {
                    data.tier = Tier.Tier1;
                    data.version = 12;
                }

                if (data.version < 13)
                {
                    data.spellActiveSlotProgress = new();
                    data.version = 13;
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Load slot {slot} failed: {e}");
                return null;
            }
        }

        public static void SaveToSlot(int slot, SaveData data)
        {
            try
            {
                if (data == null)
                {
                    Debug.LogError("[SaveService] SaveToSlot called with null data.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(data.saveId))
                    data.saveId = Guid.NewGuid().ToString("N");

                if (string.IsNullOrWhiteSpace(data.createdUtc))
                    data.createdUtc = DateTime.UtcNow.ToString("O");

                data.lastSavedUtc = DateTime.UtcNow.ToString("O");

                // Ensure version is at least current
                if (data.version < 4)
                    data.version = 4;

                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(GetSlotPath(slot), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Save slot {slot} failed: {e}");
            }
        }

        private static void EnsureTower(
            SaveData data,
            string towerId,
            bool unlocked,
            int startFloor
        )
        {
            if (data.towers == null)
                data.towers = new List<SavedTowerProgress>();

            var existing = data.towers.Find(t => t != null && t.towerId == towerId);
            if (existing != null)
                return;

            data.towers.Add(
                new SavedTowerProgress
                {
                    towerId = towerId,
                    unlocked = unlocked,
                    currentFloor = startFloor,
                }
            );
        }

        /// <summary>
        /// Creates a new save in the first available free slot (1..5).
        /// Returns true + outSlot if successful, false if no free slots.
        /// </summary>
        public static bool TryCreateNewSave(SaveData data, out int outSlot)
        {
            outSlot = -1;

            for (int slot = 1; slot <= MaxSlots; slot++)
            {
                if (!HasSaveInSlot(slot))
                {
                    SaveToSlot(slot, data);
                    outSlot = slot;
                    return true;
                }
            }

            return false; // all 5 slots full
        }

        public static void DeleteSlot(int slot)
        {
            try
            {
                var path = GetSlotPath(slot);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Delete slot {slot} failed: {e}");
            }
        }

        /// <summary>
        /// Returns a list of slots that currently have saves.
        /// </summary>
        public static List<int> GetExistingSlots()
        {
            var result = new List<int>(MaxSlots);
            for (int slot = 1; slot <= MaxSlots; slot++)
            {
                if (HasSaveInSlot(slot))
                    result.Add(slot);
            }
            return result;
        }
    }
}
