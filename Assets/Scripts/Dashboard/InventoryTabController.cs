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

public sealed class InventoryTabController : IDashboardTabController
{
    private const int InventoryCapacity = 402;
    private const int SlotSizePx = 64;
    private const int SlotPaddingPx = 8;
    private const int SlotGapPx = 6;

    private const int TooltipOffsetPx = 18;
    private const int TooltipEdgePaddingPx = 8;
    private const int TooltipFallbackWidthPx = 396;
    private const int TooltipFallbackHeightPx = 220;

    private enum GridKind
    {
        Equipment,
        Items,
        Equipped,
    }

    private static readonly string[] SlotBackgroundResourceCandidates =
    {
        "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_inventory",
    };

    private readonly ScreenSwapper _swapper;

    public InventoryTabController(ScreenSwapper swapper = null)
    {
        _swapper = swapper;
    }

    private VisualElement _root;
    private bool _dirty = true;

    private bool _isVisible;
    private PlayerEquipment _subscribedEquipment;
    private Action _onEquipmentChanged;

    private EquippedItemsSectionController _equippedItems;

    private VisualElement _equipmentContent;
    private VisualElement _itemsContent;

    private TextField _equipmentSearchField;
    private TextField _itemsSearchField;
    private string _equipmentSearchQuery;
    private string _itemsSearchQuery;

    private EventCallback<ChangeEvent<string>> _onEquipmentSearchChanged;
    private EventCallback<ChangeEvent<string>> _onItemsSearchChanged;

    private Label _goldValueLabel;
    private bool _gridsBuilt;
    private Sprite _gridSlotBackground;

    private readonly System.Collections.Generic.List<VisualElement> _equipmentSlotIcons = new();
    private readonly System.Collections.Generic.List<VisualElement> _itemsSlotIcons = new();

    private readonly System.Collections.Generic.List<Label> _itemsSlotCountLabels = new();

    private readonly System.Collections.Generic.List<string> _equipmentSlotInstanceIds = new();

    private readonly System.Collections.Generic.List<string> _itemsSlotItemIds = new();

    private VisualElement _inventoryPanel;
    private VisualElement _detailTooltip;
    private VisualElement _detailTooltipIcon;
    private Label _detailTooltipName;
    private Label _detailTooltipRaritySlot;
    private Label _detailTooltipLevelTier;
    private Label _detailTooltipDetailText;
    private VisualElement _detailTooltipRolledStatsSection;
    private VisualElement _detailTooltipRolledStatsList;
    private VisualElement _detailTooltipRequirementsSection;
    private VisualElement _detailTooltipRequirementsList;

    private VisualElement _detailTooltipCompare;
    private VisualElement _detailTooltipCompareIcon;
    private Label _detailTooltipCompareName;
    private Label _detailTooltipCompareRaritySlot;
    private Label _detailTooltipCompareLevelTier;
    private Label _detailTooltipCompareDetailText;
    private VisualElement _detailTooltipCompareRolledStatsSection;
    private VisualElement _detailTooltipCompareRolledStatsList;
    private VisualElement _detailTooltipCompareRequirementsSection;
    private VisualElement _detailTooltipCompareRequirementsList;

    private readonly System.Collections.Generic.Dictionary<
        MyName.Equipment.EquipmentSlot,
        VisualElement
    > _equippedSlotElementsBySlot = new();

    private GridKind _hoveredGrid;
    private int _hoveredSlotIndex = -1;
    private VisualElement _hoveredSlotElement;
    private Vector2 _hoveredPointerWorld;

    // ALT/SHIFT hold behavior (replaces click-to-pin and alt-click toggle compare).
    private bool _altHeld;
    private bool _shiftHeld;

    // When holding a modifier, keep the last hovered tooltip(s) visible even after pointer leaves.
    private bool _lockedHoverValid;
    private GridKind _lockedGrid;
    private int _lockedSlotIndex = -1;
    private VisualElement _lockedSlotElement;
    private Vector2 _lockedPointerWorld;

    // Some platforms/UI Toolkit setups don't reliably emit KeyDown/KeyUp for modifier keys.
    // We poll Unity input while the tab is visible to keep ALT/SHIFT behavior stable.
    private IVisualElementScheduledItem _modifierPoller;

    private EventCallback<KeyDownEvent> _onKeyDown;
    private EventCallback<KeyUpEvent> _onKeyUp;
    private EventCallback<PointerDownEvent> _onRootPointerDownFocus;
    private bool _equippedTooltipHandlersRegistered;

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;
        if (_root != null)
            _root.focusable = true;

        _equippedItems = new EquippedItemsSectionController();
        _equippedItems.Bind(_root);

        _equipmentContent = _root.Q<VisualElement>("EquipmentContent");
        _itemsContent = _root.Q<VisualElement>("ItemsContent");

        _equipmentSearchField = _root.Q<TextField>("EquipmentSearch");
        _itemsSearchField = _root.Q<TextField>("ItemsSearch");

        _equipmentSearchQuery = NormalizeSearch(_equipmentSearchField?.value);
        _itemsSearchQuery = NormalizeSearch(_itemsSearchField?.value);

        _onEquipmentSearchChanged = evt =>
        {
            _equipmentSearchQuery = NormalizeSearch(evt.newValue);
            if (_isVisible)
                RefreshEquipmentGridIcons();
        };
        _onItemsSearchChanged = evt =>
        {
            _itemsSearchQuery = NormalizeSearch(evt.newValue);
            if (_isVisible)
                RefreshItemsGridIcons();
        };

        _equipmentSearchField?.RegisterValueChangedCallback(_onEquipmentSearchChanged);
        _itemsSearchField?.RegisterValueChangedCallback(_onItemsSearchChanged);

        _goldValueLabel = _root
            ?.Q<VisualElement>("Currencies")
            ?.Q<VisualElement>("CurrencyList")
            ?.Q<VisualElement>("Gold")
            ?.Q<Label>("Value");

        _gridSlotBackground = LoadFirstSprite(SlotBackgroundResourceCandidates);
        BuildGridsIfNeeded();

        _onEquipmentChanged = () =>
        {
            if (_isVisible)
            {
                _equippedItems?.RefreshFromSave();
                RefreshEquipmentGridIcons();
            }
        };

        _onRootPointerDownFocus = evt =>
        {
            // Ensure we receive KeyDown/KeyUp events for ALT/SHIFT.
            _root?.Focus();
        };
        _root?.RegisterCallback(_onRootPointerDownFocus);

        _onKeyDown = evt =>
        {
            if (evt == null)
                return;

            bool changed = false;
            if (evt.keyCode == KeyCode.LeftAlt || evt.keyCode == KeyCode.RightAlt)
            {
                if (!_altHeld)
                {
                    _altHeld = true;
                    changed = true;
                }
            }
            else if (evt.keyCode == KeyCode.LeftShift || evt.keyCode == KeyCode.RightShift)
            {
                if (!_shiftHeld)
                {
                    _shiftHeld = true;
                    changed = true;
                }
            }

            if (changed)
            {
                // Capture the current hover as a locked tooltip anchor so we can
                // keep the tooltip(s) visible even after the pointer leaves.
                // This also prevents ALT-held tooltips from following the pointer.
                LockHoverFromCurrent();
                RefreshTooltipsForCurrentState();
            }
        };

        _onKeyUp = evt =>
        {
            if (evt == null)
                return;

            bool changed = false;
            if (evt.keyCode == KeyCode.LeftAlt || evt.keyCode == KeyCode.RightAlt)
            {
                if (_altHeld)
                {
                    _altHeld = false;
                    changed = true;
                }
            }
            else if (evt.keyCode == KeyCode.LeftShift || evt.keyCode == KeyCode.RightShift)
            {
                if (_shiftHeld)
                {
                    _shiftHeld = false;
                    changed = true;
                }
            }

            if (changed)
                RefreshTooltipsForCurrentState();
        };

        _root?.RegisterCallback(_onKeyDown);
        _root?.RegisterCallback(_onKeyUp);

        RegisterEquippedTooltipHandlersIfNeeded();
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void OnShow()
    {
        _isVisible = true;

        _root?.Focus();

        StartModifierPolling();

        SubscribeToEquipmentChanges();

        RefreshGoldValue();

        _inventoryPanel = _root?.Q<VisualElement>("InventoryPanel");
        if (_detailTooltip == null)
            _detailTooltip = _root?.Q<VisualElement>("InventoryDetailTooltip");
        if (_detailTooltip == null && _swapper != null)
            _detailTooltip = _swapper.GetCustomTooltipElement("InventoryDetailTooltip");

        if (_detailTooltipCompare == null)
            _detailTooltipCompare = _root?.Q<VisualElement>("InventoryDetailTooltipCompare");
        if (_detailTooltipCompare == null && _swapper != null)
            _detailTooltipCompare = _swapper.GetCustomTooltipElement(
                "InventoryDetailTooltipCompare"
            );
        _detailTooltipIcon = _detailTooltip?.Q<VisualElement>("Icon");
        _detailTooltipName = _detailTooltip?.Q<Label>("Name");
        _detailTooltipRaritySlot = _detailTooltip?.Q<Label>("RaritySlot");
        _detailTooltipLevelTier = _detailTooltip?.Q<Label>("LevelTier");
        _detailTooltipDetailText = _detailTooltip?.Q<Label>("DetailText");
        _detailTooltipRolledStatsSection = _detailTooltip?.Q<VisualElement>("RolledStats");
        _detailTooltipRolledStatsList = _detailTooltip?.Q<VisualElement>("RolledStatsList");
        _detailTooltipRequirementsSection = _detailTooltip?.Q<VisualElement>("Requirements");
        _detailTooltipRequirementsList = _detailTooltip?.Q<VisualElement>("RequirmentsList");

        _detailTooltipCompareIcon = _detailTooltipCompare?.Q<VisualElement>("Icon");
        _detailTooltipCompareName = _detailTooltipCompare?.Q<Label>("Name");
        _detailTooltipCompareRaritySlot = _detailTooltipCompare?.Q<Label>("RaritySlot");
        _detailTooltipCompareLevelTier = _detailTooltipCompare?.Q<Label>("LevelTier");
        _detailTooltipCompareDetailText = _detailTooltipCompare?.Q<Label>("DetailText");
        _detailTooltipCompareRolledStatsSection = _detailTooltipCompare?.Q<VisualElement>(
            "RolledStats"
        );
        _detailTooltipCompareRolledStatsList = _detailTooltipCompare?.Q<VisualElement>(
            "RolledStatsList"
        );
        _detailTooltipCompareRequirementsSection = _detailTooltipCompare?.Q<VisualElement>(
            "Requirements"
        );
        _detailTooltipCompareRequirementsList = _detailTooltipCompare?.Q<VisualElement>(
            "RequirmentsList"
        );
        _altHeld = false;
        _shiftHeld = false;
        _lockedHoverValid = false;
        _lockedSlotIndex = -1;
        _hoveredSlotIndex = -1;
        HideAllTooltips();

        if (!_dirty)
            return;

        _dirty = false;

        // Starter: apply name + avatar to equipped-items preview.
        _equippedItems?.RefreshFromSave();

        // Populate grids from runtime state.
        RefreshEquipmentGridIcons();
        RefreshItemsGridIcons();
    }

    public void OnHide()
    {
        _isVisible = false;
        UnsubscribeFromEquipmentChanges();

        StopModifierPolling();

        _altHeld = false;
        _shiftHeld = false;
        _lockedHoverValid = false;
        _lockedSlotIndex = -1;
        _hoveredSlotIndex = -1;
        HideAllTooltips();
    }

    public void Unbind()
    {
        _isVisible = false;
        UnsubscribeFromEquipmentChanges();

        StopModifierPolling();

        if (_root != null && _onRootPointerDownFocus != null)
            _root.UnregisterCallback(_onRootPointerDownFocus);
        _onRootPointerDownFocus = null;

        if (_root != null && _onKeyDown != null)
            _root.UnregisterCallback(_onKeyDown);
        if (_root != null && _onKeyUp != null)
            _root.UnregisterCallback(_onKeyUp);
        _onKeyDown = null;
        _onKeyUp = null;

        if (_equipmentSearchField != null && _onEquipmentSearchChanged != null)
            _equipmentSearchField.UnregisterValueChangedCallback(_onEquipmentSearchChanged);
        if (_itemsSearchField != null && _onItemsSearchChanged != null)
            _itemsSearchField.UnregisterValueChangedCallback(_onItemsSearchChanged);

        _equipmentSearchField = null;
        _itemsSearchField = null;
        _equipmentSearchQuery = null;
        _itemsSearchQuery = null;
        _onEquipmentSearchChanged = null;
        _onItemsSearchChanged = null;

        _equippedItems?.Unbind();

        _equippedItems = null;

        _equipmentContent = null;
        _itemsContent = null;
        _goldValueLabel = null;
        _gridsBuilt = false;
        _gridSlotBackground = null;

        _equipmentSlotIcons.Clear();
        _itemsSlotIcons.Clear();
        _itemsSlotItemIds.Clear();
        _itemsSlotCountLabels.Clear();

        HideAllTooltips();
        _inventoryPanel = null;
        _detailTooltip = null;
        _detailTooltipIcon = null;
        _detailTooltipName = null;
        _detailTooltipRaritySlot = null;
        _detailTooltipLevelTier = null;
        _detailTooltipDetailText = null;
        _detailTooltipRolledStatsSection = null;
        _detailTooltipRolledStatsList = null;
        _detailTooltipRequirementsSection = null;
        _detailTooltipRequirementsList = null;

        _detailTooltipCompare = null;
        _detailTooltipCompareIcon = null;
        _detailTooltipCompareName = null;
        _detailTooltipCompareRaritySlot = null;
        _detailTooltipCompareLevelTier = null;
        _detailTooltipCompareDetailText = null;
        _detailTooltipCompareRolledStatsSection = null;
        _detailTooltipCompareRolledStatsList = null;
        _detailTooltipCompareRequirementsSection = null;
        _detailTooltipCompareRequirementsList = null;

        _equippedSlotElementsBySlot.Clear();
        _altHeld = false;
        _shiftHeld = false;
        _lockedHoverValid = false;
        _lockedSlotIndex = -1;
        _hoveredSlotIndex = -1;
        _hoveredSlotElement = null;

        _root = null;
        _equippedTooltipHandlersRegistered = false;
    }

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

        foreach (var b in bindings)
        {
            var slotEl = equippedContent.Q<VisualElement>(b.elementName);
            if (slotEl == null)
                continue;

            var slotValue = b.slot;
            int slotIndex = (int)slotValue;

            _equippedSlotElementsBySlot[slotValue] = slotEl;

            slotEl.RegisterCallback<PointerEnterEvent>(evt =>
            {
                OnHoverEnter(GridKind.Equipped, slotIndex, slotEl, evt.position);
            });

            slotEl.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                OnHoverLeave(GridKind.Equipped, slotIndex);
            });

            slotEl.RegisterCallback<PointerMoveEvent>(evt =>
            {
                OnHoverMove(GridKind.Equipped, slotIndex, slotEl, evt.position);
            });

            slotEl.RegisterCallback<PointerDownEvent>(evt =>
            {
                // Left click is reserved for future dragging behavior; do not pin tooltips.
                // (Right-click equip is handled on inventory grid slots only.)
            });
        }

        _equippedTooltipHandlersRegistered = true;
    }

    private void OnHoverEnter(
        GridKind gridKind,
        int slotIndex,
        VisualElement slotElement,
        Vector2 pointerWorld
    )
    {
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

        if (!TryGetTooltipData(gridKind, slotIndex, out var data))
            return false;

        ApplyRarityNameStyle(_detailTooltipName, data.rarity);

        if (_detailTooltipName != null)
            _detailTooltipName.text = data.displayName;
        if (_detailTooltipIcon != null)
            _detailTooltipIcon.style.backgroundImage =
                data.icon != null ? new StyleBackground(data.icon) : StyleKeyword.None;

        if (_detailTooltipRaritySlot != null)
        {
            _detailTooltipRaritySlot.text = data.raritySlotLine;
            _detailTooltipRaritySlot.style.display = string.IsNullOrWhiteSpace(
                _detailTooltipRaritySlot.text
            )
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (_detailTooltipLevelTier != null)
        {
            _detailTooltipLevelTier.text = data.levelTierLine;
            _detailTooltipLevelTier.style.display = string.IsNullOrWhiteSpace(
                _detailTooltipLevelTier.text
            )
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (_detailTooltipDetailText != null)
        {
            _detailTooltipDetailText.text = data.description;
            _detailTooltipDetailText.style.display = string.IsNullOrWhiteSpace(
                _detailTooltipDetailText.text
            )
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (_detailTooltipRolledStatsSection != null)
            _detailTooltipRolledStatsSection.style.display =
                data.rolledStatLines != null && data.rolledStatLines.Count > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

        if (_detailTooltipRolledStatsList != null)
        {
            _detailTooltipRolledStatsList.Clear();
            if (data.rolledStatLines != null)
            {
                for (int i = 0; i < data.rolledStatLines.Count; i++)
                {
                    var t = data.rolledStatLines[i];
                    if (string.IsNullOrWhiteSpace(t))
                        continue;

                    var row = new Label($"\u2022 {t}");
                    row.AddToClassList("label-sm");
                    _detailTooltipRolledStatsList.Add(row);
                }
            }
        }

        if (_detailTooltipRequirementsSection != null)
            _detailTooltipRequirementsSection.style.display =
                data.requirements != null && data.requirements.Count > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

        if (_detailTooltipRequirementsList != null)
        {
            _detailTooltipRequirementsList.Clear();

            if (data.requirements != null)
            {
                for (int i = 0; i < data.requirements.Count; i++)
                {
                    var r = data.requirements[i];
                    if (r == null || string.IsNullOrWhiteSpace(r.name))
                        continue;

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;

                    var nameLabel = new Label($"\u2022 {r.name}");
                    nameLabel.AddToClassList("label-sm");

                    var valueLabel = new Label(r.required.ToString());
                    valueLabel.AddToClassList("label-sm");
                    valueLabel.style.marginLeft = 8;

                    if (r.current < r.required)
                        valueLabel.AddToClassList("text-danger");
                    else
                        valueLabel.AddToClassList("text-success");

                    row.Add(nameLabel);
                    row.Add(valueLabel);
                    _detailTooltipRequirementsList.Add(row);
                }
            }
        }

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

        if (!TryBuildEquipmentTooltipData(invInst, out var invData) || invData == null)
            return false;
        if (!TryBuildEquipmentTooltipData(equippedInst, out var eqData) || eqData == null)
            return false;

        ApplyTooltipDataToTooltipElements(
            _detailTooltipIcon,
            _detailTooltipName,
            _detailTooltipRaritySlot,
            _detailTooltipLevelTier,
            _detailTooltipDetailText,
            _detailTooltipRolledStatsSection,
            _detailTooltipRolledStatsList,
            _detailTooltipRequirementsSection,
            _detailTooltipRequirementsList,
            invData
        );

        ApplyTooltipDataToTooltipElements(
            _detailTooltipCompareIcon,
            _detailTooltipCompareName,
            _detailTooltipCompareRaritySlot,
            _detailTooltipCompareLevelTier,
            _detailTooltipCompareDetailText,
            _detailTooltipCompareRolledStatsSection,
            _detailTooltipCompareRolledStatsList,
            _detailTooltipCompareRequirementsSection,
            _detailTooltipCompareRequirementsList,
            eqData
        );

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

    private sealed class TooltipData
    {
        public string displayName;
        public Sprite icon;
        public Rarity rarity;
        public string raritySlotLine;
        public string levelTierLine;
        public string description;
        public System.Collections.Generic.List<string> rolledStatLines;
        public System.Collections.Generic.List<RequirementEntry> requirements;
    }

    private sealed class RequirementEntry
    {
        public string name;
        public int required;
        public int current;
    }

    private bool TryGetTooltipData(GridKind gridKind, int slotIndex, out TooltipData data)
    {
        data = null;
        if (slotIndex < 0)
            return false;

        if (gridKind == GridKind.Items)
            return TryGetItemTooltipData(slotIndex, out data);

        if (gridKind == GridKind.Equipment)
            return TryGetEquipmentTooltipDataFromInventory(slotIndex, out data);

        if (gridKind == GridKind.Equipped)
            return TryGetEquipmentTooltipDataFromEquipped(slotIndex, out data);

        return false;
    }

    private bool TryGetItemTooltipData(int slotIndex, out TooltipData data)
    {
        data = null;
        if (slotIndex >= _itemsSlotItemIds.Count)
            return false;

        var itemId = _itemsSlotItemIds[slotIndex];
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        var db = GameConfigProvider.Instance?.ItemDatabase;
        var def = db != null ? db.GetById(itemId) : null;

        var rarity = def != null ? def.rarity : Rarity.Common;
        var name =
            def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId;
        var icon = def != null ? def.icon : null;
        var desc = def != null ? def.description : null;

        data = new TooltipData
        {
            displayName = name,
            icon = icon,
            rarity = rarity,
            raritySlotLine = NiceEnum(rarity.ToString()),
            levelTierLine = null, // items: not shown for now
            description = desc,
            rolledStatLines = null,
        };
        return true;
    }

    private bool TryGetEquipmentTooltipDataFromInventory(int slotIndex, out TooltipData data)
    {
        data = null;

        if (slotIndex >= _equipmentSlotInstanceIds.Count)
            return false;

        var instanceId = _equipmentSlotInstanceIds[slotIndex];
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return false;

        var inst = equipment.GetInstance(instanceId);
        if (inst == null || string.IsNullOrWhiteSpace(inst.equipmentId))
            return false;

        return TryBuildEquipmentTooltipData(inst, out data);
    }

    private bool TryGetEquipmentTooltipDataFromEquipped(int slotIndex, out TooltipData data)
    {
        data = null;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return false;

        var slot = (MyName.Equipment.EquipmentSlot)slotIndex;
        if (slot == MyName.Equipment.EquipmentSlot.None)
            return false;

        if (!equipment.TryGetEquippedInstance(slot, out var inst) || inst == null)
            return false;

        if (string.IsNullOrWhiteSpace(inst.equipmentId))
            return false;

        return TryBuildEquipmentTooltipData(inst, out data);
    }

    private bool TryBuildEquipmentTooltipData(
        PlayerEquipment.EquipmentInstance inst,
        out TooltipData data
    )
    {
        data = null;
        var db = GameConfigProvider.Instance?.EquipmentDatabase;
        var def = db != null ? db.GetById(inst.equipmentId) : null;

        var rarity = def != null ? def.rarity : Rarity.Common;
        var name =
            def != null && !string.IsNullOrWhiteSpace(def.displayName)
                ? def.displayName
                : inst.equipmentId;
        var icon = def != null ? def.icon : null;
        if (icon == null && db != null)
            icon = db.GetIcon(inst.equipmentId);
        var desc = def != null ? def.description : null;

        var slotText = def != null ? NiceEnum(def.slot.ToString()) : string.Empty;
        var rarityText = NiceEnum(rarity.ToString());

        var levelTierLine = string.Empty;
        if (def != null)
            levelTierLine =
                $"Level {Mathf.Max(1, def.level)}   {HelperFunctions.ToTierRoman(def.tier)}";

        var rolledLines = BuildRolledStatLines(inst);

        var reqs = BuildEquipmentRequirements(def);

        data = new TooltipData
        {
            displayName = name,
            icon = icon,
            rarity = rarity,
            raritySlotLine = string.IsNullOrWhiteSpace(slotText)
                ? rarityText
                : $"{rarityText} {slotText}",
            levelTierLine = levelTierLine,
            description = desc,
            rolledStatLines = rolledLines,
            requirements = reqs,
        };
        return true;
    }

    private static System.Collections.Generic.List<RequirementEntry> BuildEquipmentRequirements(
        EquipmentDefinitionSO def
    )
    {
        if (def == null)
            return null;

        var save = SaveSession.Current;
        int playerLevel = save != null ? Mathf.Max(1, save.level) : 1;
        var stats = save != null ? save.finalStats : default;

        var reqs = new System.Collections.Generic.List<RequirementEntry>(8);

        // Required player level to equip (always show, red/green depending on met).
        reqs.Add(
            new RequirementEntry
            {
                name = "Level",
                required = Mathf.Max(1, def.requiredLevel),
                current = playerLevel,
            }
        );

        // Required flat base stats.
        if (def.flatStatRequirements != null)
        {
            for (int i = 0; i < def.flatStatRequirements.Count; i++)
            {
                var r = def.flatStatRequirements[i];
                if (r.minValue <= 0)
                    continue;

                int current = GetBaseStatValue(stats, r.stat);
                reqs.Add(
                    new RequirementEntry
                    {
                        name = NiceEnum(r.stat.ToString()),
                        required = Mathf.Max(0, r.minValue),
                        current = current,
                    }
                );
            }
        }

        return reqs.Count > 0 ? reqs : null;
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

    private static System.Collections.Generic.List<string> BuildRolledStatLines(
        PlayerEquipment.EquipmentInstance inst
    )
    {
        var lines = new System.Collections.Generic.List<string>(32);

        if (inst?.rolledBaseStatMods != null)
        {
            for (int i = 0; i < inst.rolledBaseStatMods.Count; i++)
            {
                var m = inst.rolledBaseStatMods[i];
                lines.Add(
                    $"{HumanizeStatName(NiceEnum(m.stat.ToString()))} {FormatModValue(m.op, m.value)}"
                );
            }
        }

        if (inst?.rolledDerivedStatMods != null)
        {
            for (int i = 0; i < inst.rolledDerivedStatMods.Count; i++)
            {
                var m = inst.rolledDerivedStatMods[i];
                lines.Add(
                    $"{HumanizeStatName(NiceEnum(m.stat.ToString()))} {FormatModValue(m.op, m.value)}"
                );
            }
        }

        if (inst?.rolledSpellMods != null)
        {
            for (int i = 0; i < inst.rolledSpellMods.Count; i++)
            {
                var m = inst.rolledSpellMods[i];
                var line = FormatSpellCombatModifierLine(m);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }

        if (inst?.rolledSpellOverrides != null)
        {
            for (int i = 0; i < inst.rolledSpellOverrides.Count; i++)
            {
                var o = inst.rolledSpellOverrides[i];
                var line = FormatSpellVariableOverrideLine(o);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }

        return lines;
    }

    private static string HumanizeStatName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        // Consistent spelling/abbreviations used elsewhere in the UI.
        var s = raw;
        s = s.Replace("Max Hp", "Max HP");
        s = s.Replace("Defense", "Defence");
        s = s.Replace("Casting Speed", "Cast Speed");
        return s;
    }

    private static string FormatSpellCombatModifierLine(SpellCombatModifier m)
    {
        var targetName = m.target.ToString();
        if (string.IsNullOrWhiteSpace(targetName) || targetName == "None")
            return null;

        bool isLessPercent = targetName.EndsWith("LessPercent", StringComparison.Ordinal);

        // Strip suffixes like Flat / MorePercent / LessPercent for cleaner labels.
        string baseName = targetName;
        baseName = baseName.Replace("MorePercent", string.Empty);
        baseName = baseName.Replace("LessPercent", string.Empty);
        baseName = baseName.Replace("Flat", string.Empty);

        // Friendlier actor wording.
        baseName = baseName.Replace("Attacker", "Attack");
        baseName = baseName.Replace("Defender", "Enemy");

        string label = HumanizeStatName(NiceEnum(baseName));

        // Include type/range context if present.
        if (targetName.EndsWith("ByType", StringComparison.Ordinal) && m.damageType != default)
            label += $" ({NiceEnum(m.damageType.ToString())})";
        else if (
            targetName.EndsWith("ByRange", StringComparison.Ordinal)
            && m.damageRangeType != default
        )
            label += $" ({NiceEnum(m.damageRangeType.ToString())})";
        else if (m.damageKind != default)
            label += $" ({NiceEnum(m.damageKind.ToString())})";

        // Display exactly what was rolled/stored. We don't roll negative values.

        // Safety: if a *LessPercent target ever slips in, don't display it like a positive bonus.
        if (isLessPercent && m.op == ModOp.Percent)
        {
            float abs = Mathf.Abs(m.value);
            int v = Mathf.RoundToInt(abs);
            string pct = Mathf.Abs(abs - v) < 0.001f ? (v + "%") : abs.ToString("0.##") + "%";
            return $"{label} {pct} less";
        }

        return $"{label} {FormatModValue(m.op, m.value)}";
    }

    private static string FormatSpellVariableOverrideLine(SpellVariableOverride o)
    {
        switch (o.type)
        {
            case SpellVariableOverrideType.DamageKind:
                return $"Damage Kind: {NiceEnum(o.damageKind.ToString())}";
            case SpellVariableOverrideType.DamageRangeType:
                return $"Range: {NiceEnum(o.damageRangeType.ToString())}";
            case SpellVariableOverrideType.DamageType:
                return $"Damage Type: {NiceEnum(o.damageType.ToString())}";
            case SpellVariableOverrideType.IgnoreDefenseFlat:
                return $"Ignore Defence {FormatModValue(ModOp.Flat, o.ignoreDefenseFlat)}";
            case SpellVariableOverrideType.IgnoreDefensePercent:
                return $"Ignore Defence {FormatModValue(ModOp.Percent, o.ignoreDefensePercent)}";
            default:
                return NiceEnum(o.type.ToString());
        }
    }

    private static void ApplyRarityNameStyle(Label nameLabel, Rarity rarity)
    {
        if (nameLabel == null)
            return;

        nameLabel.RemoveFromClassList("rarity-common");
        nameLabel.RemoveFromClassList("rarity-uncommon");
        nameLabel.RemoveFromClassList("rarity-rare");
        nameLabel.RemoveFromClassList("rarity-epic");
        nameLabel.RemoveFromClassList("rarity-legendary");
        nameLabel.RemoveFromClassList("rarity-mythical");
        nameLabel.RemoveFromClassList("rarity-forbidden");

        switch (rarity)
        {
            case Rarity.Uncommon:
                nameLabel.AddToClassList("rarity-uncommon");
                break;
            case Rarity.Rare:
                nameLabel.AddToClassList("rarity-rare");
                break;
            case Rarity.Epic:
                nameLabel.AddToClassList("rarity-epic");
                break;
            case Rarity.Legendary:
                nameLabel.AddToClassList("rarity-legendary");
                break;
            case Rarity.Mythical:
                nameLabel.AddToClassList("rarity-mythical");
                break;
            case Rarity.Forbidden:
                nameLabel.AddToClassList("rarity-forbidden");
                break;
            default:
                nameLabel.AddToClassList("rarity-common");
                break;
        }
    }

    private static string FormatModValue(ModOp op, float value)
    {
        switch (op)
        {
            case ModOp.Flat:
            {
                var v = Mathf.RoundToInt(value);
                if (Mathf.Abs(value - v) < 0.001f)
                    return (v > 0 ? "+" : "") + v;
                return (value > 0 ? "+" : "") + value.ToString("0.##");
            }
            case ModOp.Percent:
            {
                var v = Mathf.RoundToInt(value);
                if (Mathf.Abs(value - v) < 0.001f)
                    return (v > 0 ? "+" : "") + v + "%";
                return (value > 0 ? "+" : "") + value.ToString("0.##") + "%";
            }
            default:
                return value.ToString("0.##");
        }
    }

    private static string NiceEnum(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var sb = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(raw[i - 1]) || char.IsDigit(raw[i - 1])))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

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

    private void ApplyTooltipDataToTooltipElements(
        VisualElement icon,
        Label name,
        Label raritySlot,
        Label levelTier,
        Label detailText,
        VisualElement rolledStatsSection,
        VisualElement rolledStatsList,
        VisualElement requirementsSection,
        VisualElement requirementsList,
        TooltipData data
    )
    {
        if (data == null)
            return;

        ApplyRarityNameStyle(name, data.rarity);

        if (name != null)
            name.text = data.displayName;
        if (icon != null)
            icon.style.backgroundImage =
                data.icon != null ? new StyleBackground(data.icon) : StyleKeyword.None;

        if (raritySlot != null)
        {
            raritySlot.text = data.raritySlotLine;
            raritySlot.style.display = string.IsNullOrWhiteSpace(raritySlot.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (levelTier != null)
        {
            levelTier.text = data.levelTierLine;
            levelTier.style.display = string.IsNullOrWhiteSpace(levelTier.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (detailText != null)
        {
            detailText.text = data.description;
            detailText.style.display = string.IsNullOrWhiteSpace(detailText.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        if (rolledStatsSection != null)
            rolledStatsSection.style.display =
                data.rolledStatLines != null && data.rolledStatLines.Count > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

        if (rolledStatsList != null)
        {
            rolledStatsList.Clear();
            if (data.rolledStatLines != null)
            {
                for (int i = 0; i < data.rolledStatLines.Count; i++)
                {
                    var t = data.rolledStatLines[i];
                    if (string.IsNullOrWhiteSpace(t))
                        continue;

                    var row = new Label($"\u2022 {t}");
                    row.AddToClassList("label-sm");
                    rolledStatsList.Add(row);
                }
            }
        }

        if (requirementsSection != null)
            requirementsSection.style.display =
                data.requirements != null && data.requirements.Count > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

        if (requirementsList != null)
        {
            requirementsList.Clear();

            if (data.requirements != null)
            {
                for (int i = 0; i < data.requirements.Count; i++)
                {
                    var r = data.requirements[i];
                    if (r == null || string.IsNullOrWhiteSpace(r.name))
                        continue;

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;

                    var nameLabel = new Label($"\u2022 {r.name}");
                    nameLabel.AddToClassList("label-sm");

                    var valueLabel = new Label(r.required.ToString());
                    valueLabel.AddToClassList("label-sm");
                    valueLabel.style.marginLeft = 8;

                    if (r.current < r.required)
                        valueLabel.AddToClassList("text-danger");
                    else
                        valueLabel.AddToClassList("text-success");

                    row.Add(nameLabel);
                    row.Add(valueLabel);
                    requirementsList.Add(row);
                }
            }
        }
    }

    private void RefreshEquipmentGridIcons()
    {
        if (_equipmentSlotIcons.Count == 0)
            return;

        // Clear all icons first.
        for (int i = 0; i < _equipmentSlotIcons.Count; i++)
            _equipmentSlotIcons[i].style.backgroundImage = StyleKeyword.None;

        var equipment = RunSession.Equipment;
        if (equipment == null)
            return;

        // Clear mapping.
        for (int i = 0; i < _equipmentSlotInstanceIds.Count; i++)
            _equipmentSlotInstanceIds[i] = null;

        // Show non-equipped equipment instances in the equipment inventory.
        var equippedIds = new System.Collections.Generic.HashSet<string>(
            equipment.Equipped.Values,
            StringComparer.OrdinalIgnoreCase
        );

        var equipmentDb = GameConfigProvider.Instance?.EquipmentDatabase;
        var query = _equipmentSearchQuery;

        int slotIndex = 0;
        foreach (var kvp in equipment.Instances)
        {
            if (slotIndex >= _equipmentSlotIcons.Count)
                break;

            var inst = kvp.Value;
            if (inst == null || string.IsNullOrWhiteSpace(inst.instanceId))
                continue;

            if (equippedIds.Contains(inst.instanceId))
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

            var icon = GameConfigProvider.Instance?.EquipmentDatabase?.GetIcon(inst.equipmentId);
            if (icon == null)
                continue;

            _equipmentSlotIcons[slotIndex].style.backgroundImage = new StyleBackground(icon);
            if (slotIndex < _equipmentSlotInstanceIds.Count)
                _equipmentSlotInstanceIds[slotIndex] = inst.instanceId;
            slotIndex++;
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

    private void SubscribeToEquipmentChanges()
    {
        var eq = RunSession.Equipment;
        if (eq == null)
            return;

        if (ReferenceEquals(eq, _subscribedEquipment))
            return;

        UnsubscribeFromEquipmentChanges();

        _subscribedEquipment = eq;
        if (_onEquipmentChanged != null)
            _subscribedEquipment.Changed += _onEquipmentChanged;
    }

    private void UnsubscribeFromEquipmentChanges()
    {
        if (_subscribedEquipment == null)
            return;

        if (_onEquipmentChanged != null)
            _subscribedEquipment.Changed -= _onEquipmentChanged;

        _subscribedEquipment = null;
    }
}
