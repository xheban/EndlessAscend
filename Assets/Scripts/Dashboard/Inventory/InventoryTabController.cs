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

public sealed partial class InventoryTabController : IDashboardTabController
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
        ActiveCombatSlots,
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
    private bool _equippedTooltipHandlersRegistered;

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;

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

        RegisterEquippedTooltipHandlersIfNeeded();
        RegisterDragDropHandlersIfNeeded();

        BindActiveCombatSlotsIfPresent();
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void OnShow()
    {
        _isVisible = true;

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

        RefreshActiveCombatSlotsIcons();
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
        UnregisterEquippedTooltipHandlers();
        UnregisterDragDropHandlers();

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

        _activeCombatItemsRoot = null;
        _activeCombatSlotRoots.Clear();
        _activeCombatSlotIcons.Clear();

        _root = null;
        _equippedTooltipHandlersRegistered = false;
    }
}
