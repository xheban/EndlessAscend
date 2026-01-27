using System;
using System.Collections.Generic;
using MyGame.Run;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ActiveSpellsSectionController
{
    private sealed class SlotTooltipHandlers
    {
        public VisualElement slot;
        public EventCallback<PointerEnterEvent> enter;
        public EventCallback<PointerLeaveEvent> leave;
        public EventCallback<PointerOutEvent> outCb;
        public EventCallback<PointerMoveEvent> move;
    }

    private readonly Sprite _lockedIcon;
    private VisualElement _panelRoot;
    private readonly Dictionary<int, VisualElement> _slots = new();
    private readonly Dictionary<int, SlotTooltipHandlers> _tooltipHandlers = new();

    private int _unlockedSlots = 1;
    private ScreenSwapper _screenSwapper;

    // âś… drag start from active slot (slotIndex, evt)
    public Action<int, PointerDownEvent> RequestBeginDragFromSlot;
    public Func<bool> IsDraggingSpell;

    public ActiveSpellsSectionController(string lockedIconResourcesPath)
    {
        _lockedIcon = !string.IsNullOrWhiteSpace(lockedIconResourcesPath)
            ? Resources.Load<Sprite>(lockedIconResourcesPath)
            : null;
    }

    public void Bind(VisualElement spellsPanel, int unlockedSlots, ScreenSwapper screenSwapper)
    {
        _panelRoot = spellsPanel;
        _unlockedSlots = Mathf.Max(0, unlockedSlots);
        _screenSwapper = screenSwapper;

        CacheSlots();
        Refresh();
    }

    public void Unbind()
    {
        UnregisterAllTooltipHandlers();
        _slots.Clear();
        _panelRoot = null;
        _screenSwapper = null;
        RequestBeginDragFromSlot = null;
        IsDraggingSpell = null;
    }

    public void SetUnlockedSlots(int unlockedSlots)
    {
        _unlockedSlots = Mathf.Max(0, unlockedSlots);
        Refresh();
    }

    private void CacheSlots()
    {
        _slots.Clear();

        if (_panelRoot == null)
            return;

        for (int slotIndex = 0; slotIndex < PlayerSpellbook.ActiveSlotCount; slotIndex++)
        {
            int uxmlIndex = slotIndex + 1;
            var ve = _panelRoot.Q<VisualElement>($"ActiveSpellSlot{uxmlIndex}");
            if (ve != null)
                _slots[slotIndex] = ve;
        }
    }

    public void Refresh()
    {
        if (_panelRoot == null)
            return;

        if (!RunSession.IsInitialized || RunSession.Spellbook == null)
            return;

        var book = RunSession.Spellbook;
        var config = GameConfigProvider.Instance;
        if (config == null || config.SpellDatabase == null)
            return;
        var db = config.SpellDatabase;

        foreach (var kv in _slots)
        {
            int slotIndex = kv.Key;
            var slotRoot = kv.Value;

            bool isUnlocked = slotIndex < _unlockedSlots;

            if (isUnlocked)
                slotRoot.RemoveFromClassList("disabled-spell-slot");
            else
                slotRoot.AddToClassList("disabled-spell-slot");

            var iconHost = slotRoot.Q<VisualElement>("Icon");
            var nameLabel = slotRoot.Q<Label>("Name");
            if (iconHost == null || nameLabel == null)
                continue;

            // âś… IMPORTANT: remove placeholder background AND any previous Image child
            iconHost.Clear();
            iconHost.style.backgroundImage = StyleKeyword.None;

            if (!isUnlocked)
            {
                nameLabel.text = "Locked";

                if (_lockedIcon != null)
                    SetupIconAsImage(iconHost, _lockedIcon);

                UnregisterTooltipHandlers(slotIndex);
                continue;
            }

            var entry = book.GetSpellInSlot(slotIndex);
            if (entry == null)
            {
                nameLabel.text = "Empty";
                UnregisterTooltipHandlers(slotIndex);
                continue;
            }

            var def = db.GetById(entry.spellId);
            nameLabel.text = def != null ? def.displayName : entry.spellId;

            var sprite = def != null ? def.icon : null;
            if (sprite != null)
            {
                var img = SetupIconAsImage(iconHost, sprite);

                // âś… only icon is draggable
                img.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    RequestBeginDragFromSlot?.Invoke(slotIndex, evt);
                    evt.StopPropagation();
                });
            }

            RegisterTooltipHandlers(slotIndex, slotRoot, entry.spellId);
        }
    }

    // Centers icon and avoids â€śbottom-right driftâ€ť
    private static Image SetupIconAsImage(VisualElement iconHost, Sprite sprite)
    {
        iconHost.style.justifyContent = Justify.Center;
        iconHost.style.alignItems = Align.Center;

        var img = new Image
        {
            pickingMode = PickingMode.Position,
            scaleMode = ScaleMode.ScaleToFit,
            sprite = sprite,
        };

        // Slight inset looks better than full size
        img.style.width = Length.Percent(92);
        img.style.height = Length.Percent(92);

        iconHost.Add(img);
        return img;
    }

    private bool IsPointerOverSpellDetailTooltip(Vector2 pointerPosition)
    {
        if (_screenSwapper == null)
            return false;

        var tooltip = _screenSwapper.GetCustomTooltipElement("SpellDetailTooltip");
        if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
            return false;

        return tooltip.worldBound.Contains(pointerPosition);
    }

    private void RegisterTooltipHandlers(int slotIndex, VisualElement slotRoot, string spellId)
    {
        if (slotRoot == null || string.IsNullOrWhiteSpace(spellId))
            return;

        UnregisterTooltipHandlers(slotIndex);

        var handlers = new SlotTooltipHandlers { slot = slotRoot };

        handlers.enter = evt =>
        {
            try
            {
                if (IsDraggingSpell?.Invoke() == true)
                    return;

                if (_screenSwapper == null)
                {
                    Debug.LogWarning(
                        "ActiveSpellsSectionController: ScreenSwapper not found on hover."
                    );
                    return;
                }

                var tooltip = _screenSwapper.GetCustomTooltipElement("SpellDetailTooltip");
                if (tooltip != null)
                {
                    bool ok = MyGame.Helpers.SpellDetailTooltipBuilder.TryPopulateForSpellId(
                        tooltip,
                        spellId,
                        learned: true,
                        swapper: _screenSwapper
                    );

                    if (!ok)
                        Debug.LogWarning(
                            $"SpellDetailTooltipBuilder failed to populate for '{spellId}'."
                        );

                    var worldPos = evt.position;
                    _screenSwapper.ShowCustomTooltipAtWorldPosition(tooltip, worldPos);
                }
                else
                {
                    _screenSwapper.ShowTooltipAtElement((VisualElement)evt.currentTarget, spellId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        };

        handlers.leave = evt =>
        {
            if (_screenSwapper != null && _screenSwapper.TryFreezeCustomTooltipIfAltHeld())
                return;
            var anchor = evt.currentTarget as VisualElement;
            if (anchor != null && anchor.worldBound.Contains(evt.position))
                return;
            if (IsPointerOverSpellDetailTooltip(evt.position))
                return;
            _screenSwapper?.HideCustomTooltip();
            _screenSwapper?.HideTooltip();
        };

        handlers.outCb = evt =>
        {
            if (_screenSwapper != null && _screenSwapper.TryFreezeCustomTooltipIfAltHeld())
                return;
            var anchor = evt.currentTarget as VisualElement;
            if (anchor != null && anchor.worldBound.Contains(evt.position))
                return;
            if (IsPointerOverSpellDetailTooltip(evt.position))
                return;
            _screenSwapper?.HideCustomTooltip();
            _screenSwapper?.HideTooltip();
        };

        handlers.move = evt =>
        {
            if (IsDraggingSpell?.Invoke() == true)
                return;
            if (_screenSwapper == null)
                return;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return;

            var tooltip = _screenSwapper.GetCustomTooltipElement("SpellDetailTooltip");
            if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
                return;

            _screenSwapper.PositionCustomTooltipAtWorldPosition(tooltip, evt.position);
        };

        slotRoot.RegisterCallback(handlers.enter, TrickleDown.TrickleDown);
        slotRoot.RegisterCallback(handlers.leave, TrickleDown.TrickleDown);
        slotRoot.RegisterCallback(handlers.outCb, TrickleDown.TrickleDown);
        slotRoot.RegisterCallback(handlers.move, TrickleDown.TrickleDown);

        _tooltipHandlers[slotIndex] = handlers;
    }

    private void UnregisterTooltipHandlers(int slotIndex)
    {
        if (!_tooltipHandlers.TryGetValue(slotIndex, out var handlers) || handlers == null)
            return;

        var slot = handlers.slot;
        if (slot != null)
        {
            if (handlers.enter != null)
                slot.UnregisterCallback(handlers.enter, TrickleDown.TrickleDown);
            if (handlers.leave != null)
                slot.UnregisterCallback(handlers.leave, TrickleDown.TrickleDown);
            if (handlers.outCb != null)
                slot.UnregisterCallback(handlers.outCb, TrickleDown.TrickleDown);
            if (handlers.move != null)
                slot.UnregisterCallback(handlers.move, TrickleDown.TrickleDown);
        }

        _tooltipHandlers.Remove(slotIndex);
    }

    private void UnregisterAllTooltipHandlers()
    {
        foreach (var kv in _tooltipHandlers)
        {
            var handlers = kv.Value;
            var slot = handlers?.slot;
            if (slot == null)
                continue;
            if (handlers.enter != null)
                slot.UnregisterCallback(handlers.enter, TrickleDown.TrickleDown);
            if (handlers.leave != null)
                slot.UnregisterCallback(handlers.leave, TrickleDown.TrickleDown);
            if (handlers.outCb != null)
                slot.UnregisterCallback(handlers.outCb, TrickleDown.TrickleDown);
            if (handlers.move != null)
                slot.UnregisterCallback(handlers.move, TrickleDown.TrickleDown);
        }

        _tooltipHandlers.Clear();
    }
}
