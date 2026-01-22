using System;
using System.Collections.Generic;
using MyGame.Run;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ActiveSpellsSectionController
{
    private readonly Sprite _lockedIcon;
    private VisualElement _panelRoot;
    private readonly Dictionary<int, VisualElement> _slots = new();

    private int _unlockedSlots = 1;

    // ✅ drag start from active slot (slotIndex, evt)
    public Action<int, PointerDownEvent> RequestBeginDragFromSlot;

    public ActiveSpellsSectionController(string lockedIconResourcesPath)
    {
        _lockedIcon = !string.IsNullOrWhiteSpace(lockedIconResourcesPath)
            ? Resources.Load<Sprite>(lockedIconResourcesPath)
            : null;
    }

    public void Bind(VisualElement spellsPanel, int unlockedSlots)
    {
        _panelRoot = spellsPanel;
        _unlockedSlots = Mathf.Max(0, unlockedSlots);

        CacheSlots();
        Refresh();
    }

    public void Unbind()
    {
        _slots.Clear();
        _panelRoot = null;
        RequestBeginDragFromSlot = null;
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
        var db = GameConfigProvider.Instance.SpellDatabase;

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

            // ✅ IMPORTANT: remove placeholder background AND any previous Image child
            iconHost.Clear();
            iconHost.style.backgroundImage = StyleKeyword.None;

            if (!isUnlocked)
            {
                nameLabel.text = "Locked";

                if (_lockedIcon != null)
                    SetupIconAsImage(iconHost, _lockedIcon);

                continue;
            }

            var entry = book.GetSpellInSlot(slotIndex);
            if (entry == null)
            {
                nameLabel.text = "Empty";
                continue;
            }

            var def = db.GetById(entry.spellId);
            nameLabel.text = def != null ? def.displayName : entry.spellId;

            var sprite = def != null ? def.icon : null;
            if (sprite != null)
            {
                var img = SetupIconAsImage(iconHost, sprite);

                // ✅ only icon is draggable
                img.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    RequestBeginDragFromSlot?.Invoke(slotIndex, evt);
                    evt.StopPropagation();
                });
            }
        }
    }

    // Centers icon and avoids “bottom-right drift”
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
}
