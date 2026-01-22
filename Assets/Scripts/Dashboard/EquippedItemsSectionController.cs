using System;
using System.Collections.Generic;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using MyName.Equipment;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class EquippedItemsSectionController
{
    private const string DefaultUnknownName = "Unknown";

    private const string EquipSlotIconClass = "equip-slot-icon";

    // Preferred resource name for the "occupied" slot background.
    // If not found, we fall back to other existing UI sprites.
    private static readonly string[] EquippedSlotBackgroundResourceCandidates =
    {
        "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_inventory",
    };

    private sealed class SlotUi
    {
        public VisualElement slot;
        public VisualElement icon;
        public StyleBackground emptyBackground;
        public Sprite emptySprite;
    }

    private VisualElement _root;
    private VisualElement _equippedItemsContent;

    private Label _nameLabel; // EquippedItemsContent/NameHolder/Name
    private VisualElement _avatar; // EquippedItemsContent/CharacterAvatar

    private Sprite _equippedSlotBackground;
    private readonly Dictionary<EquipmentSlot, SlotUi> _slotUiBySlot = new();
    private readonly Dictionary<EquipmentSlot, Sprite> _defaultEmptySlotSprites = new();
    private SlotUi _ring2Ui;

    private static readonly Dictionary<EquipmentSlot, string> DefaultEmptySlotSpriteResourceBySlot =
        new()
        {
            { EquipmentSlot.Head, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Head" },
            { EquipmentSlot.Chest, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Chest" },
            { EquipmentSlot.Legs, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_pants" },
            { EquipmentSlot.Hands, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Bracelet" },
            { EquipmentSlot.Feet, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_boots" },
            { EquipmentSlot.MainHand, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Weapon" },
            { EquipmentSlot.Offhand, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Shield" },
            { EquipmentSlot.Ring, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_ring" },
            { EquipmentSlot.Amulet, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_neck" },
            { EquipmentSlot.Jewelry, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Jewelery" },
            { EquipmentSlot.Belt, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Belt" },
            { EquipmentSlot.Trinket, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Trinket" },
            { EquipmentSlot.Gloves, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Glowes" },
            { EquipmentSlot.Shoulders, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Shoulder" },
            { EquipmentSlot.Ranged, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_bow" },
            { EquipmentSlot.Cape, "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_Eq_Tabard" },
        };

    public void Bind(VisualElement inventoryTabRoot)
    {
        _root = inventoryTabRoot;

        if (_root == null)
        {
            Debug.LogError("EquippedItemsSectionController.Bind: inventoryTabRoot is null.");
            return;
        }

        _equippedItemsContent = _root.Q<VisualElement>("EquippedItemsContent");
        if (_equippedItemsContent == null)
        {
            Debug.LogError(
                "EquippedItemsSectionController: Could not find VisualElement named 'EquippedItemsContent'."
            );
            return;
        }

        _nameLabel = _equippedItemsContent.Q<Label>("Name");
        _avatar = _equippedItemsContent.Q<VisualElement>("CharacterAvatar");

        _equippedSlotBackground = LoadFirstSprite(EquippedSlotBackgroundResourceCandidates);
        BindSlotUis();

        RefreshFromSave();
    }

    public void Unbind()
    {
        _root = null;
        _equippedItemsContent = null;
        _nameLabel = null;
        _avatar = null;

        _equippedSlotBackground = null;
        _slotUiBySlot.Clear();
        _defaultEmptySlotSprites.Clear();
        _ring2Ui = null;
    }

    public void RefreshFromSave()
    {
        if (!SaveSession.HasSave)
            return;

        var save = SaveSession.Current;

        if (_nameLabel != null)
        {
            _nameLabel.text = string.IsNullOrWhiteSpace(save.characterName)
                ? DefaultUnknownName
                : save.characterName;
        }

        if (_avatar != null)
        {
            var sprite = GameConfigProvider.Instance?.PlayerAvatarDatabase?.GetSpriteOrNull(
                save.avatarId
            );
            _avatar.style.backgroundImage =
                sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
        }

        // Prefer runtime equipment if available (updates instantly when gear changes),
        // else fall back to reading equipped instances from the save.
        var runtimeEquipment = RunSession.Equipment;
        if (runtimeEquipment != null)
            RefreshEquipmentSlots(runtimeEquipment);
        else
            RefreshEquipmentSlots(save);
    }

    private void BindSlotUis()
    {
        _slotUiBySlot.Clear();
        _defaultEmptySlotSprites.Clear();

        // Mapping from UXML element names to EquipmentSlot.
        // Some names differ (e.g., "OffHand" vs enum "Offhand"), and the layout has a few
        // extra placeholders that don't exist as real slots yet (e.g., Ring2, Ammunition).
        var bindings = new (string elementName, EquipmentSlot slot)[]
        {
            ("Head", EquipmentSlot.Head),
            ("Chest", EquipmentSlot.Chest),
            ("Legs", EquipmentSlot.Legs),
            ("Bracer", EquipmentSlot.Hands),
            ("Foot", EquipmentSlot.Feet),
            ("MainHand", EquipmentSlot.MainHand),
            ("OffHand", EquipmentSlot.Offhand),
            ("Ring1", EquipmentSlot.Ring),
            ("Amulet", EquipmentSlot.Amulet),
            ("Jewelery", EquipmentSlot.Jewelry),
            ("Belt", EquipmentSlot.Belt),
            ("Trinket", EquipmentSlot.Trinket),
            ("Gloves", EquipmentSlot.Gloves),
            ("Shoulder", EquipmentSlot.Shoulders),
            ("Ranged", EquipmentSlot.Ranged),
            ("Cape", EquipmentSlot.Cape),
        };

        foreach (var b in bindings)
        {
            var slotEl = _equippedItemsContent.Q<VisualElement>(b.elementName);
            if (slotEl == null)
            {
                Debug.LogWarning(
                    $"EquippedItemsSectionController: Could not find slot element '{b.elementName}'."
                );
                continue;
            }

            var iconEl = slotEl.Q<VisualElement>("Icon");
            if (iconEl == null)
            {
                Debug.LogWarning(
                    $"EquippedItemsSectionController: Slot '{b.elementName}' is missing child 'Icon'."
                );
            }
            else
            {
                iconEl.AddToClassList(EquipSlotIconClass);
            }

            _slotUiBySlot[b.slot] = new SlotUi
            {
                slot = slotEl,
                icon = iconEl,
                emptyBackground = slotEl.style.backgroundImage,
                emptySprite = GetOrLoadDefaultEmptySprite(b.slot),
            };

            // Right click on an equipped-slot icon: unequip.
            var equipSlot = b.slot;
            slotEl.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1)
                    return;

                TryUnequip(equipSlot);
                evt.StopPropagation();
            });
        }

        // "Ring2" is a visual placeholder; we don't have a second ring slot in the save yet.
        var ring2El = _equippedItemsContent.Q<VisualElement>("Ring2");
        if (ring2El != null)
        {
            var iconEl = ring2El.Q<VisualElement>("Icon");
            if (iconEl != null)
                iconEl.AddToClassList(EquipSlotIconClass);

            _ring2Ui = new SlotUi
            {
                slot = ring2El,
                icon = iconEl,
                emptyBackground = ring2El.style.backgroundImage,
            };
        }
    }

    private static void TryUnequip(EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.None)
            return;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        // Equip(null) removes the equipped item from this slot.
        equipment.Equip(slot, instanceId: null);
    }

    private void RefreshEquipmentSlots(PlayerEquipment equipment)
    {
        if (equipment == null)
            return;

        foreach (var kvp in _slotUiBySlot)
        {
            var slot = kvp.Key;
            var ui = kvp.Value;
            if (ui == null || ui.slot == null)
                continue;

            if (!equipment.TryGetEquippedInstance(slot, out var inst) || inst == null)
            {
                SetSlotEmpty(ui);
                continue;
            }

            var iconSprite = GameConfigProvider.Instance?.EquipmentDatabase?.GetIcon(
                inst.equipmentId
            );
            SetSlotEquipped(ui, iconSprite);
        }

        // Always clear Ring2 for now (no save backing).
        if (_ring2Ui != null)
            SetSlotEmpty(_ring2Ui);
    }

    private void RefreshEquipmentSlots(SaveData save)
    {
        if (_slotUiBySlot.Count == 0)
            return;

        // Build quick lookups: slot -> instanceId, instanceId -> instance.
        var equippedInstanceIdBySlot = new Dictionary<EquipmentSlot, string>();
        if (save.equippedSlots != null)
        {
            foreach (var e in save.equippedSlots)
            {
                if (e == null || e.slot == EquipmentSlot.None)
                    continue;

                equippedInstanceIdBySlot[e.slot] = e.equipmentInstanceId;
            }
        }

        var instanceById = new Dictionary<string, SavedEquipmentInstance>(
            StringComparer.OrdinalIgnoreCase
        );
        if (save.equipmentInstances != null)
        {
            foreach (var inst in save.equipmentInstances)
            {
                if (inst == null || string.IsNullOrWhiteSpace(inst.instanceId))
                    continue;
                instanceById[inst.instanceId] = inst;
            }
        }

        foreach (var kvp in _slotUiBySlot)
        {
            var slot = kvp.Key;
            var ui = kvp.Value;
            if (ui == null || ui.slot == null)
                continue;

            equippedInstanceIdBySlot.TryGetValue(slot, out var instanceId);
            if (
                string.IsNullOrWhiteSpace(instanceId)
                || !instanceById.TryGetValue(instanceId, out var inst)
            )
            {
                SetSlotEmpty(ui);
                continue;
            }

            var equipmentId = inst != null ? inst.equipmentId : null;
            var iconSprite = GameConfigProvider.Instance?.EquipmentDatabase?.GetIcon(equipmentId);
            SetSlotEquipped(ui, iconSprite);
        }

        // Always clear Ring2 for now (no save backing).
        if (_ring2Ui != null)
            SetSlotEmpty(_ring2Ui);
    }

    private void SetSlotEmpty(SlotUi ui)
    {
        if (ui == null || ui.slot == null)
            return;

        if (ui.emptySprite != null)
            ui.slot.style.backgroundImage = new StyleBackground(ui.emptySprite);
        else
            ui.slot.style.backgroundImage = ui.emptyBackground;

        if (ui.icon != null)
            ui.icon.style.backgroundImage = StyleKeyword.None;
    }

    private void SetSlotEquipped(SlotUi ui, Sprite itemIcon)
    {
        if (ui == null || ui.slot == null)
            return;

        if (_equippedSlotBackground != null)
            ui.slot.style.backgroundImage = new StyleBackground(_equippedSlotBackground);

        if (ui.icon != null)
        {
            ui.icon.style.backgroundImage =
                itemIcon != null ? new StyleBackground(itemIcon) : StyleKeyword.None;
        }
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

    private Sprite GetOrLoadDefaultEmptySprite(EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.None)
            return null;

        if (_defaultEmptySlotSprites.TryGetValue(slot, out var existing))
            return existing;

        if (!DefaultEmptySlotSpriteResourceBySlot.TryGetValue(slot, out var resourcePath))
        {
            _defaultEmptySlotSprites[slot] = null;
            return null;
        }

        var sprite = Resources.Load<Sprite>(resourcePath);
        _defaultEmptySlotSprites[slot] = sprite;
        return sprite;
    }
}
