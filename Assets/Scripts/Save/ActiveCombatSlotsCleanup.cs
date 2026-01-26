using System;
using System.Collections.Generic;

namespace MyGame.Save
{
    public static class ActiveCombatSlotsCleanup
    {
        public const int ActiveCombatSlotsCount = 4;

        public static bool RemoveItemFromAllActiveCombatSlots(SaveData save, string itemId)
        {
            if (save == null)
                return false;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            save.activeCombatSlots ??= new List<SavedCombatActiveSlotEntry>();

            bool changed = false;

            while (save.activeCombatSlots.Count < ActiveCombatSlotsCount)
            {
                save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());
                changed = true;
            }

            for (int i = 0; i < save.activeCombatSlots.Count; i++)
            {
                var slot = save.activeCombatSlots[i];
                if (slot == null)
                    continue;

                if (
                    !string.IsNullOrWhiteSpace(slot.itemId)
                    && string.Equals(slot.itemId, itemId, StringComparison.OrdinalIgnoreCase)
                )
                {
                    slot.itemId = null;
                    slot.equipmentInstanceId = null;
                    changed = true;
                }
            }

            return changed;
        }

        public static bool RemoveItemFromAllActiveCombatSlotsAndSave(string itemId, bool saveNowWithRuntime)
        {
            if (!SaveSession.HasSave || SaveSession.Current == null)
                return false;

            bool changed = RemoveItemFromAllActiveCombatSlots(SaveSession.Current, itemId);
            if (!changed)
                return false;

            if (saveNowWithRuntime)
                SaveSessionRuntimeSave.SaveNowWithRuntime();
            else
                SaveSession.SaveNow();

            return true;
        }
    }
}
