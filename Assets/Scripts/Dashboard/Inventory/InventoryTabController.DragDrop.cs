using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class InventoryTabController
{
    private const float DragStartThresholdPx = 6f;

    private enum EquipmentDragSource
    {
        None,
        InventoryGrid,
        EquippedSlot,
    }

    private sealed class InventorySlotUserData
    {
        public GridKind gridKind;
        public int slotIndex;
    }

    private sealed class EquippedSlotUserData
    {
        public MyName.Equipment.EquipmentSlot slot;
    }

    private sealed class ActiveCombatSlotUserData
    {
        public int slotIndex;
    }

    private sealed class AvatarDropUserData { }

    private bool _dragHandlersRegistered;
    private EventCallback<PointerMoveEvent> _onRootPointerMove;
    private EventCallback<PointerUpEvent> _onRootPointerUp;
    private EventCallback<PointerCaptureOutEvent> _onRootPointerCaptureOut;

    private VisualElement _characterAvatarElement;

    private bool _dragCandidateActive;
    private int _dragCandidatePointerId;
    private EquipmentDragSource _dragCandidateSource;
    private int _dragCandidateFromEquipmentSlotIndex;
    private MyName.Equipment.EquipmentSlot _dragCandidateFromEquippedSlot;
    private Vector2 _dragCandidateStartPos;
    private string _dragCandidateInstanceId;

    private bool _itemDragCandidateActive;
    private int _itemDragCandidatePointerId;
    private int _itemDragCandidateFromItemsSlotIndex;
    private Vector2 _itemDragCandidateStartPos;
    private string _itemDragCandidateItemId;

    private bool _isDraggingEquipment;
    private string _draggingInstanceId;
    private EquipmentDragSource _draggingSource;
    private int _draggingFromEquipmentSlotIndex;
    private MyName.Equipment.EquipmentSlot _draggingFromEquippedSlot;

    private bool _isDraggingItem;
    private string _draggingItemId;
    private int _draggingFromItemsSlotIndex;

    private VisualElement _dragGhost;

    // Master ordering for equipment inventory (non-equipped), independent of search filtering.
    private readonly System.Collections.Generic.List<string> _equipmentInventoryOrderAll = new();

    private bool _equipmentInventoryOrderLoadedFromSave;
    private bool _equipmentInventoryLayoutDirty;

    private void RegisterDragDropHandlersIfNeeded()
    {
        if (_dragHandlersRegistered)
            return;
        if (_root == null)
            return;

        _characterAvatarElement = _root.Q<VisualElement>("CharacterAvatar");
        if (_characterAvatarElement != null)
            _characterAvatarElement.userData = new AvatarDropUserData();

        _onRootPointerMove = OnRootPointerMove;
        _onRootPointerUp = OnRootPointerUp;
        _onRootPointerCaptureOut = OnRootPointerCaptureOut;

        _root.RegisterCallback(_onRootPointerMove, TrickleDown.TrickleDown);
        _root.RegisterCallback(_onRootPointerUp, TrickleDown.TrickleDown);
        _root.RegisterCallback(_onRootPointerCaptureOut, TrickleDown.TrickleDown);

        _dragHandlersRegistered = true;
    }

    private void UnregisterDragDropHandlers()
    {
        if (!_dragHandlersRegistered)
            return;

        if (_root != null)
        {
            if (_onRootPointerMove != null)
                _root.UnregisterCallback(_onRootPointerMove, TrickleDown.TrickleDown);
            if (_onRootPointerUp != null)
                _root.UnregisterCallback(_onRootPointerUp, TrickleDown.TrickleDown);
            if (_onRootPointerCaptureOut != null)
                _root.UnregisterCallback(_onRootPointerCaptureOut, TrickleDown.TrickleDown);
        }

        CancelEquipmentDrag();
        CancelItemDrag();

        _onRootPointerMove = null;
        _onRootPointerUp = null;
        _onRootPointerCaptureOut = null;
        _dragHandlersRegistered = false;
    }

    private void OnRootPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        // Safety: if capture gets lost (window focus change etc), stop drag.
        if (_dragCandidateActive || _isDraggingEquipment)
            CancelEquipmentDrag();

        if (_itemDragCandidateActive || _isDraggingItem)
            CancelItemDrag();
    }

    private void TryBeginEquipmentDragCandidateFromInventorySlot(
        int equipmentSlotIndex,
        int pointerId,
        Vector2 pointerWorld
    )
    {
        if (!_isVisible)
            return;

        if (equipmentSlotIndex < 0 || equipmentSlotIndex >= _equipmentSlotInstanceIds.Count)
            return;

        var instanceId = _equipmentSlotInstanceIds[equipmentSlotIndex];
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        int masterIndex = IndexOfId(_equipmentInventoryOrderAll, instanceId);
        if (masterIndex < 0)
            masterIndex = equipmentSlotIndex;

        _dragCandidateActive = true;
        _dragCandidatePointerId = pointerId;
        _dragCandidateSource = EquipmentDragSource.InventoryGrid;
        _dragCandidateFromEquipmentSlotIndex = masterIndex;
        _dragCandidateFromEquippedSlot = MyName.Equipment.EquipmentSlot.None;
        _dragCandidateStartPos = pointerWorld;
        _dragCandidateInstanceId = instanceId;

        if (_root != null)
            _root.CapturePointer(pointerId);

        HideAllTooltips();
    }

    private void TryBeginEquipmentDragCandidateFromEquippedSlot(
        MyName.Equipment.EquipmentSlot slot,
        int pointerId,
        Vector2 pointerWorld
    )
    {
        if (!_isVisible)
            return;

        if (slot == MyName.Equipment.EquipmentSlot.None)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        var instanceId = equipment.GetEquippedInstanceId(slot);
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        _dragCandidateActive = true;
        _dragCandidatePointerId = pointerId;
        _dragCandidateSource = EquipmentDragSource.EquippedSlot;
        _dragCandidateFromEquipmentSlotIndex = -1;
        _dragCandidateFromEquippedSlot = slot;
        _dragCandidateStartPos = pointerWorld;
        _dragCandidateInstanceId = instanceId;

        if (_root != null)
            _root.CapturePointer(pointerId);

        HideAllTooltips();
    }

    private void OnRootPointerMove(PointerMoveEvent evt)
    {
        if (!_isVisible)
            return;

        // Equipment drag has priority.
        if (_dragCandidateActive && !_isDraggingEquipment)
        {
            if (evt.pointerId != _dragCandidatePointerId)
                return;

            float dist = Vector2.Distance(evt.position, _dragCandidateStartPos);
            if (dist >= DragStartThresholdPx)
            {
                StartEquipmentDragFromCandidate(evt.position);
            }

            return;
        }

        if (_isDraggingEquipment)
        {
            if (evt.pointerId != _dragCandidatePointerId)
                return;

            UpdateDragGhostPosition(evt.position);
            return;
        }

        // Item drag.
        if (_itemDragCandidateActive && !_isDraggingItem)
        {
            if (evt.pointerId != _itemDragCandidatePointerId)
                return;

            float dist = Vector2.Distance(evt.position, _itemDragCandidateStartPos);
            if (dist >= DragStartThresholdPx)
            {
                StartItemDragFromCandidate(evt.position);
            }

            return;
        }

        if (_isDraggingItem)
        {
            if (evt.pointerId != _itemDragCandidatePointerId)
                return;

            UpdateDragGhostPosition(evt.position);
        }
    }

    private void OnRootPointerUp(PointerUpEvent evt)
    {
        if (!_isVisible)
            return;

        if (_dragCandidateActive && evt.pointerId == _dragCandidatePointerId)
        {
            if (_isDraggingEquipment)
                CompleteEquipmentDrag(evt.position);
            else
                CancelEquipmentDrag();

            return;
        }

        if (_itemDragCandidateActive && evt.pointerId == _itemDragCandidatePointerId)
        {
            if (_isDraggingItem)
                CompleteItemDrag(evt.position);
            else
                CancelItemDrag();
        }
    }

    private void StartEquipmentDragFromCandidate(Vector2 pointerWorld)
    {
        _isDraggingEquipment = true;
        _draggingInstanceId = _dragCandidateInstanceId;
        _draggingSource = _dragCandidateSource;
        _draggingFromEquipmentSlotIndex = _dragCandidateFromEquipmentSlotIndex;
        _draggingFromEquippedSlot = _dragCandidateFromEquippedSlot;

        EnsureDragGhost();
        SetDragGhostIconForInstance(_draggingInstanceId);
        UpdateDragGhostPosition(pointerWorld);

        HideAllTooltips();
    }

    private void CompleteEquipmentDrag(Vector2 pointerWorld)
    {
        var target = _root?.panel?.Pick(pointerWorld);

        // Walk up the tree to find any drop target userData.
        if (
            TryFindInventorySlotTarget(target, out var gridKind, out var toSlotIndex)
            && gridKind == GridKind.Equipment
        )
        {
            HandleDropOntoEquipmentInventory(toSlotIndex);
            CancelEquipmentDrag(releasePointer: true);
            return;
        }

        if (TryFindEquippedSlotTarget(target, out var equippedSlot))
        {
            HandleDropOntoEquippedSlot(equippedSlot);
            CancelEquipmentDrag(releasePointer: true);
            return;
        }

        if (TryFindAvatarTarget(target))
        {
            HandleDropOntoAvatar();
            CancelEquipmentDrag(releasePointer: true);
            return;
        }

        if (TryFindActiveCombatSlotTarget(target, out var activeCombatSlotIndex))
        {
            TryAssignActiveCombatSlotEquipment(activeCombatSlotIndex, _draggingInstanceId);
            CancelEquipmentDrag(releasePointer: true);
            return;
        }

        CancelEquipmentDrag(releasePointer: true);
    }

    private void CancelEquipmentDrag(bool releasePointer = true)
    {
        _dragCandidateActive = false;
        _isDraggingEquipment = false;
        _draggingInstanceId = null;
        _draggingSource = EquipmentDragSource.None;
        _draggingFromEquippedSlot = MyName.Equipment.EquipmentSlot.None;
        _dragCandidateInstanceId = null;
        _dragCandidateSource = EquipmentDragSource.None;
        _dragCandidateFromEquippedSlot = MyName.Equipment.EquipmentSlot.None;
        _draggingFromEquipmentSlotIndex = -1;
        _dragCandidateFromEquipmentSlotIndex = -1;

        if (releasePointer && _root != null && _root.HasPointerCapture(_dragCandidatePointerId))
            _root.ReleasePointer(_dragCandidatePointerId);

        _dragCandidatePointerId = 0;

        if (_dragGhost != null)
            _dragGhost.style.display = DisplayStyle.None;
    }

    private void TryBeginItemDragCandidateFromItemsSlot(
        int itemsSlotIndex,
        int pointerId,
        Vector2 pointerWorld
    )
    {
        if (!_isVisible)
            return;

        // Do not allow starting an item drag while equipment drag is active.
        if (_dragCandidateActive || _isDraggingEquipment)
            return;
        if (_itemDragCandidateActive || _isDraggingItem)
            return;

        if (itemsSlotIndex < 0 || itemsSlotIndex >= _itemsSlotItemIds.Count)
            return;

        var itemId = _itemsSlotItemIds[itemsSlotIndex];
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        _itemDragCandidateActive = true;
        _itemDragCandidatePointerId = pointerId;
        _itemDragCandidateFromItemsSlotIndex = itemsSlotIndex;
        _itemDragCandidateStartPos = pointerWorld;
        _itemDragCandidateItemId = itemId;

        if (_root != null)
            _root.CapturePointer(pointerId);

        HideAllTooltips();
    }

    private void StartItemDragFromCandidate(Vector2 pointerWorld)
    {
        _isDraggingItem = true;
        _draggingItemId = _itemDragCandidateItemId;
        _draggingFromItemsSlotIndex = _itemDragCandidateFromItemsSlotIndex;

        EnsureDragGhost();
        SetDragGhostIconForItem(_draggingItemId);
        UpdateDragGhostPosition(pointerWorld);

        HideAllTooltips();
    }

    private void CompleteItemDrag(Vector2 pointerWorld)
    {
        var target = _root?.panel?.Pick(pointerWorld);

        if (TryFindActiveCombatSlotTarget(target, out var activeCombatSlotIndex))
        {
            TryAssignActiveCombatSlotItem(activeCombatSlotIndex, _draggingItemId);
            CancelItemDrag(releasePointer: true);
            return;
        }

        CancelItemDrag(releasePointer: true);
    }

    private void CancelItemDrag(bool releasePointer = true)
    {
        _itemDragCandidateActive = false;
        _isDraggingItem = false;
        _draggingItemId = null;
        _draggingFromItemsSlotIndex = -1;
        _itemDragCandidateItemId = null;
        _itemDragCandidateFromItemsSlotIndex = -1;

        if (releasePointer && _root != null && _root.HasPointerCapture(_itemDragCandidatePointerId))
            _root.ReleasePointer(_itemDragCandidatePointerId);

        _itemDragCandidatePointerId = 0;

        if (_dragGhost != null)
            _dragGhost.style.display = DisplayStyle.None;
    }

    private void SetDragGhostIconForItem(string itemId)
    {
        if (_dragGhost == null)
            return;

        var icon = GameConfigProvider.Instance?.ItemDatabase?.GetIcon(itemId);
        _dragGhost.style.backgroundImage =
            icon != null ? new StyleBackground(icon) : StyleKeyword.None;
    }

    private void EnsureDragGhost()
    {
        if (_root == null)
            return;

        if (_dragGhost != null)
        {
            _dragGhost.style.display = DisplayStyle.Flex;
            _dragGhost.BringToFront();
            return;
        }

        _dragGhost = new VisualElement();
        _dragGhost.name = "EquipmentDragGhost";
        _dragGhost.pickingMode = PickingMode.Ignore;
        _dragGhost.style.position = Position.Absolute;
        _dragGhost.style.width = 54;
        _dragGhost.style.height = 54;
        _dragGhost.style.opacity = 0.9f;
        _dragGhost.AddToClassList("equip-slot-icon");
        _dragGhost.style.display = DisplayStyle.Flex;

        _root.Add(_dragGhost);
        _dragGhost.BringToFront();
    }

    private void SetDragGhostIconForInstance(string instanceId)
    {
        if (_dragGhost == null)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
        {
            _dragGhost.style.backgroundImage = StyleKeyword.None;
            return;
        }

        var inst = equipment.GetInstance(instanceId);
        var icon =
            inst != null
                ? GameConfigProvider.Instance?.EquipmentDatabase?.GetIcon(inst.equipmentId)
                : null;

        _dragGhost.style.backgroundImage =
            icon != null ? new StyleBackground(icon) : StyleKeyword.None;
    }

    private void UpdateDragGhostPosition(Vector2 pointerWorld)
    {
        if (_dragGhost == null || _root == null)
            return;

        var local = _root.WorldToLocal(pointerWorld);

        float halfW =
            _dragGhost.resolvedStyle.width > 1f ? _dragGhost.resolvedStyle.width * 0.5f : 27f;
        float halfH =
            _dragGhost.resolvedStyle.height > 1f ? _dragGhost.resolvedStyle.height * 0.5f : 27f;

        _dragGhost.style.left = local.x - halfW;
        _dragGhost.style.top = local.y - halfH;
        _dragGhost.BringToFront();
    }

    private static bool TryFindInventorySlotTarget(
        VisualElement start,
        out GridKind gridKind,
        out int slotIndex
    )
    {
        var e = start;
        while (e != null)
        {
            if (e.userData is InventorySlotUserData ud)
            {
                gridKind = ud.gridKind;
                slotIndex = ud.slotIndex;
                return true;
            }
            e = e.parent;
        }

        gridKind = default;
        slotIndex = -1;
        return false;
    }

    private static bool TryFindEquippedSlotTarget(
        VisualElement start,
        out MyName.Equipment.EquipmentSlot slot
    )
    {
        var e = start;
        while (e != null)
        {
            if (e.userData is EquippedSlotUserData ud)
            {
                slot = ud.slot;
                return true;
            }
            e = e.parent;
        }

        slot = MyName.Equipment.EquipmentSlot.None;
        return false;
    }

    private static bool TryFindAvatarTarget(VisualElement start)
    {
        var e = start;
        while (e != null)
        {
            if (e.userData is AvatarDropUserData)
                return true;
            e = e.parent;
        }

        return false;
    }

    private static bool TryFindActiveCombatSlotTarget(VisualElement start, out int slotIndex)
    {
        var e = start;
        while (e != null)
        {
            if (e.userData is ActiveCombatSlotUserData ud)
            {
                slotIndex = ud.slotIndex;
                return true;
            }
            e = e.parent;
        }

        slotIndex = -1;
        return false;
    }

    private void HandleDropOntoEquipmentInventory(int toVisibleSlotIndex)
    {
        if (!_isVisible)
            return;

        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        if (_draggingSource == EquipmentDragSource.InventoryGrid)
        {
            // EI -> EI: move/swap in-place (no shifting).
            MoveOrSwapInventoryInstanceInPlace(
                instanceId: _draggingInstanceId,
                fromSlotIndex: _draggingFromEquipmentSlotIndex,
                toSlotIndex: toVisibleSlotIndex
            );
            _equippedItems?.RefreshFromSave();
            RefreshEquipmentGridIcons();
            return;
        }

        if (
            _draggingSource == EquipmentDragSource.EquippedSlot
            && _draggingFromEquippedSlot != MyName.Equipment.EquipmentSlot.None
        )
        {
            HandleDropFromEquippedSlotOntoEquipmentInventory(toVisibleSlotIndex);
        }
    }

    private void HandleDropOntoEquippedSlot(MyName.Equipment.EquipmentSlot equippedSlot)
    {
        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        // EE -> EE swapping is only meaningful if we have distinct slots (e.g., Ring1/Ring2).
        // The current equipment model has only one Ring slot, so we treat EE->EE as no-op.
        if (_draggingSource == EquipmentDragSource.EquippedSlot)
        {
            if (_draggingFromEquippedSlot == equippedSlot)
                return;
            return;
        }

        // EI -> EE: equip, and if the slot was occupied, swap the previous equipped item back
        // into the inventory at the dragged item's original position.
        TryEquipDraggedInventoryItemToEquippedSlotWithSwap(equippedSlot);
    }

    private void HandleDropOntoAvatar()
    {
        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        // Avatar drop is an auto-equip for EI items.
        if (_draggingSource != EquipmentDragSource.InventoryGrid)
            return;

        TryEquipDraggedInventoryItemToCorrectSlotWithSwap();
    }

    private void HandleDropFromEquippedSlotOntoEquipmentInventory(int toVisibleSlotIndex)
    {
        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        var fromSlot = _draggingFromEquippedSlot;
        if (fromSlot == MyName.Equipment.EquipmentSlot.None)
            return;

        string targetId = null;
        if (toVisibleSlotIndex >= 0 && toVisibleSlotIndex < _equipmentSlotInstanceIds.Count)
            targetId = _equipmentSlotInstanceIds[toVisibleSlotIndex];

        // EE -> EI empty: remove from EE and insert in EI.
        if (string.IsNullOrWhiteSpace(targetId))
        {
            equipment.Equip(fromSlot, instanceId: null);
            PlaceInventoryInstanceAtSlot(_draggingInstanceId, toVisibleSlotIndex);
            _equippedItems?.RefreshFromSave();
            RefreshEquipmentGridIcons();
            return;
        }

        var targetSlot = GetEquipmentSlotForInstanceId(targetId);
        if (targetSlot == MyName.Equipment.EquipmentSlot.None)
        {
            // Unknown item type; treat as a plain unequip+insert.
            equipment.Equip(fromSlot, instanceId: null);
            PlaceInventoryInstanceAtSlot(_draggingInstanceId, toVisibleSlotIndex);
            _equippedItems?.RefreshFromSave();
            RefreshEquipmentGridIcons();
            return;
        }

        if (targetSlot == fromSlot)
        {
            // EE -> EI (same slot item): swap them.
            SwapEquippedSlotItemWithInventoryItemAtIndex(
                fromSlot,
                equippedId: _draggingInstanceId,
                inventoryId: targetId,
                inventorySlotIndex: toVisibleSlotIndex
            );
            _equippedItems?.RefreshFromSave();
            RefreshEquipmentGridIcons();
            return;
        }

        // EE -> EI (different slot item): remove EE item, place it into this EI position,
        // and move the previous EI item into the first available empty slot.
        ReplaceInventoryItemWithUnequippedAndMovePreviousToFirstEmpty(
            unequippedId: _draggingInstanceId,
            previousInventoryId: targetId,
            inventorySlotIndex: toVisibleSlotIndex,
            fromEquippedSlot: fromSlot
        );
        _equippedItems?.RefreshFromSave();
        RefreshEquipmentGridIcons();
    }

    private MyName.Equipment.EquipmentSlot GetEquipmentSlotForInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return MyName.Equipment.EquipmentSlot.None;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return MyName.Equipment.EquipmentSlot.None;

        var inst = equipment.GetInstance(instanceId);
        if (inst == null)
            return MyName.Equipment.EquipmentSlot.None;

        var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(inst.equipmentId);
        return def != null ? def.slot : MyName.Equipment.EquipmentSlot.None;
    }

    private void TryEquipDraggedInventoryItemToEquippedSlotWithSwap(
        MyName.Equipment.EquipmentSlot equippedSlot
    )
    {
        if (_draggingSource != EquipmentDragSource.InventoryGrid)
            return;
        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        var inst = equipment.GetInstance(_draggingInstanceId);
        if (inst == null)
            return;

        var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(inst.equipmentId);
        if (def == null || def.slot == MyName.Equipment.EquipmentSlot.None)
            return;

        if (def.slot != equippedSlot)
            return;

        if (!CanEquip(def))
            return;

        var previousEquippedId = equipment.GetEquippedInstanceId(equippedSlot);
        int fromMasterIndex = _draggingFromEquipmentSlotIndex;
        if (fromMasterIndex < 0)
            fromMasterIndex = IndexOfId(_equipmentInventoryOrderAll, _draggingInstanceId);

        equipment.Equip(equippedSlot, _draggingInstanceId);

        // The dragged item leaves EI; keep the slot position stable.
        ClearAllInventoryOccurrences(_draggingInstanceId);
        if (fromMasterIndex >= 0 && fromMasterIndex < _equipmentInventoryOrderAll.Count)
            _equipmentInventoryOrderAll[fromMasterIndex] = null;

        // If slot was occupied, put previous equipped item into the same EI slot.
        if (!string.IsNullOrWhiteSpace(previousEquippedId))
        {
            EnsureInventoryOrderSize(fromMasterIndex + 1);
            ClearAllInventoryOccurrences(previousEquippedId);
            _equipmentInventoryOrderAll[fromMasterIndex] = previousEquippedId;
        }
        MarkEquipmentInventoryLayoutDirty();

        _equippedItems?.RefreshFromSave();
        RefreshEquipmentGridIcons();
    }

    private void TryEquipDraggedInventoryItemToCorrectSlotWithSwap()
    {
        if (_draggingSource != EquipmentDragSource.InventoryGrid)
            return;
        if (string.IsNullOrWhiteSpace(_draggingInstanceId))
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        var inst = equipment.GetInstance(_draggingInstanceId);
        if (inst == null)
            return;

        var def = GameConfigProvider.Instance?.EquipmentDatabase?.GetById(inst.equipmentId);
        if (def == null || def.slot == MyName.Equipment.EquipmentSlot.None)
            return;

        if (!CanEquip(def))
            return;

        var targetSlot = def.slot;
        var previousEquippedId = equipment.GetEquippedInstanceId(targetSlot);
        int fromMasterIndex = _draggingFromEquipmentSlotIndex;
        if (fromMasterIndex < 0)
            fromMasterIndex = IndexOfId(_equipmentInventoryOrderAll, _draggingInstanceId);

        equipment.Equip(targetSlot, _draggingInstanceId);

        ClearAllInventoryOccurrences(_draggingInstanceId);
        if (fromMasterIndex >= 0)
        {
            EnsureInventoryOrderSize(fromMasterIndex + 1);
            _equipmentInventoryOrderAll[fromMasterIndex] = null;
        }

        if (!string.IsNullOrWhiteSpace(previousEquippedId) && fromMasterIndex >= 0)
        {
            ClearAllInventoryOccurrences(previousEquippedId);
            _equipmentInventoryOrderAll[fromMasterIndex] = previousEquippedId;
        }
        MarkEquipmentInventoryLayoutDirty();

        _equippedItems?.RefreshFromSave();
        RefreshEquipmentGridIcons();
    }

    private void SwapEquippedSlotItemWithInventoryItemAtIndex(
        MyName.Equipment.EquipmentSlot equippedSlot,
        string equippedId,
        string inventoryId,
        int inventorySlotIndex
    )
    {
        if (equippedSlot == MyName.Equipment.EquipmentSlot.None)
            return;
        if (string.IsNullOrWhiteSpace(equippedId) || string.IsNullOrWhiteSpace(inventoryId))
            return;
        if (inventorySlotIndex < 0)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        // Equip the inventory item into the equipped slot (this unequips equippedId).
        equipment.Equip(equippedSlot, inventoryId);

        // Update EI in-place at the exact drop slot: replace that slot with the old equipped item.
        EnsureInventoryOrderSize(inventorySlotIndex + 1);
        ClearAllInventoryOccurrences(inventoryId);
        ClearAllInventoryOccurrences(equippedId);
        _equipmentInventoryOrderAll[inventorySlotIndex] = equippedId;
        MarkEquipmentInventoryLayoutDirty();
    }

    private void ReplaceInventoryItemWithUnequippedAndMovePreviousToFirstEmpty(
        string unequippedId,
        string previousInventoryId,
        int inventorySlotIndex,
        MyName.Equipment.EquipmentSlot fromEquippedSlot
    )
    {
        if (
            string.IsNullOrWhiteSpace(unequippedId)
            || string.IsNullOrWhiteSpace(previousInventoryId)
        )
            return;

        if (inventorySlotIndex < 0)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        if (fromEquippedSlot == MyName.Equipment.EquipmentSlot.None)
            return;

        // Remove from EE.
        equipment.Equip(fromEquippedSlot, instanceId: null);

        int visibleCap = _equipmentSlotInstanceIds != null ? _equipmentSlotInstanceIds.Count : 0;
        if (visibleCap <= 0)
            visibleCap = _equipmentInventoryOrderAll.Count;

        EnsureInventoryOrderSize(Mathf.Max(inventorySlotIndex + 1, visibleCap));
        ClearAllInventoryOccurrences(unequippedId);
        ClearAllInventoryOccurrences(previousInventoryId);

        // Place the unequipped item into the exact EI slot where the user dropped.
        _equipmentInventoryOrderAll[inventorySlotIndex] = unequippedId;

        // Move the previous EI item into the first empty visible slot.
        int firstEmpty = FindFirstEmptyInventorySlotIndex(visibleCap);
        if (firstEmpty >= 0)
        {
            _equipmentInventoryOrderAll[firstEmpty] = previousInventoryId;
            return;
        }

        // No empty slot available; append so it isn't lost.
        _equipmentInventoryOrderAll.Add(previousInventoryId);

        MarkEquipmentInventoryLayoutDirty();
    }

    private int FindFirstEmptyInventorySlotIndex(int visibleCap)
    {
        if (visibleCap <= 0)
            return -1;

        int cap = Mathf.Min(visibleCap, _equipmentInventoryOrderAll.Count);
        for (int i = 0; i < cap; i++)
        {
            if (string.IsNullOrWhiteSpace(_equipmentInventoryOrderAll[i]))
                return i;
        }

        return -1;
    }

    private void MoveOrSwapInventoryInstanceInPlace(
        string instanceId,
        int fromSlotIndex,
        int toSlotIndex
    )
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;
        if (fromSlotIndex < 0 || toSlotIndex < 0)
            return;

        EnsureInventoryOrderSize(Mathf.Max(fromSlotIndex, toSlotIndex) + 1);

        // If our stored fromSlotIndex is stale (e.g., filtering), locate the item.
        if (
            fromSlotIndex >= _equipmentInventoryOrderAll.Count
            || !IdEquals(_equipmentInventoryOrderAll[fromSlotIndex], instanceId)
        )
            fromSlotIndex = IndexOfId(_equipmentInventoryOrderAll, instanceId);

        if (fromSlotIndex < 0)
        {
            // Not currently in EI list (shouldn't happen), just place it.
            PlaceInventoryInstanceAtSlot(instanceId, toSlotIndex);
            return;
        }

        if (fromSlotIndex == toSlotIndex)
            return;

        string toId = _equipmentInventoryOrderAll[toSlotIndex];

        // Clear any duplicates first so we don't ever end up with the same id twice.
        ClearAllInventoryOccurrences(instanceId);

        if (string.IsNullOrWhiteSpace(toId))
        {
            _equipmentInventoryOrderAll[toSlotIndex] = instanceId;
            _equipmentInventoryOrderAll[fromSlotIndex] = null;
            MarkEquipmentInventoryLayoutDirty();
            return;
        }

        // Swap.
        _equipmentInventoryOrderAll[toSlotIndex] = instanceId;
        _equipmentInventoryOrderAll[fromSlotIndex] = toId;
        MarkEquipmentInventoryLayoutDirty();
    }

    private void PlaceInventoryInstanceAtSlot(string instanceId, int toSlotIndex)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;
        if (toSlotIndex < 0)
            return;

        EnsureInventoryOrderSize(toSlotIndex + 1);
        ClearAllInventoryOccurrences(instanceId);
        _equipmentInventoryOrderAll[toSlotIndex] = instanceId;
        MarkEquipmentInventoryLayoutDirty();
    }

    private void MarkEquipmentInventoryLayoutDirty()
    {
        _equipmentInventoryLayoutDirty = true;
    }

    private void EnsureEquipmentInventoryLayoutLoadedFromSave(int visibleCap)
    {
        if (_equipmentInventoryOrderLoadedFromSave)
            return;
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        var saved = SaveSession.Current.equipmentInventorySlots;
        if (saved == null || saved.Count == 0)
        {
            _equipmentInventoryOrderLoadedFromSave = true;
            return;
        }

        int maxIndex = -1;
        for (int i = 0; i < saved.Count; i++)
        {
            var e = saved[i];
            if (e == null)
                continue;
            if (e.slotIndex > maxIndex)
                maxIndex = e.slotIndex;
        }

        int minSize = Mathf.Max(visibleCap, maxIndex + 1);
        _equipmentInventoryOrderAll.Clear();
        EnsureInventoryOrderSize(minSize);

        for (int i = 0; i < saved.Count; i++)
        {
            var e = saved[i];
            if (e == null)
                continue;
            if (e.slotIndex < 0)
                continue;
            if (string.IsNullOrWhiteSpace(e.equipmentInstanceId))
                continue;

            EnsureInventoryOrderSize(e.slotIndex + 1);
            // Clear duplicates and place.
            ClearAllInventoryOccurrences(e.equipmentInstanceId);
            _equipmentInventoryOrderAll[e.slotIndex] = e.equipmentInstanceId;
        }

        _equipmentInventoryOrderLoadedFromSave = true;
    }

    private void PersistEquipmentInventoryLayoutToSave(bool saveNow)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        SaveSession.Current.equipmentInventorySlots ??=
            new System.Collections.Generic.List<SavedEquipmentInventorySlotEntry>();
        SaveSession.Current.equipmentInventorySlots.Clear();

        var seen = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase
        );
        for (int i = 0; i < _equipmentInventoryOrderAll.Count; i++)
        {
            var id = _equipmentInventoryOrderAll[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (!seen.Add(id))
                continue;

            SaveSession.Current.equipmentInventorySlots.Add(
                new SavedEquipmentInventorySlotEntry { slotIndex = i, equipmentInstanceId = id }
            );
        }

        if (saveNow)
            SaveSession.SaveNow();
    }

    private void EnsureInventoryOrderSize(int minCount)
    {
        if (minCount <= 0)
            return;
        while (_equipmentInventoryOrderAll.Count < minCount)
            _equipmentInventoryOrderAll.Add(null);
    }

    private void ClearAllInventoryOccurrences(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _equipmentInventoryOrderAll.Count == 0)
            return;

        for (int i = 0; i < _equipmentInventoryOrderAll.Count; i++)
        {
            if (IdEquals(_equipmentInventoryOrderAll[i], id))
                _equipmentInventoryOrderAll[i] = null;
        }
    }

    private static bool IdEquals(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int IndexOfId(System.Collections.Generic.List<string> list, string id)
    {
        if (list == null || list.Count == 0 || string.IsNullOrWhiteSpace(id))
            return -1;

        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i];
            if (string.IsNullOrWhiteSpace(v))
                continue;
            if (string.Equals(v, id, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    // Old equip helpers replaced by HandleDropOntoEquippedSlot/HandleDropOntoAvatar.
}
