using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Run;
using MyGame.Save;
using MyName.Equipment;

namespace MyGame.Inventory
{
    public static class InventoryGrantService
    {
        public static void GrantItem(string itemId, int amount = 1, bool saveNow = true)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return;

            if (!RunSession.IsInitialized)
                return;

            RunSession.Items.Add(itemId, amount);

            if (saveNow)
                SaveSessionRuntimeSave.SaveNowWithRuntime();
        }

        /// <summary>
        /// Creates a new equipment instance (with rolled affixes) and adds it to the player's
        /// equipment inventory (not auto-equipped). Returns the new instanceId.
        /// </summary>
        public static string GrantEquipmentToInventory(string equipmentId, bool saveNow = true)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
                return null;

            if (!RunSession.IsInitialized)
                return null;

            var equipment = RunSession.Equipment;
            if (equipment == null)
                return null;

            string instanceId = Guid.NewGuid().ToString("N");

            var inst = new PlayerEquipment.EquipmentInstance
            {
                instanceId = instanceId,
                equipmentId = equipmentId,
                rolledBaseStatMods = new List<BaseStatModifier>(),
                rolledDerivedStatMods = new List<DerivedStatModifier>(),
                rolledCombatStatMods = new List<CombatStatModifier>(),
                rolledSpellOverrides = new List<SpellVariableOverride>(),
            };

            // Roll affixes if we have a definition.
            var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(equipmentId);
            if (def != null)
            {
                var rng = new Random(instanceId.GetHashCode());
                EquipmentRoller.RollAllByRarity(
                    def,
                    rng,
                    inst.rolledBaseStatMods,
                    inst.rolledDerivedStatMods,
                    inst.rolledCombatStatMods,
                    inst.rolledSpellOverrides
                );
            }

            equipment.AddOrReplace(inst);

            if (saveNow)
                SaveSessionRuntimeSave.SaveNowWithRuntime();

            return instanceId;
        }

        /// <summary>
        /// Creates a new equipment instance (with rolled affixes) and adds it to the given save.
        /// Does not auto-equip. Returns the new instanceId.
        /// </summary>
        public static string GrantEquipmentToSave(SaveData data, string equipmentId)
        {
            if (data == null || string.IsNullOrWhiteSpace(equipmentId))
                return null;

            data.equipmentInstances ??= new List<SavedEquipmentInstance>();

            string instanceId = Guid.NewGuid().ToString("N");

            var inst = new SavedEquipmentInstance
            {
                instanceId = instanceId,
                equipmentId = equipmentId,
                rolledBaseStatMods = new List<BaseStatModifier>(),
                rolledDerivedStatMods = new List<DerivedStatModifier>(),
                rolledCombatStatMods = new List<CombatStatModifier>(),
                rolledSpellOverrides = new List<SpellVariableOverride>(),
            };

            var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(equipmentId);
            if (def != null)
            {
                var rng = new Random(instanceId.GetHashCode());
                EquipmentRoller.RollAllByRarity(
                    def,
                    rng,
                    inst.rolledBaseStatMods,
                    inst.rolledDerivedStatMods,
                    inst.rolledCombatStatMods,
                    inst.rolledSpellOverrides
                );
            }

            data.equipmentInstances.Add(inst);
            return instanceId;
        }

        /// <summary>
        /// Character-creation helper: create an equipment instance, roll it, and equip it into its slot.
        /// Returns the created instanceId.
        /// </summary>
        public static string GrantAndEquipEquipment(SaveData data, string equipmentId)
        {
            if (data == null || string.IsNullOrWhiteSpace(equipmentId))
                return null;

            data.equippedSlots ??= new List<SavedEquippedSlot>();

            string instanceId = GrantEquipmentToSave(data, equipmentId);
            if (string.IsNullOrWhiteSpace(instanceId))
                return null;

            var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(equipmentId);
            EquipmentSlot slot = def != null ? def.slot : EquipmentSlot.None;

            // Fallback heuristics for early content.
            if (slot == EquipmentSlot.None)
            {
                var key = equipmentId.Trim().ToLowerInvariant();
                if (key.Contains("boot"))
                    slot = EquipmentSlot.Feet;
                else if (key.Contains("bow") || key.Contains("ranged"))
                    slot = EquipmentSlot.Ranged;
                else
                    slot = EquipmentSlot.MainHand;
            }

            UpsertEquippedSlot(data, slot, instanceId);
            return instanceId;
        }

        private static void UpsertEquippedSlot(SaveData data, EquipmentSlot slot, string instanceId)
        {
            if (data == null || slot == EquipmentSlot.None)
                return;

            data.equippedSlots ??= new List<SavedEquippedSlot>();

            for (int i = 0; i < data.equippedSlots.Count; i++)
            {
                var e = data.equippedSlots[i];
                if (e == null)
                    continue;

                if (e.slot == slot)
                {
                    e.equipmentInstanceId = instanceId;
                    return;
                }
            }

            data.equippedSlots.Add(
                new SavedEquippedSlot { slot = slot, equipmentInstanceId = instanceId }
            );
        }
    }
}
