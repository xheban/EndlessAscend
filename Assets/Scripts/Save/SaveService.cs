using System;
using System.Collections.Generic;
using System.IO;
using MyGame.Combat;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Save
{
    public static class SaveService
    {
        private const int CurrentSaveVersion = 19;
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
                    data.avatarId = "default";
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

                if (data.version < 14)
                {
                    // new settings container + default spell bindings
                    data.version = 14;
                }

                if (data.version < 15)
                {
                    if (data.items == null)
                        data.items = new List<SavedItemStackEntry>();

                    if (data.equipmentInstances == null)
                        data.equipmentInstances = new List<SavedEquipmentInstance>();

                    if (data.equippedSlots == null)
                        data.equippedSlots = new List<SavedEquippedSlot>();

                    // Ensure roll lists exist on equipment instances (older saves)
                    if (data.equipmentInstances != null)
                    {
                        foreach (var e in data.equipmentInstances)
                        {
                            if (e == null)
                                continue;

                            e.rolledBaseStatMods ??= new List<BaseStatModifier>();
                            e.rolledDerivedStatMods ??= new List<DerivedStatModifier>();
                            e.rolledSpellMods ??= new List<SpellCombatModifier>();
                            e.rolledSpellOverrides ??= new List<SpellVariableOverride>();
                        }
                    }

                    data.version = 15;
                }

                if (data.version < 16)
                {
                    data.equipmentInventorySlots ??= new List<SavedEquipmentInventorySlotEntry>();
                    data.version = 16;
                }

                if (data.version < 17)
                {
                    // Rolled modifier values are now stored as ints.
                    // No migration needed (legacy float values will be dropped/rounded by Unity deserialization).
                    data.version = 17;
                }

                if (data.version < 18)
                {
                    data.activeCombatSlots ??= new List<SavedCombatActiveSlotEntry>();

                    // Ensure exactly 4 entries exist (Slot1..Slot4)
                    while (data.activeCombatSlots.Count < 4)
                        data.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

                    data.version = 18;
                }

                if (data.version < 19)
                {
                    data.persistentItemCooldowns ??= new List<SavedItemCooldownEntry>();
                    data.version = 19;
                }

                NormalizeInventoryRollLists(data);

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

                if (data.version < CurrentSaveVersion)
                    data.version = CurrentSaveVersion;

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

        private static void NormalizeInventoryRollLists(SaveData data)
        {
            if (data == null)
                return;

            data.items ??= new List<SavedItemStackEntry>();
            data.equipmentInstances ??= new List<SavedEquipmentInstance>();
            data.equippedSlots ??= new List<SavedEquippedSlot>();
            data.activeCombatSlots ??= new List<SavedCombatActiveSlotEntry>();
            data.persistentItemCooldowns ??= new List<SavedItemCooldownEntry>();

            while (data.activeCombatSlots.Count < 4)
                data.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

            if (data.equipmentInstances == null)
                return;

            for (int i = 0; i < data.equipmentInstances.Count; i++)
            {
                var e = data.equipmentInstances[i];
                if (e == null)
                    continue;

                e.rolledBaseStatMods ??= new List<BaseStatModifier>();
                e.rolledDerivedStatMods ??= new List<DerivedStatModifier>();
                e.rolledSpellMods ??= new List<SpellCombatModifier>();
                e.rolledSpellOverrides ??= new List<SpellVariableOverride>();
            }
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

        private static KeyCode DefaultKeyForSpellSlot(int slotIndex)
        {
            // Typical defaults: 1..0 for first 10, then - and =
            return slotIndex switch
            {
                0 => KeyCode.Alpha1,
                1 => KeyCode.Alpha2,
                2 => KeyCode.Alpha3,
                3 => KeyCode.Alpha4,
                4 => KeyCode.Alpha5,
                5 => KeyCode.Alpha6,
                6 => KeyCode.Alpha7,
                7 => KeyCode.Alpha8,
                8 => KeyCode.Alpha9,
                9 => KeyCode.Alpha0,
                10 => KeyCode.Minus,
                11 => KeyCode.Equals,
                _ => KeyCode.None,
            };
        }
    }
}
