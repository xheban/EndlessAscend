using MyGame.Save;
using MyName.Equipment;

namespace MyGame.Inventory
{
    public static class InventorySaveMapper
    {
        public static PlayerItems LoadItemsFromSave(SaveData save)
        {
            var items = new PlayerItems();

            if (save?.items == null)
                return items;

            foreach (var s in save.items)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.itemId) || s.quantity <= 0)
                    continue;

                items.Add(s.itemId, s.quantity);
            }

            return items;
        }

        public static PlayerEquipment LoadEquipmentFromSave(SaveData save)
        {
            var eq = new PlayerEquipment();

            if (save?.equipmentInstances != null)
            {
                foreach (var s in save.equipmentInstances)
                {
                    if (
                        s == null
                        || string.IsNullOrWhiteSpace(s.instanceId)
                        || string.IsNullOrWhiteSpace(s.equipmentId)
                    )
                        continue;

                    var inst = new PlayerEquipment.EquipmentInstance
                    {
                        instanceId = s.instanceId,
                        equipmentId = s.equipmentId,
                        rolledBaseStatMods =
                            s.rolledBaseStatMods != null
                                ? new System.Collections.Generic.List<MyGame.Common.BaseStatModifier>(
                                    s.rolledBaseStatMods
                                )
                                : new System.Collections.Generic.List<MyGame.Common.BaseStatModifier>(),
                        rolledDerivedStatMods =
                            s.rolledDerivedStatMods != null
                                ? new System.Collections.Generic.List<MyGame.Common.DerivedStatModifier>(
                                    s.rolledDerivedStatMods
                                )
                                : new System.Collections.Generic.List<MyGame.Common.DerivedStatModifier>(),
                        rolledSpellMods =
                            s.rolledSpellMods != null
                                ? new System.Collections.Generic.List<MyGame.Combat.SpellCombatModifier>(
                                    s.rolledSpellMods
                                )
                                : new System.Collections.Generic.List<MyGame.Combat.SpellCombatModifier>(),
                        rolledSpellOverrides =
                            s.rolledSpellOverrides != null
                                ? new System.Collections.Generic.List<MyGame.Combat.SpellVariableOverride>(
                                    s.rolledSpellOverrides
                                )
                                : new System.Collections.Generic.List<MyGame.Combat.SpellVariableOverride>(),
                    };

                    eq.AddOrReplace(inst);
                }
            }

            if (save?.equippedSlots != null)
            {
                foreach (var slot in save.equippedSlots)
                {
                    if (slot == null)
                        continue;

                    if (slot.slot == EquipmentSlot.None)
                        continue;

                    if (string.IsNullOrWhiteSpace(slot.equipmentInstanceId))
                        continue;

                    eq.Equip(slot.slot, slot.equipmentInstanceId);
                }
            }

            eq.EnforceEquippedValidity();
            return eq;
        }

        public static void WriteToSave(PlayerItems items, PlayerEquipment equipment, SaveData save)
        {
            if (save == null)
                return;

            // Items
            save.items ??= new System.Collections.Generic.List<SavedItemStackEntry>();
            save.items.Clear();

            if (items != null)
            {
                foreach (var kvp in items.Counts)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value <= 0)
                        continue;

                    save.items.Add(
                        new SavedItemStackEntry { itemId = kvp.Key, quantity = kvp.Value }
                    );
                }
            }

            // Equipment instances
            save.equipmentInstances ??=
                new System.Collections.Generic.List<SavedEquipmentInstance>();
            save.equipmentInstances.Clear();

            if (equipment != null)
            {
                foreach (var kvp in equipment.Instances)
                {
                    var inst = kvp.Value;
                    if (
                        inst == null
                        || string.IsNullOrWhiteSpace(inst.instanceId)
                        || string.IsNullOrWhiteSpace(inst.equipmentId)
                    )
                        continue;

                    save.equipmentInstances.Add(
                        new SavedEquipmentInstance
                        {
                            instanceId = inst.instanceId,
                            equipmentId = inst.equipmentId,
                            rolledBaseStatMods =
                                inst.rolledBaseStatMods != null
                                    ? new System.Collections.Generic.List<MyGame.Common.BaseStatModifier>(
                                        inst.rolledBaseStatMods
                                    )
                                    : new System.Collections.Generic.List<MyGame.Common.BaseStatModifier>(),
                            rolledDerivedStatMods =
                                inst.rolledDerivedStatMods != null
                                    ? new System.Collections.Generic.List<MyGame.Common.DerivedStatModifier>(
                                        inst.rolledDerivedStatMods
                                    )
                                    : new System.Collections.Generic.List<MyGame.Common.DerivedStatModifier>(),
                            rolledSpellMods =
                                inst.rolledSpellMods != null
                                    ? new System.Collections.Generic.List<MyGame.Combat.SpellCombatModifier>(
                                        inst.rolledSpellMods
                                    )
                                    : new System.Collections.Generic.List<MyGame.Combat.SpellCombatModifier>(),
                            rolledSpellOverrides =
                                inst.rolledSpellOverrides != null
                                    ? new System.Collections.Generic.List<MyGame.Combat.SpellVariableOverride>(
                                        inst.rolledSpellOverrides
                                    )
                                    : new System.Collections.Generic.List<MyGame.Combat.SpellVariableOverride>(),
                        }
                    );
                }

                // Equipped slots
                save.equippedSlots ??= new System.Collections.Generic.List<SavedEquippedSlot>();
                save.equippedSlots.Clear();

                foreach (var kvp in equipment.Equipped)
                {
                    if (kvp.Key == EquipmentSlot.None || string.IsNullOrWhiteSpace(kvp.Value))
                        continue;

                    save.equippedSlots.Add(
                        new SavedEquippedSlot { slot = kvp.Key, equipmentInstanceId = kvp.Value }
                    );
                }
            }
            else
            {
                save.equippedSlots ??= new System.Collections.Generic.List<SavedEquippedSlot>();
                save.equippedSlots.Clear();
            }
        }
    }
}
