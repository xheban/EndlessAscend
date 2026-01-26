using System;
using System.Text;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class InventoryTabController
{
    private sealed class EquippedTooltipHandlers
    {
        public EventCallback<PointerEnterEvent> Enter;
        public EventCallback<PointerLeaveEvent> Leave;
        public EventCallback<PointerMoveEvent> Move;
        public EventCallback<PointerDownEvent> Down;
    }

    private readonly System.Collections.Generic.Dictionary<
        VisualElement,
        EquippedTooltipHandlers
    > _equippedTooltipHandlers = new();

    private void RegisterEquippedTooltipHandlersIfNeeded()
    {
        if (_equippedTooltipHandlersRegistered)
            return;

        var equippedContent = _root?.Q<VisualElement>("EquippedItemsContent");
        if (equippedContent == null)
            return;

        var bindings = new (string elementName, MyName.Equipment.EquipmentSlot slot)[]
        {
            ("Head", MyName.Equipment.EquipmentSlot.Head),
            ("Chest", MyName.Equipment.EquipmentSlot.Chest),
            ("Legs", MyName.Equipment.EquipmentSlot.Legs),
            ("Bracer", MyName.Equipment.EquipmentSlot.Hands),
            ("Foot", MyName.Equipment.EquipmentSlot.Feet),
            ("MainHand", MyName.Equipment.EquipmentSlot.MainHand),
            ("OffHand", MyName.Equipment.EquipmentSlot.Offhand),
            ("Ring1", MyName.Equipment.EquipmentSlot.Ring),
            ("Amulet", MyName.Equipment.EquipmentSlot.Amulet),
            ("Jewelery", MyName.Equipment.EquipmentSlot.Jewelry),
            ("Belt", MyName.Equipment.EquipmentSlot.Belt),
            ("Trinket", MyName.Equipment.EquipmentSlot.Trinket),
            ("Gloves", MyName.Equipment.EquipmentSlot.Gloves),
            ("Shoulder", MyName.Equipment.EquipmentSlot.Shoulders),
            ("Ranged", MyName.Equipment.EquipmentSlot.Ranged),
            ("Cape", MyName.Equipment.EquipmentSlot.Cape),
        };

        UnregisterEquippedTooltipHandlers();

        foreach (var b in bindings)
        {
            var slotEl = equippedContent.Q<VisualElement>(b.elementName);
            if (slotEl == null)
                continue;

            var slotValue = b.slot;
            int slotIndex = (int)slotValue;

            _equippedSlotElementsBySlot[slotValue] = slotEl;
            slotEl.userData = new EquippedSlotUserData { slot = slotValue };

            var handlers = new EquippedTooltipHandlers();

            handlers.Enter = evt =>
            {
                OnHoverEnter(GridKind.Equipped, slotIndex, slotEl, evt.position);
            };

            handlers.Leave = evt =>
            {
                OnHoverLeave(GridKind.Equipped, slotIndex);
            };

            handlers.Move = evt =>
            {
                OnHoverMove(GridKind.Equipped, slotIndex, slotEl, evt.position);
            };

            handlers.Down = evt =>
            {
                if (evt.button == 0)
                {
                    TryBeginEquipmentDragCandidateFromEquippedSlot(
                        slotValue,
                        evt.pointerId,
                        evt.position
                    );
                    evt.StopPropagation();
                }
            };

            slotEl.RegisterCallback(handlers.Enter);
            slotEl.RegisterCallback(handlers.Leave);
            slotEl.RegisterCallback(handlers.Move);
            slotEl.RegisterCallback(handlers.Down);
            _equippedTooltipHandlers[slotEl] = handlers;
        }

        _equippedTooltipHandlersRegistered = true;
    }

    private void UnregisterEquippedTooltipHandlers()
    {
        if (_equippedTooltipHandlers.Count == 0)
        {
            _equippedTooltipHandlersRegistered = false;
            return;
        }

        foreach (var kvp in _equippedTooltipHandlers)
        {
            var slotEl = kvp.Key;
            var handlers = kvp.Value;
            if (slotEl == null || handlers == null)
                continue;

            if (handlers.Enter != null)
                slotEl.UnregisterCallback(handlers.Enter);
            if (handlers.Leave != null)
                slotEl.UnregisterCallback(handlers.Leave);
            if (handlers.Move != null)
                slotEl.UnregisterCallback(handlers.Move);
            if (handlers.Down != null)
                slotEl.UnregisterCallback(handlers.Down);
        }

        _equippedTooltipHandlers.Clear();
        _equippedTooltipHandlersRegistered = false;
    }

    private void OnHoverEnter(
        GridKind gridKind,
        int slotIndex,
        VisualElement slotElement,
        Vector2 pointerWorld
    )
    {
        if (_isDraggingEquipment)
            return;

        _hoveredGrid = gridKind;
        _hoveredSlotIndex = slotIndex;
        _hoveredSlotElement = slotElement;
        _hoveredPointerWorld = pointerWorld;

        // ALT-hold suppresses tooltip updates entirely until ALT is released.
        // (This keeps the current tooltip frozen, and prevents new ones from showing.)
        if (_altHeld)
            return;

        // SHIFT-hold: if we don't have a locked compare anchor yet, capture the current hover.
        if (_shiftHeld && !_lockedHoverValid)
            LockHoverFromCurrent();

        RefreshTooltipsForCurrentState();
    }

    private void OnHoverMove(
        GridKind gridKind,
        int slotIndex,
        VisualElement slotElement,
        Vector2 pointerWorld
    )
    {
        if (_isDraggingEquipment)
            return;

        if (_hoveredGrid != gridKind || _hoveredSlotIndex != slotIndex)
            return;

        _hoveredSlotElement = slotElement;
        _hoveredPointerWorld = pointerWorld;

        // ALT-hold suppresses tooltip updates entirely until ALT is released.
        if (_altHeld)
            return;

        // SHIFT compare should keep anchoring to the most recent hovered slot.
        // ALT should freeze the tooltip position where it was when ALT was pressed.
        if (_shiftHeld && !_lockedHoverValid)
            LockHoverFromCurrent();

        // If not holding a modifier, keep the tooltip following the pointer.
        if (!_altHeld && !_shiftHeld)
        {
            if (_detailTooltip != null && _detailTooltip.style.display != DisplayStyle.None)
                PositionDetailTooltip(pointerWorld);
        }
        else if (_shiftHeld)
        {
            // While holding ALT/SHIFT, tooltip(s) may be anchored; refresh positioning.
            RefreshTooltipsForCurrentState();
        }
        // ALT-held: do not reposition while the pointer moves.
    }

    private void OnHoverLeave(GridKind gridKind, int slotIndex)
    {
        if (_isDraggingEquipment)
            return;

        if (_hoveredGrid == gridKind && _hoveredSlotIndex == slotIndex)
        {
            _hoveredSlotIndex = -1;
            _hoveredSlotElement = null;
        }

        if (_altHeld || _shiftHeld)
        {
            // Keep showing the last locked tooltip(s) while a modifier is held.
            return;
        }

        _lockedHoverValid = false;
        _lockedSlotIndex = -1;
        HideAllTooltips();
    }

    private void LockHoverFromCurrent()
    {
        if (_hoveredSlotIndex < 0)
            return;

        _lockedHoverValid = true;
        _lockedGrid = _hoveredGrid;
        _lockedSlotIndex = _hoveredSlotIndex;
        _lockedSlotElement = _hoveredSlotElement;
        _lockedPointerWorld = _hoveredPointerWorld;
    }

    private void StartModifierPolling()
    {
        if (_modifierPoller != null)
            return;
        if (_root == null)
            return;

        _modifierPoller = _root.schedule.Execute(PollModifiersFromUnityInput).Every(33);
    }

    private void StopModifierPolling()
    {
        if (_modifierPoller == null)
            return;

        _modifierPoller.Pause();
        _modifierPoller = null;
    }

    private void PollModifiersFromUnityInput()
    {
        if (!_isVisible)
            return;

        bool altNow = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool shiftNow = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool altPressed = altNow && !_altHeld;
        bool shiftPressed = shiftNow && !_shiftHeld;

        if (altNow == _altHeld && shiftNow == _shiftHeld)
            return;

        _altHeld = altNow;
        _shiftHeld = shiftNow;

        if (altPressed || shiftPressed)
            LockHoverFromCurrent();

        RefreshTooltipsForCurrentState();
    }

    private bool TryGetActiveHover(
        out GridKind gridKind,
        out int slotIndex,
        out VisualElement slotElement,
        out Vector2 pointerWorld
    )
    {
        // While holding ALT, always prefer the locked anchor (and show nothing if none).
        if (_altHeld)
        {
            if (_lockedHoverValid && _lockedSlotIndex >= 0)
            {
                gridKind = _lockedGrid;
                slotIndex = _lockedSlotIndex;
                slotElement = _lockedSlotElement;
                pointerWorld = _lockedPointerWorld;
                return true;
            }

            gridKind = default;
            slotIndex = -1;
            slotElement = null;
            pointerWorld = default;
            return false;
        }

        // While holding SHIFT, prefer a locked Equipment anchor so comparison remains stable
        // no matter where the pointer moves.
        if (
            _shiftHeld
            && _lockedHoverValid
            && _lockedGrid == GridKind.Equipment
            && _lockedSlotIndex >= 0
        )
        {
            gridKind = _lockedGrid;
            slotIndex = _lockedSlotIndex;
            slotElement = _lockedSlotElement;
            pointerWorld = _lockedPointerWorld;
            return true;
        }

        // Default: use current hover if available.
        if (_hoveredSlotIndex >= 0)
        {
            gridKind = _hoveredGrid;
            slotIndex = _hoveredSlotIndex;
            slotElement = _hoveredSlotElement;
            pointerWorld = _hoveredPointerWorld;
            return true;
        }

        // SHIFT held but no equipment lock: fall back to any locked hover.
        if (_shiftHeld && _lockedHoverValid && _lockedSlotIndex >= 0)
        {
            gridKind = _lockedGrid;
            slotIndex = _lockedSlotIndex;
            slotElement = _lockedSlotElement;
            pointerWorld = _lockedPointerWorld;
            return true;
        }

        gridKind = default;
        slotIndex = -1;
        slotElement = null;
        pointerWorld = default;
        return false;
    }

    private void RefreshTooltipsForCurrentState()
    {
        // If no modifier is held, only show tooltip while actively hovering.
        if (!_altHeld && !_shiftHeld)
        {
            _lockedHoverValid = false;
            _lockedSlotIndex = -1;

            HideCompareTooltip();

            if (_hoveredSlotIndex < 0)
            {
                HideDetailTooltip();
                return;
            }

            TryShowDetailTooltipForSlot(_hoveredGrid, _hoveredSlotIndex, _hoveredPointerWorld);
            return;
        }

        // ALT held: keep showing the tooltip for the locked anchor, and ignore any other hovers
        // until ALT is released.
        if (_altHeld)
        {
            HideCompareTooltip();

            if (_lockedHoverValid && _lockedSlotIndex >= 0)
                TryShowDetailTooltipForSlot(_lockedGrid, _lockedSlotIndex, _lockedPointerWorld);
            else
                HideDetailTooltip();

            return;
        }

        // Modifier held: show tooltip based on current hover, otherwise keep last locked.
        if (
            !TryGetActiveHover(
                out var gridKind,
                out var slotIndex,
                out var slotElement,
                out var pointerWorld
            )
        )
        {
            HideAllTooltips();
            return;
        }

        if (_shiftHeld && gridKind == GridKind.Equipment)
        {
            if (TryShowShiftCompareTooltips(slotIndex, slotElement))
                return;

            // If we can't compare (nothing equipped), fall back to a single tooltip.
        }

        HideCompareTooltip();
        TryShowDetailTooltipForSlot(gridKind, slotIndex, pointerWorld);
    }

    private bool TryShowDetailTooltipForSlot(GridKind gridKind, int slotIndex, Vector2 pointerWorld)
    {
        if (_detailTooltip == null)
            return false;

        if (!TryPopulateInventoryDetailTooltip(_detailTooltip, gridKind, slotIndex))
            return false;

        if (_swapper != null)
        {
            _swapper.ShowCustomTooltipAtWorldPosition(
                _detailTooltip,
                pointerWorld,
                TooltipOffsetPx,
                TooltipEdgePaddingPx,
                TooltipFallbackWidthPx,
                TooltipFallbackHeightPx
            );
        }
        else
        {
            _detailTooltip.style.display = DisplayStyle.Flex;
            _detailTooltip.style.position = Position.Absolute;
            _detailTooltip.BringToFront();
            PositionDetailTooltip(pointerWorld);
        }
        return true;
    }

    private bool TryPopulateInventoryDetailTooltip(
        VisualElement tooltip,
        GridKind gridKind,
        int slotIndex
    )
    {
        if (tooltip == null || slotIndex < 0)
            return false;

        switch (gridKind)
        {
            case GridKind.ActiveCombatSlots:
            {
                if (!SaveSession.HasSave || SaveSession.Current == null)
                    return false;

                var save = SaveSession.Current;
                save.activeCombatSlots ??=
                    new System.Collections.Generic.List<SavedCombatActiveSlotEntry>();
                while (save.activeCombatSlots.Count < 4)
                    save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

                if (slotIndex >= save.activeCombatSlots.Count)
                    return false;

                var entry = save.activeCombatSlots[slotIndex];
                if (entry == null)
                    return false;

                // Equipment precedence.
                if (!string.IsNullOrWhiteSpace(entry.equipmentInstanceId))
                {
                    var equipment = RunSession.Equipment;
                    if (equipment == null)
                        return false;

                    var inst = equipment.GetInstance(entry.equipmentInstanceId);
                    return MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                        tooltip,
                        inst
                    );
                }

                if (!string.IsNullOrWhiteSpace(entry.itemId))
                {
                    return MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForItemId(
                        tooltip,
                        entry.itemId
                    );
                }

                return false;
            }
            case GridKind.Items:
            {
                if (slotIndex >= _itemsSlotItemIds.Count)
                    return false;
                var itemId = _itemsSlotItemIds[slotIndex];
                return MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForItemId(
                    tooltip,
                    itemId
                );
            }
            case GridKind.Equipment:
            {
                if (slotIndex >= _equipmentSlotInstanceIds.Count)
                    return false;
                var instanceId = _equipmentSlotInstanceIds[slotIndex];
                if (string.IsNullOrWhiteSpace(instanceId))
                    return false;

                var equipment = RunSession.Equipment;
                if (equipment == null)
                    return false;

                var inst = equipment.GetInstance(instanceId);
                return MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                    tooltip,
                    inst
                );
            }
            case GridKind.Equipped:
            {
                var equipment = RunSession.Equipment;
                if (equipment == null)
                    return false;

                var slot = (MyName.Equipment.EquipmentSlot)slotIndex;
                if (slot == MyName.Equipment.EquipmentSlot.None)
                    return false;

                if (!equipment.TryGetEquippedInstance(slot, out var inst) || inst == null)
                    return false;

                return MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                    tooltip,
                    inst
                );
            }
            default:
                return false;
        }
    }

    private void PositionDetailTooltip(Vector2 pointerWorld)
    {
        if (_detailTooltip == null)
            return;

        if (_swapper != null)
        {
            _swapper.PositionCustomTooltipAtWorldPosition(
                _detailTooltip,
                pointerWorld,
                TooltipOffsetPx,
                TooltipEdgePaddingPx,
                TooltipFallbackWidthPx,
                TooltipFallbackHeightPx
            );
            return;
        }

        if (_inventoryPanel == null)
            return;

        var local = _inventoryPanel.WorldToLocal(pointerWorld);

        float w = _detailTooltip.resolvedStyle.width;
        float h = _detailTooltip.resolvedStyle.height;
        if (w <= 1f)
            w = TooltipFallbackWidthPx;
        if (h <= 1f)
            h = TooltipFallbackHeightPx;

        float panelW = _inventoryPanel.resolvedStyle.width;
        float panelH = _inventoryPanel.resolvedStyle.height;

        float x = local.x + TooltipOffsetPx;
        float y = local.y + TooltipOffsetPx;
        if (panelW > 0 && x + w + TooltipEdgePaddingPx > panelW)
            x = local.x - w - TooltipOffsetPx;
        if (panelH > 0 && y + h + TooltipEdgePaddingPx > panelH)
            y = local.y - h - TooltipOffsetPx;

        if (panelW > 0)
            x = Mathf.Clamp(
                x,
                TooltipEdgePaddingPx,
                Mathf.Max(TooltipEdgePaddingPx, panelW - w - TooltipEdgePaddingPx)
            );
        if (panelH > 0)
            y = Mathf.Clamp(
                y,
                TooltipEdgePaddingPx,
                Mathf.Max(TooltipEdgePaddingPx, panelH - h - TooltipEdgePaddingPx)
            );

        _detailTooltip.style.left = x;
        _detailTooltip.style.top = y;
    }

    private void HideDetailTooltip()
    {
        if (_detailTooltip == null)
            return;

        if (_swapper != null)
        {
            _swapper.HideCustomTooltip(_detailTooltip);
            return;
        }

        _detailTooltip.style.display = DisplayStyle.None;
    }

    private void HideCompareTooltip()
    {
        if (_detailTooltipCompare == null)
            return;

        if (_swapper != null)
        {
            _swapper.HideCustomTooltip(_detailTooltipCompare);
            return;
        }

        _detailTooltipCompare.style.display = DisplayStyle.None;
    }

    private void HideAllTooltips()
    {
        HideDetailTooltip();
        HideCompareTooltip();
    }

    private bool TryShowShiftCompareTooltips(
        int inventorySlotIndex,
        VisualElement inventorySlotElement
    )
    {
        if (_detailTooltip == null || _detailTooltipCompare == null)
            return false;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return false;

        if (inventorySlotIndex < 0 || inventorySlotIndex >= _equipmentSlotInstanceIds.Count)
            return false;

        var instanceId = _equipmentSlotInstanceIds[inventorySlotIndex];
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var invInst = equipment.GetInstance(instanceId);
        if (invInst == null)
            return false;

        var db = GameConfigProvider.Instance?.EquipmentDatabase;
        var def = db != null ? db.GetById(invInst.equipmentId) : null;
        if (def == null || def.slot == MyName.Equipment.EquipmentSlot.None)
            return false;

        if (
            !equipment.TryGetEquippedInstance(def.slot, out var equippedInst)
            || equippedInst == null
        )
            return false;

        // Use shared builder so compare tooltip matches everywhere.
        if (
            !MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                _detailTooltip,
                invInst
            )
        )
            return false;

        if (
            !MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                _detailTooltipCompare,
                equippedInst
            )
        )
            return false;

        var invRect = inventorySlotElement != null ? inventorySlotElement.worldBound : default;
        var invAnchor = new Vector2(invRect.center.x, invRect.yMin);

        Vector2 eqAnchor = invAnchor;
        if (_equippedSlotElementsBySlot.TryGetValue(def.slot, out var eqSlotEl) && eqSlotEl != null)
        {
            var eqRect = eqSlotEl.worldBound;
            eqAnchor = new Vector2(eqRect.center.x, eqRect.yMin);
        }

        if (_swapper != null)
        {
            _swapper.ShowCustomTooltipAboveWorldPosition(
                _detailTooltip,
                invAnchor,
                offsetPx: 10f,
                edgePaddingPx: TooltipEdgePaddingPx,
                fallbackWidthPx: TooltipFallbackWidthPx,
                fallbackHeightPx: TooltipFallbackHeightPx
            );

            _swapper.ShowCustomTooltipAboveWorldPosition(
                _detailTooltipCompare,
                eqAnchor,
                offsetPx: 10f,
                edgePaddingPx: TooltipEdgePaddingPx,
                fallbackWidthPx: TooltipFallbackWidthPx,
                fallbackHeightPx: TooltipFallbackHeightPx
            );
        }
        else
        {
            _detailTooltip.style.display = DisplayStyle.Flex;
            _detailTooltipCompare.style.display = DisplayStyle.Flex;
            _detailTooltip.BringToFront();
            _detailTooltipCompare.BringToFront();
        }

        return true;
    }
}
