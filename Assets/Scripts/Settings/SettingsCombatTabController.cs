// File: Assets/Scripts/Settings/SettingsCombatTabController.cs
// Unity 6 - UI Toolkit
// Uses KeyBind overlay instead of GlobalModal.

using UnityEngine;
using UnityEngine.UIElements;

public sealed class SettingsCombatTabController : ISettingsTabController
{
    private const string KeyBindOverlayId = "key_bind"; // <-- MUST match OverlayEntry.overlayId

    private readonly ScreenSwapper _swapper;
    private VisualElement _panel;

    private bool _wiredClicks;

    public SettingsCombatTabController(ScreenSwapper swapper)
    {
        _swapper = swapper;
    }

    public void Bind(VisualElement panelRoot, object context)
    {
        _panel = panelRoot;

        if (_panel == null)
        {
            Debug.LogError("SettingsCombatTabController.Bind: panelRoot is null.");
            return;
        }

        if (_swapper == null)
        {
            Debug.LogError("SettingsCombatTabController.Bind: ScreenSwapper is null.");
            return;
        }

        // Wire clicks once (panel is persistent)
        if (!_wiredClicks)
        {
            RegisterValueClicks();
            _wiredClicks = true;
        }
    }

    public void OnShow()
    {
        if (_panel == null)
            return;

        KeybindPrefs.EnsureSpellDefaults();
        KeybindPrefs.EnsureItemDefaults();
        PopulateSpellBindings();
        PopulateItemBindings();
    }

    public void OnHide()
    {
        // no-op
    }

    public void Unbind()
    {
        _panel = null;
        _wiredClicks = false;
    }

    private void RegisterValueClicks()
    {
        // UXML path: CombatSettings / Binding / Spells / SpellBindingList
        var spellBindingList = _panel.Q<VisualElement>("SpellBindingList");
        if (spellBindingList == null)
        {
            Debug.LogError(
                "SettingsCombatTabController.RegisterValueClicks: SpellBindingList not found."
            );
            return;
        }

        for (int slotIndex = 0; slotIndex < KeybindPrefs.SpellSlotCount; slotIndex++)
        {
            var row = spellBindingList.Q<VisualElement>($"Slot{slotIndex + 1}");
            if (row == null)
                continue;

            var valueBox = row.Q<VisualElement>("Value");
            if (valueBox == null)
                continue;

            valueBox.pickingMode = PickingMode.Position;

            // Inner label shouldn't steal clicks
            var valueLabel = valueBox.Q<Label>();
            if (valueLabel != null)
                valueLabel.pickingMode = PickingMode.Ignore;

            int capturedSlotIndex = slotIndex;

            valueBox.RegisterCallback<PointerDownEvent>(_ =>
            {
                OpenKeyBindOverlay(capturedSlotIndex, valueBox);
            });
        }

        // UXML path: CombatSettings / Binding / Items / ItemBindingList
        var itemBindingList = _panel.Q<VisualElement>("ItemBindingList");
        if (itemBindingList == null)
        {
            Debug.LogError(
                "SettingsCombatTabController.RegisterValueClicks: ItemBindingList not found."
            );
            return;
        }

        for (int slotIndex = 0; slotIndex < KeybindPrefs.ItemSlotCount; slotIndex++)
        {
            var row = itemBindingList.Q<VisualElement>($"Slot{slotIndex + 1}");
            if (row == null)
                continue;

            var valueBox = row.Q<VisualElement>("Value");
            if (valueBox == null)
                continue;

            valueBox.pickingMode = PickingMode.Position;

            // Inner label shouldn't steal clicks
            var valueLabel = valueBox.Q<Label>();
            if (valueLabel != null)
                valueLabel.pickingMode = PickingMode.Ignore;

            int capturedSlotIndex = slotIndex;

            valueBox.RegisterCallback<PointerDownEvent>(_ =>
            {
                OpenItemKeyBindOverlay(capturedSlotIndex, valueBox);
            });
        }
    }

    private void OpenKeyBindOverlay(int slotIndex, VisualElement valueBox)
    {
        // Find current key for this slot (from PlayerPrefs)
        var currentKey = KeybindPrefs.GetSpellSlotKey(slotIndex);

        // We will update THIS label when binding is confirmed
        var valueLabel = valueBox.Q<Label>();

        int slotNumber = slotIndex + 1;

        _swapper.ShowOverlay(
            KeyBindOverlayId,
            new KeyBindOverlayContext
            {
                Title = "Bind",
                TargetName = $"Slot {slotNumber}",
                Message = $"Press any new keybind you want to bind for Slot {slotNumber}.",
                CurrentKey = currentKey,

                // When user confirms binding in the overlay:
                OnBound = newKey =>
                {
                    KeybindPrefs.ReplaceSpellSlotKey(slotIndex, newKey);

                    // Refresh both lists so any cleared duplicates update immediately.
                    PopulateSpellBindings();
                    PopulateItemBindings();
                },

                OnCancelled = () => {
                    // no-op
                },

                IgnoreMouseButtons = true,
            }
        );
    }

    private void OpenItemKeyBindOverlay(int slotIndex, VisualElement valueBox)
    {
        var currentKey = KeybindPrefs.GetItemSlotKey(slotIndex);

        var valueLabel = valueBox.Q<Label>();

        int slotNumber = slotIndex + 1;

        _swapper.ShowOverlay(
            KeyBindOverlayId,
            new KeyBindOverlayContext
            {
                Title = "Bind",
                TargetName = $"Item {slotNumber}",
                Message = $"Press any new keybind you want to bind for Item {slotNumber}.",
                CurrentKey = currentKey,

                OnBound = newKey =>
                {
                    KeybindPrefs.ReplaceItemSlotKey(slotIndex, newKey);

                    // Refresh both lists so any cleared duplicates update immediately.
                    PopulateSpellBindings();
                    PopulateItemBindings();
                },

                OnCancelled = () => {
                    // no-op
                },

                IgnoreMouseButtons = true,
            }
        );
    }

    private void PopulateSpellBindings()
    {
        var spellBindingList = _panel.Q<VisualElement>("SpellBindingList");
        if (spellBindingList == null)
        {
            Debug.LogError(
                "SettingsCombatTabController.PopulateSpellBindings: SpellBindingList not found."
            );
            return;
        }

        for (int slotIndex = 0; slotIndex < KeybindPrefs.SpellSlotCount; slotIndex++)
        {
            var row = spellBindingList.Q<VisualElement>($"Slot{slotIndex + 1}");
            if (row == null)
                continue;

            var valueBox = row.Q<VisualElement>("Value");
            var valueLabel = valueBox?.Q<Label>();
            if (valueLabel == null)
                continue;

            valueLabel.text = FormatKey(KeybindPrefs.GetSpellSlotKey(slotIndex));
        }
    }

    private void PopulateItemBindings()
    {
        var itemBindingList = _panel.Q<VisualElement>("ItemBindingList");
        if (itemBindingList == null)
        {
            Debug.LogError(
                "SettingsCombatTabController.PopulateItemBindings: ItemBindingList not found."
            );
            return;
        }

        for (int slotIndex = 0; slotIndex < KeybindPrefs.ItemSlotCount; slotIndex++)
        {
            var row = itemBindingList.Q<VisualElement>($"Slot{slotIndex + 1}");
            if (row == null)
                continue;

            var valueBox = row.Q<VisualElement>("Value");
            var valueLabel = valueBox?.Q<Label>();
            if (valueLabel == null)
                continue;

            valueLabel.text = FormatKey(KeybindPrefs.GetItemSlotKey(slotIndex));
        }
    }

    private static string FormatKey(KeyCode key)
    {
        if (key == KeyCode.None)
            return "-";

        string s = key.ToString();
        if (s.StartsWith("Alpha"))
            return s.Substring("Alpha".Length);
        if (s.StartsWith("Keypad"))
            return "Num" + s.Substring("Keypad".Length);
        return s;
    }
}
