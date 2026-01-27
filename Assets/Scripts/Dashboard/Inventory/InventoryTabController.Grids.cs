using MyGame.Common;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class InventoryTabController
{
    private void BuildGridsIfNeeded()
    {
        if (_gridsBuilt)
            return;

        if (_equipmentContent != null)
            BuildEmptyGrid(
                _equipmentContent,
                InventoryCapacity,
                _equipmentSlotIcons,
                GridKind.Equipment,
                registerRightClickEquip: true,
                showStackCount: false
            );
        if (_itemsContent != null)
            BuildEmptyGrid(
                _itemsContent,
                InventoryCapacity,
                _itemsSlotIcons,
                GridKind.Items,
                registerRightClickEquip: false,
                showStackCount: true
            );

        _gridsBuilt = true;
    }

    private void BuildEmptyGrid(
        VisualElement host,
        int capacity,
        System.Collections.Generic.List<VisualElement> outIconTargets,
        GridKind gridKind,
        bool registerRightClickEquip,
        bool showStackCount
    )
    {
        if (host == null)
            return;

        outIconTargets?.Clear();

        if (showStackCount)
            _itemsSlotCountLabels.Clear();

        if (gridKind == GridKind.Items)
        {
            _itemsSlotItemIds.Clear();
            for (int i = 0; i < capacity; i++)
                _itemsSlotItemIds.Add(null);
        }

        host.Clear();

        // Force a single child that can stretch: ScrollView.
        host.style.flexDirection = FlexDirection.Column;
        host.style.alignItems = Align.Stretch;

        var scroll = new ScrollView(ScrollViewMode.Vertical)
        {
            horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            verticalScrollerVisibility = ScrollerVisibility.Auto,
        };
        scroll.style.flexGrow = 1;
        scroll.style.flexShrink = 1;
        scroll.style.width = Length.Percent(100);
        scroll.style.height = Length.Percent(100);

        // Grid container inside scroll.
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.alignContent = Align.FlexStart;
        grid.style.justifyContent = Justify.FlexStart;

        // Slightly reduce right padding to make room for scrollbar.
        grid.style.paddingRight = 2;

        for (int i = 0; i < capacity; i++)
        {
            var slot = new VisualElement();
            slot.name = $"Slot_{i}";
            slot.userData = new InventorySlotUserData { gridKind = gridKind, slotIndex = i };
            slot.style.position = Position.Relative;
            slot.style.width = SlotSizePx;
            slot.style.height = SlotSizePx;
            slot.style.marginRight = SlotGapPx;
            slot.style.marginBottom = SlotGapPx;
            slot.style.paddingLeft = SlotPaddingPx;
            slot.style.paddingRight = SlotPaddingPx;
            slot.style.paddingTop = SlotPaddingPx;
            slot.style.paddingBottom = SlotPaddingPx;

            if (_gridSlotBackground != null)
                slot.style.backgroundImage = new StyleBackground(_gridSlotBackground);

            var icon = new VisualElement();
            icon.name = "Icon";
            icon.style.flexGrow = 1;
            // Reuse the global class (added earlier) so icons scale nicely once we start binding real items.
            icon.AddToClassList("equip-slot-icon");
            slot.Add(icon);

            outIconTargets?.Add(icon);

            if (showStackCount)
            {
                var count = new Label();
                count.name = "Count";
                count.pickingMode = PickingMode.Ignore;
                count.AddToClassList("header-xs");
                count.AddToClassList("text-white");
                count.style.position = Position.Absolute;
                count.style.right = 4;
                count.style.bottom = 4;
                count.style.unityTextAlign = TextAnchor.LowerRight;
                count.style.marginLeft = 0;
                count.style.marginRight = 0;
                count.style.marginTop = 0;
                count.style.marginBottom = 0;
                count.style.paddingLeft = 0;
                count.style.paddingRight = 0;
                count.style.paddingTop = 0;
                count.style.paddingBottom = 0;
                count.text = string.Empty;

                slot.Add(count);
                _itemsSlotCountLabels.Add(count);
            }

            int slotIndex = i;
            slot.RegisterCallback<PointerEnterEvent>(evt =>
            {
                OnHoverEnter(gridKind, slotIndex, slot, evt.position);
            });
            slot.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                OnHoverLeave(gridKind, slotIndex);
            });
            slot.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_hoveredGrid != gridKind || _hoveredSlotIndex != slotIndex)
                    return;
                if (_detailTooltip == null || _detailTooltip.style.display == DisplayStyle.None)
                    return;

                OnHoverMove(gridKind, slotIndex, slot, evt.position);
            });
            slot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (gridKind == GridKind.Equipment && evt.button == 0)
                {
                    TryBeginEquipmentDragCandidateFromInventorySlot(
                        slotIndex,
                        evt.pointerId,
                        evt.position
                    );
                    evt.StopPropagation();
                    return;
                }

                if (gridKind == GridKind.Items && evt.button == 0)
                {
                    TryBeginItemDragCandidateFromItemsSlot(slotIndex, evt.pointerId, evt.position);
                    evt.StopPropagation();
                    return;
                }

                if (gridKind == GridKind.Items && evt.button == 1)
                {
                    TryLearnSpellFromItemScrollSlot(slotIndex);
                    evt.StopPropagation();
                    return;
                }

                if (registerRightClickEquip && evt.button == 1)
                {
                    TryEquipFromEquipmentInventorySlot(slotIndex);
                    evt.StopPropagation();
                }
            });

            grid.Add(slot);
        }

        scroll.Add(grid);
        host.Add(scroll);

        if (registerRightClickEquip)
        {
            _equipmentSlotInstanceIds.Clear();
            for (int i = 0; i < capacity; i++)
                _equipmentSlotInstanceIds.Add(null);
        }

        if (showStackCount)
        {
            // Ensure label list matches capacity even if something went wrong.
            while (_itemsSlotCountLabels.Count < capacity)
                _itemsSlotCountLabels.Add(null);
        }
    }

    private void TryLearnSpellFromItemScrollSlot(int itemsSlotIndex)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        if (!RunSession.IsInitialized || RunSession.Items == null || RunSession.Spellbook == null)
            return;

        if (itemsSlotIndex < 0 || itemsSlotIndex >= _itemsSlotItemIds.Count)
            return;

        var itemId = _itemsSlotItemIds[itemsSlotIndex];
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        var itemDb = GameConfigProvider.Instance?.ItemDatabase;
        var itemDef = itemDb != null ? itemDb.GetById(itemId) : null;
        if (itemDef == null)
            return;

        // Must meet item level requirement.
        var save = SaveSession.Current;
        int requiredLevel = Mathf.Max(1, itemDef.requiredLevel);
        if (save.level < requiredLevel)
        {
            _swapper?.ShowTooltipAtElement(
                _itemsSlotIcons != null
                && itemsSlotIndex >= 0
                && itemsSlotIndex < _itemsSlotIcons.Count
                    ? _itemsSlotIcons[itemsSlotIndex]
                    : null,
                $"Requires Level {requiredLevel}"
            );
            return;
        }

        // "Learning Scroll": reuses scrollData, but only when NOT usable in combat.
        if (itemDef.itemType != ItemDefinitionType.SpellScroll)
            return;

        if (itemDef.usableInCombat)
            return;

        var scroll = itemDef.scrollData;
        if (scroll == null || string.IsNullOrWhiteSpace(scroll.spellId))
            return;

        var spellDb = GameConfigProvider.Instance?.SpellDatabase;
        var spellDef = spellDb != null ? spellDb.GetById(scroll.spellId) : null;
        if (spellDef == null)
            return;

        int desiredLevel = Mathf.Max(1, scroll.spellLevel);
        if (spellDef.maxLevel > 0)
            desiredLevel = Mathf.Clamp(desiredLevel, 1, spellDef.maxLevel);

        // Learn / upgrade mastery without wiping XP/cooldown/active slot.
        var existing = RunSession.Spellbook.Get(scroll.spellId);
        if (existing == null)
        {
            RunSession.Spellbook.UnlockIfMissing(scroll.spellId, desiredLevel);
        }
        else
        {
            existing.level = Mathf.Max(existing.level, desiredLevel);
        }

        // Consume one scroll.
        if (!RunSession.Items.Remove(itemId, 1))
            return;

        // If stack hit 0, clear from active combat slots.
        if (RunSession.Items.GetCount(itemId) <= 0)
            ActiveCombatSlotsCleanup.RemoveItemFromAllActiveCombatSlots(
                SaveSession.Current,
                itemId
            );

        SaveSessionRuntimeSave.SaveNowWithRuntime();

        RefreshItemsGridIcons();
        RefreshActiveCombatSlotsIcons();
    }

    private void RefreshEquipmentGridIcons()
    {
        if (_equipmentSlotIcons.Count == 0)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        // Clear all icons + mapping first.
        for (int i = 0; i < _equipmentSlotIcons.Count; i++)
            _equipmentSlotIcons[i].style.backgroundImage = StyleKeyword.None;
        for (int i = 0; i < _equipmentSlotInstanceIds.Count; i++)
            _equipmentSlotInstanceIds[i] = null;

        var equippedIds = new System.Collections.Generic.HashSet<string>(
            equipment.Equipped.Values,
            System.StringComparer.OrdinalIgnoreCase
        );

        int cap = Mathf.Min(_equipmentSlotIcons.Count, _equipmentSlotInstanceIds.Count);
        EnsureEquipmentInventoryLayoutLoadedFromSave(cap);
        bool layoutDirty = _equipmentInventoryLayoutDirty;

        // Step 1: build the full available list (non-equipped), independent of search.
        var availableAll = new System.Collections.Generic.List<string>(equipment.Instances.Count);
        foreach (var kvp in equipment.Instances)
        {
            var inst = kvp.Value;
            if (inst == null || string.IsNullOrWhiteSpace(inst.instanceId))
                continue;
            if (equippedIds.Contains(inst.instanceId))
                continue;
            availableAll.Add(inst.instanceId);
        }

        // Step 2: reconcile master ordering so fixed-slot placement persists across refreshes.
        var availableSet = new System.Collections.Generic.HashSet<string>(
            availableAll,
            System.StringComparer.OrdinalIgnoreCase
        );

        EnsureInventoryOrderSize(cap);

        // Remove invalid IDs and duplicates (leave holes instead of shifting).
        var present = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase
        );
        for (int i = 0; i < _equipmentInventoryOrderAll.Count; i++)
        {
            var id = _equipmentInventoryOrderAll[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!availableSet.Contains(id) || present.Contains(id))
            {
                layoutDirty = true;
                _equipmentInventoryOrderAll[i] = null;
                continue;
            }

            present.Add(id);
        }

        // Add any newly-available IDs into the first empty slots; if none, append.
        for (int i = 0; i < availableAll.Count; i++)
        {
            var id = availableAll[i];
            if (present.Contains(id))
                continue;

            int emptyIndex = -1;
            for (int j = 0; j < _equipmentInventoryOrderAll.Count; j++)
            {
                if (string.IsNullOrWhiteSpace(_equipmentInventoryOrderAll[j]))
                {
                    emptyIndex = j;
                    break;
                }
            }

            if (emptyIndex >= 0)
            {
                layoutDirty = true;
                _equipmentInventoryOrderAll[emptyIndex] = id;
            }
            else
            {
                layoutDirty = true;
                _equipmentInventoryOrderAll.Add(id);
            }

            present.Add(id);
        }

        // Persist slot assignments once we're on the Inventory tab.
        // This covers "new item has no slot yet" as well as user drag changes.
        if (layoutDirty)
        {
            PersistEquipmentInventoryLayoutToSave(saveNow: true);
            _equipmentInventoryLayoutDirty = false;
        }

        // Step 3: apply search as a visibility mask (does NOT compact or reorder).
        var equipmentDb = GameConfigProvider.Instance?.EquipmentDatabase;
        var query = _equipmentSearchQuery;

        // Step 4: write mapping + icons from fixed slots.
        for (int i = 0; i < cap; i++)
        {
            var id = i < _equipmentInventoryOrderAll.Count ? _equipmentInventoryOrderAll[i] : null;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var inst = equipment.GetInstance(id);
            if (inst == null)
                continue;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var name =
                    equipmentDb != null
                        ? equipmentDb.GetDisplayName(inst.equipmentId)
                        : inst.equipmentId;
                if (!MatchesSearch(name, query) && !MatchesSearch(inst.equipmentId, query))
                    continue;
            }

            _equipmentSlotInstanceIds[i] = id;

            var icon = GameConfigProvider.Instance?.EquipmentDatabase?.GetIcon(inst.equipmentId);
            if (icon == null)
                continue;

            _equipmentSlotIcons[i].style.backgroundImage = new StyleBackground(icon);
        }
    }

    private void TryEquipFromEquipmentInventorySlot(int inventorySlotIndex)
    {
        if (inventorySlotIndex < 0 || inventorySlotIndex >= _equipmentSlotInstanceIds.Count)
            return;

        var instanceId = _equipmentSlotInstanceIds[inventorySlotIndex];
        if (string.IsNullOrWhiteSpace(instanceId))
            return; // empty grid cell

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        var inst = equipment.GetInstance(instanceId);
        if (inst == null)
            return;

        var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(inst.equipmentId);
        var targetSlot = def != null ? def.slot : MyName.Equipment.EquipmentSlot.None;
        if (targetSlot == MyName.Equipment.EquipmentSlot.None)
            return;

        if (!CanEquip(def))
            return;

        // If the slot is occupied, Equip will replace it; the previous item becomes non-equipped.
        equipment.Equip(targetSlot, instanceId);
    }

    private static bool CanEquip(EquipmentDefinitionSO def)
    {
        if (def == null)
            return true;

        var save = SaveSession.Current;
        if (save == null)
            return true;

        if (save.level < Mathf.Max(1, def.requiredLevel))
            return false;

        if (def.flatStatRequirements != null)
        {
            for (int i = 0; i < def.flatStatRequirements.Count; i++)
            {
                var r = def.flatStatRequirements[i];
                if (r.minValue <= 0)
                    continue;

                int current = GetBaseStatValue(save.finalStats, r.stat);
                if (current < r.minValue)
                    return false;
            }
        }

        return true;
    }

    private static int GetBaseStatValue(Stats stats, BaseStatType stat)
    {
        return stat switch
        {
            BaseStatType.Strength => stats.strength,
            BaseStatType.Agility => stats.agility,
            BaseStatType.Intelligence => stats.intelligence,
            BaseStatType.Spirit => stats.spirit,
            BaseStatType.Endurance => stats.endurance,
            _ => 0,
        };
    }

    private void RefreshItemsGridIcons()
    {
        if (_itemsSlotIcons.Count == 0)
            return;

        // Clear all icons first.
        for (int i = 0; i < _itemsSlotIcons.Count; i++)
            _itemsSlotIcons[i].style.backgroundImage = StyleKeyword.None;

        for (int i = 0; i < _itemsSlotCountLabels.Count; i++)
        {
            if (_itemsSlotCountLabels[i] != null)
                _itemsSlotCountLabels[i].text = string.Empty;
        }

        if (_itemsSlotItemIds.Count != _itemsSlotIcons.Count)
        {
            _itemsSlotItemIds.Clear();
            for (int i = 0; i < _itemsSlotIcons.Count; i++)
                _itemsSlotItemIds.Add(null);
        }
        else
        {
            for (int i = 0; i < _itemsSlotItemIds.Count; i++)
                _itemsSlotItemIds[i] = null;
        }

        var items = RunSession.Items;
        if (items == null)
            return;

        var itemDb = GameConfigProvider.Instance?.ItemDatabase;
        var query = _itemsSearchQuery;

        int slotIndex = 0;
        foreach (var kvp in items.Counts)
        {
            if (slotIndex >= _itemsSlotIcons.Count)
                break;

            var itemId = kvp.Key;
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var name = itemDb != null ? itemDb.GetDisplayName(itemId) : itemId;
                if (!MatchesSearch(name, query) && !MatchesSearch(itemId, query))
                    continue;
            }

            var stackCount = kvp.Value;
            var icon = GameConfigProvider.Instance?.ItemDatabase?.GetIcon(itemId);
            if (icon == null)
                continue;

            _itemsSlotIcons[slotIndex].style.backgroundImage = new StyleBackground(icon);

            if (slotIndex < _itemsSlotItemIds.Count)
                _itemsSlotItemIds[slotIndex] = itemId;

            // Show stack count in bottom-right.
            if (slotIndex < _itemsSlotCountLabels.Count && _itemsSlotCountLabels[slotIndex] != null)
            {
                _itemsSlotCountLabels[slotIndex].text =
                    stackCount > 1 ? stackCount.ToString() : string.Empty;
            }

            slotIndex++;
        }
    }

    private static string NormalizeSearch(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        return t.Length == 0 ? null : t.ToLowerInvariant();
    }

    private static bool MatchesSearch(string haystack, string normalizedNeedle)
    {
        if (string.IsNullOrWhiteSpace(normalizedNeedle))
            return true;
        if (string.IsNullOrWhiteSpace(haystack))
            return false;

        return haystack.ToLowerInvariant().Contains(normalizedNeedle);
    }

    private void RefreshGoldValue()
    {
        if (_goldValueLabel == null)
            return;

        if (!SaveSession.HasSave || SaveSession.Current == null)
        {
            _goldValueLabel.text = "0";
            return;
        }

        _goldValueLabel.text =
            SaveSession.Current.gold != null ? SaveSession.Current.gold.Amount.ToString() : "0";
    }

    private static Sprite LoadFirstSprite(string[] resourcePaths)
    {
        if (resourcePaths == null || resourcePaths.Length == 0)
            return null;

        for (int i = 0; i < resourcePaths.Length; i++)
        {
            var p = resourcePaths[i];
            if (string.IsNullOrWhiteSpace(p))
                continue;

            var sprite = Resources.Load<Sprite>(p);
            if (sprite != null)
                return sprite;
        }

        return null;
    }
}
