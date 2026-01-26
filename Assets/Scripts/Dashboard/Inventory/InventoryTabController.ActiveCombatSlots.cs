using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class InventoryTabController
{
    private const int ActiveCombatSlotsCount = 4;

    private VisualElement _activeCombatItemsRoot;
    private readonly System.Collections.Generic.List<VisualElement> _activeCombatSlotRoots = new();
    private readonly System.Collections.Generic.List<VisualElement> _activeCombatSlotIcons = new();

    private void BindActiveCombatSlotsIfPresent()
    {
        if (_root == null)
            return;

        _activeCombatItemsRoot = _root.Q<VisualElement>("ActiveCombatItems");
        if (_activeCombatItemsRoot == null)
            return;

        _activeCombatSlotRoots.Clear();
        _activeCombatSlotIcons.Clear();

        for (int i = 0; i < ActiveCombatSlotsCount; i++)
        {
            var slotRoot = _activeCombatItemsRoot.Q<VisualElement>($"Slot{i + 1}");
            if (slotRoot == null)
                continue;

            var icon = slotRoot.Q<VisualElement>("Icon");

            slotRoot.userData = new ActiveCombatSlotUserData { slotIndex = i };

            int slotIndex = i;

            slotRoot.RegisterCallback<PointerEnterEvent>(evt =>
            {
                OnHoverEnter(GridKind.ActiveCombatSlots, slotIndex, slotRoot, evt.position);
            });

            slotRoot.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                OnHoverLeave(GridKind.ActiveCombatSlots, slotIndex);
            });

            slotRoot.RegisterCallback<PointerMoveEvent>(evt =>
            {
                OnHoverMove(GridKind.ActiveCombatSlots, slotIndex, slotRoot, evt.position);
            });

            slotRoot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1)
                    return;

                ClearActiveCombatSlot(slotIndex);
                evt.StopPropagation();
            });

            _activeCombatSlotRoots.Add(slotRoot);
            _activeCombatSlotIcons.Add(icon);
        }

        RefreshActiveCombatSlotsIcons();
    }

    private void RefreshActiveCombatSlotsIcons()
    {
        if (_activeCombatSlotIcons.Count == 0)
            return;

        for (int i = 0; i < _activeCombatSlotIcons.Count; i++)
        {
            var icon = _activeCombatSlotIcons[i];
            if (icon != null)
                icon.style.backgroundImage = StyleKeyword.None;
        }

        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        EnsureActiveCombatSlotsListExists(SaveSession.Current, saveNowIfChanged: false);

        bool anyChanged = false;

        var itemDb = GameConfigProvider.Instance?.ItemDatabase;
        var equipDb = GameConfigProvider.Instance?.EquipmentDatabase;

        var items = RunSession.Items;
        var equipment = RunSession.Equipment;

        for (int slotIndex = 0; slotIndex < ActiveCombatSlotsCount; slotIndex++)
        {
            if (slotIndex >= _activeCombatSlotIcons.Count)
                break;

            var iconEl = _activeCombatSlotIcons[slotIndex];
            if (iconEl == null)
                continue;

            var entry = GetActiveCombatSlotEntryOrNull(SaveSession.Current, slotIndex);
            if (entry == null)
                continue;

            // Equipment takes precedence if both are set (shouldn't happen).
            if (!string.IsNullOrWhiteSpace(entry.equipmentInstanceId))
            {
                var inst =
                    equipment != null ? equipment.GetInstance(entry.equipmentInstanceId) : null;
                if (inst == null)
                {
                    entry.equipmentInstanceId = null;
                    anyChanged = true;
                    continue;
                }

                var def = equipDb != null ? equipDb.GetById(inst.equipmentId) : null;
                if (def != null && !def.usableInCombat)
                {
                    entry.equipmentInstanceId = null;
                    anyChanged = true;
                    continue;
                }

                var sprite = equipDb != null ? equipDb.GetIcon(inst.equipmentId) : null;
                iconEl.style.backgroundImage =
                    sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;

                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.itemId))
            {
                string currentItemId = entry.itemId;
                int qty = 0;
                if (items == null || !items.Counts.TryGetValue(currentItemId, out qty) || qty <= 0)
                {
                    if (
                        ActiveCombatSlotsCleanup.RemoveItemFromAllActiveCombatSlots(
                            SaveSession.Current,
                            currentItemId
                        )
                    )
                        anyChanged = true;
                    continue;
                }

                var def = itemDb != null ? itemDb.GetById(currentItemId) : null;
                if (def != null && !def.usableInCombat)
                {
                    if (
                        ActiveCombatSlotsCleanup.RemoveItemFromAllActiveCombatSlots(
                            SaveSession.Current,
                            currentItemId
                        )
                    )
                        anyChanged = true;
                    continue;
                }

                var sprite = itemDb != null ? itemDb.GetIcon(currentItemId) : null;
                iconEl.style.backgroundImage =
                    sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
            }
        }

        if (anyChanged)
            SaveSession.SaveNow();
    }

    private static void EnsureActiveCombatSlotsListExists(SaveData save, bool saveNowIfChanged)
    {
        if (save == null)
            return;

        save.activeCombatSlots ??=
            new System.Collections.Generic.List<SavedCombatActiveSlotEntry>();

        bool changed = false;
        while (save.activeCombatSlots.Count < ActiveCombatSlotsCount)
        {
            save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());
            changed = true;
        }

        if (changed && saveNowIfChanged)
            SaveSession.SaveNow();
    }

    private static SavedCombatActiveSlotEntry GetActiveCombatSlotEntryOrNull(
        SaveData save,
        int slotIndex
    )
    {
        if (save == null)
            return null;
        if (slotIndex < 0)
            return null;

        save.activeCombatSlots ??=
            new System.Collections.Generic.List<SavedCombatActiveSlotEntry>();

        while (save.activeCombatSlots.Count < ActiveCombatSlotsCount)
            save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

        if (slotIndex >= save.activeCombatSlots.Count)
            return null;

        save.activeCombatSlots[slotIndex] ??= new SavedCombatActiveSlotEntry();
        return save.activeCombatSlots[slotIndex];
    }

    private void ClearActiveCombatSlot(int slotIndex)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;
        if (slotIndex < 0 || slotIndex >= ActiveCombatSlotsCount)
            return;

        var entry = GetActiveCombatSlotEntryOrNull(SaveSession.Current, slotIndex);
        if (entry == null)
            return;

        entry.itemId = null;
        entry.equipmentInstanceId = null;

        SaveSession.SaveNow();
        RefreshActiveCombatSlotsIcons();
    }

    private bool TryAssignActiveCombatSlotItem(int slotIndex, string itemId)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return false;
        if (slotIndex < 0 || slotIndex >= ActiveCombatSlotsCount)
            return false;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        var items = RunSession.Items;
        if (items == null)
            return false;

        if (!items.Counts.TryGetValue(itemId, out var qty) || qty <= 0)
            return false;

        var def = GameConfigProvider.Instance?.ItemDatabase?.GetById(itemId);
        if (def != null && !def.usableInCombat)
            return false;

        EnsureActiveCombatSlotsListExists(SaveSession.Current, saveNowIfChanged: false);

        ClearDuplicateActiveCombatEntries(
            itemId: itemId,
            equipmentInstanceId: null,
            exceptSlotIndex: slotIndex
        );

        var entry = GetActiveCombatSlotEntryOrNull(SaveSession.Current, slotIndex);
        if (entry == null)
            return false;

        entry.itemId = itemId;
        entry.equipmentInstanceId = null;

        SaveSession.SaveNow();
        RefreshActiveCombatSlotsIcons();
        return true;
    }

    private bool TryAssignActiveCombatSlotEquipment(int slotIndex, string equipmentInstanceId)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return false;
        if (slotIndex < 0 || slotIndex >= ActiveCombatSlotsCount)
            return false;
        if (string.IsNullOrWhiteSpace(equipmentInstanceId))
            return false;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return false;

        var inst = equipment.GetInstance(equipmentInstanceId);
        if (inst == null)
            return false;

        var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(inst.equipmentId);
        if (def != null && !def.usableInCombat)
            return false;

        EnsureActiveCombatSlotsListExists(SaveSession.Current, saveNowIfChanged: false);

        ClearDuplicateActiveCombatEntries(
            itemId: null,
            equipmentInstanceId: equipmentInstanceId,
            exceptSlotIndex: slotIndex
        );

        var entry = GetActiveCombatSlotEntryOrNull(SaveSession.Current, slotIndex);
        if (entry == null)
            return false;

        entry.itemId = null;
        entry.equipmentInstanceId = equipmentInstanceId;

        SaveSession.SaveNow();
        RefreshActiveCombatSlotsIcons();
        return true;
    }

    private static void ClearDuplicateActiveCombatEntries(
        string itemId,
        string equipmentInstanceId,
        int exceptSlotIndex
    )
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        var save = SaveSession.Current;
        save.activeCombatSlots ??=
            new System.Collections.Generic.List<SavedCombatActiveSlotEntry>();

        for (int i = 0; i < save.activeCombatSlots.Count; i++)
        {
            if (i == exceptSlotIndex)
                continue;

            var e = save.activeCombatSlots[i];
            if (e == null)
                continue;

            if (
                !string.IsNullOrWhiteSpace(itemId)
                && !string.IsNullOrWhiteSpace(e.itemId)
                && string.Equals(e.itemId, itemId, System.StringComparison.OrdinalIgnoreCase)
            )
            {
                e.itemId = null;
            }

            if (
                !string.IsNullOrWhiteSpace(equipmentInstanceId)
                && !string.IsNullOrWhiteSpace(e.equipmentInstanceId)
                && string.Equals(
                    e.equipmentInstanceId,
                    equipmentInstanceId,
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                e.equipmentInstanceId = null;
            }
        }
    }
}
