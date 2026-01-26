using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Owns UI callback registration/unregistration for combat screen.
/// Keeps the controller free of callback arrays and button handler storage.
/// </summary>
public sealed class CombatInputBinder
{
    private VisualElement[] _spellSlots;
    private EventCallback<ClickEvent>[] _slotCallbacks;

    private VisualElement[] _itemSlots;
    private EventCallback<ClickEvent>[] _itemSlotCallbacks;

    private Button _runButton;
    private Action _runHandler;

    public void BindSpellSlots(VisualElement[] spellSlots, Action<string> onSpellClicked)
    {
        UnbindSpellSlots();

        if (spellSlots == null || spellSlots.Length == 0)
            return;

        _spellSlots = spellSlots;
        _slotCallbacks = new EventCallback<ClickEvent>[spellSlots.Length];

        for (int i = 0; i < spellSlots.Length; i++)
        {
            var slot = spellSlots[i];
            if (slot == null)
                continue;

            EventCallback<ClickEvent> cb = _ =>
            {
                string spellId = slot.userData as string;
                if (string.IsNullOrWhiteSpace(spellId))
                    return;

                onSpellClicked?.Invoke(spellId);
            };

            _slotCallbacks[i] = cb;
            slot.RegisterCallback(cb);
        }
    }

    public void UnbindSpellSlots()
    {
        if (_spellSlots == null || _slotCallbacks == null)
        {
            _spellSlots = null;
            _slotCallbacks = null;
            return;
        }

        int count = Mathf.Min(_spellSlots.Length, _slotCallbacks.Length);

        for (int i = 0; i < count; i++)
        {
            var slot = _spellSlots[i];
            var cb = _slotCallbacks[i];

            if (slot != null && cb != null)
                slot.UnregisterCallback(cb);
        }

        _spellSlots = null;
        _slotCallbacks = null;
    }

    public void BindItemSlots(VisualElement[] itemSlots, Action<int> onItemClicked)
    {
        UnbindItemSlots();

        if (itemSlots == null || itemSlots.Length == 0)
            return;

        _itemSlots = itemSlots;
        _itemSlotCallbacks = new EventCallback<ClickEvent>[itemSlots.Length];

        for (int i = 0; i < itemSlots.Length; i++)
        {
            var slot = itemSlots[i];
            if (slot == null)
                continue;

            int slotIndex = i;
            EventCallback<ClickEvent> cb = _ =>
            {
                onItemClicked?.Invoke(slotIndex);
            };

            _itemSlotCallbacks[i] = cb;
            slot.RegisterCallback(cb);
        }
    }

    public void UnbindItemSlots()
    {
        if (_itemSlots == null || _itemSlotCallbacks == null)
        {
            _itemSlots = null;
            _itemSlotCallbacks = null;
            return;
        }

        int count = Mathf.Min(_itemSlots.Length, _itemSlotCallbacks.Length);

        for (int i = 0; i < count; i++)
        {
            var slot = _itemSlots[i];
            var cb = _itemSlotCallbacks[i];

            if (slot != null && cb != null)
                slot.UnregisterCallback(cb);
        }

        _itemSlots = null;
        _itemSlotCallbacks = null;
    }

    public void BindRunButton(Button runButton, Action onRunClicked)
    {
        UnbindRunButton();

        if (runButton == null)
            return;

        _runButton = runButton;
        _runHandler = onRunClicked;

        if (_runHandler != null)
            _runButton.clicked += _runHandler;
    }

    public void UnbindRunButton()
    {
        if (_runButton != null && _runHandler != null)
            _runButton.clicked -= _runHandler;

        _runButton = null;
        _runHandler = null;
    }

    public void UnbindAll()
    {
        UnbindSpellSlots();
        UnbindItemSlots();
        UnbindRunButton();
    }
}
