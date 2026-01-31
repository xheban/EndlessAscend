using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Helpers;
using MyGame.Run;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SpellMergeRoomHomesteadTabController : SimpleHomesteadTabControllerBase
{
    private enum NodeSlot
    {
        Top,
        Right,
        Bottom,
        Left,
    }

    private enum DragKind
    {
        None,
        Core,
        Node,
        Spell,
    }

    private const string PartsHolderName = "PartsHolder";
    private const string PartsLabelName = "PartsLabel";
    private const string CoresButtonName = "Cores";
    private const string NodesButtonName = "Nodes";
    private const string SpellsButtonName = "Spells";
    private const string CoreName = "Core";
    private const string NodeTopName = "NodeTop";
    private const string NodeRightName = "NodeRight";
    private const string NodeBottomName = "NodeBottom";
    private const string NodeLeftName = "NodeLeft";
    private const string AddButtonCoreName = "AddButtonCore";
    private const string AddButtonTopName = "AddButtonTop";
    private const string AddButtonRightName = "AddButtonRight";
    private const string AddButtonBottomName = "AddButtonBottom";
    private const string AddButtonLeftName = "AddButtonLeft";
    private const string BottomPanelName = "BottomPanel";
    private const string MergeButtonName = "Merge";
    private const string ResetButtonName = "Reset";
    private const string CostValueName = "CostValue";
    private const string ChanceValueName = "ChanceValue";

    private const int CoreSlotSizePx = 64;
    private const int CoreIconSizePx = 64;
    private const int NodeSlotWidthPx = 96;
    private const int NodeSlotHeightPx = 64;
    private const int NodeIconWidthPx = 72;
    private const int NodeIconHeightPx = 48;
    private const int SlotPaddingPx = 8;
    private const int SlotGapPx = 6;

    private static readonly string[] SlotBackgroundResourceCandidates =
    {
        "Sprites/DarkPixelRPGUI/Artwok/UI/Icon_inventory",
    };
    private const string AddButtonIconResourcePath =
        "Sprites/DarkPixelRPGUI/Artwok/UI/Map_icon_plus";

    private ScreenSwapper _swapper;
    private object _screenContext;

    private VisualElement _partsHolder;
    private Label _partsLabel;
    private Button _coresButton;
    private Button _nodesButton;
    private Button _spellsButton;
    private VisualElement _core;
    private VisualElement _nodeTop;
    private VisualElement _nodeRight;
    private VisualElement _nodeBottom;
    private VisualElement _nodeLeft;
    private VisualElement _addCore;
    private VisualElement _addTop;
    private VisualElement _addRight;
    private VisualElement _addBottom;
    private VisualElement _addLeft;
    private VisualElement _bottomPanel;
    private Button _mergeButton;
    private Button _resetButton;
    private Label _costValueLabel;
    private Label _chanceValueLabel;

    private ArrayCoreDatabaseSO _coreDb;
    private ArrayNodeDatabaseSO _nodeDb;
    private SpellDatabase _spellDb;
    private Sprite _slotBackground;
    private Sprite _addButtonIcon;

    private string _selectedCoreId;
    private readonly Dictionary<NodeSlot, string> _selectedNodeIds = new();
    private readonly Dictionary<NodeSlot, string> _assignedSpellIds = new();
    private NodeSlot? _pendingNodeSlot;
    private NodeSlot? _pendingSpellSlot;
    private NodeSlot _nextNodeSlot = NodeSlot.Top;

    private DragKind _dragKind = DragKind.None;
    private object _dragItem;
    private VisualElement _dragGhost;
    private Vector2 _dragGhostSize;

    protected override string Title => "Spell Merge Room";

    public override void Bind(VisualElement tabRoot, object context)
    {
        base.Bind(tabRoot, context);

        var ctx = context as HomesteadTabContext;
        _swapper = ctx?.Swapper;
        _screenContext = ctx?.ScreenContext;

        CacheUi(tabRoot);
        CacheDatabases();
        RegisterCallbacks();
        BuildCorePartsList();
    }

    public override void Unbind()
    {
        UnregisterCallbacks();
        _partsHolder = null;
        _partsLabel = null;
        _coresButton = null;
        _nodesButton = null;
        _spellsButton = null;
        _core = null;
        _nodeTop = null;
        _nodeRight = null;
        _nodeBottom = null;
        _nodeLeft = null;
        _addCore = null;
        _addTop = null;
        _addRight = null;
        _addBottom = null;
        _addLeft = null;
        _bottomPanel = null;
        _mergeButton = null;
        _resetButton = null;
        _costValueLabel = null;
        _chanceValueLabel = null;
        _coreDb = null;
        _nodeDb = null;
        _spellDb = null;
        _slotBackground = null;
        _addButtonIcon = null;
        _selectedCoreId = null;
        _selectedNodeIds.Clear();
        _assignedSpellIds.Clear();
        _pendingNodeSlot = null;
        _pendingSpellSlot = null;
        _dragKind = DragKind.None;
        _dragItem = null;

        base.Unbind();
        _swapper = null;
        _screenContext = null;
    }

    private void CacheUi(VisualElement tabRoot)
    {
        if (tabRoot == null)
            return;

        _partsHolder = tabRoot.Q<VisualElement>(PartsHolderName);
        _partsLabel = tabRoot.Q<Label>(PartsLabelName);
        _coresButton = tabRoot.Q<Button>(CoresButtonName);
        _nodesButton = tabRoot.Q<Button>(NodesButtonName);
        _spellsButton = tabRoot.Q<Button>(SpellsButtonName);
        _core = tabRoot.Q<VisualElement>(CoreName);
        _nodeTop = tabRoot.Q<VisualElement>(NodeTopName);
        _nodeRight = tabRoot.Q<VisualElement>(NodeRightName);
        _nodeBottom = tabRoot.Q<VisualElement>(NodeBottomName);
        _nodeLeft = tabRoot.Q<VisualElement>(NodeLeftName);
        _addCore = tabRoot.Q<VisualElement>(AddButtonCoreName);
        _addTop = tabRoot.Q<VisualElement>(AddButtonTopName);
        _addRight = tabRoot.Q<VisualElement>(AddButtonRightName);
        _addBottom = tabRoot.Q<VisualElement>(AddButtonBottomName);
        _addLeft = tabRoot.Q<VisualElement>(AddButtonLeftName);
        _bottomPanel = tabRoot.Q<VisualElement>(BottomPanelName);
        _mergeButton = tabRoot.Q<Button>(MergeButtonName);
        _resetButton = tabRoot.Q<Button>(ResetButtonName);
        _costValueLabel = tabRoot.Q<Label>(CostValueName);
        _chanceValueLabel = tabRoot.Q<Label>(ChanceValueName);

        UpdateBottomPanelState();
    }

    private void CacheDatabases()
    {
        var cfg = MyGame.Run.GameConfigProvider.Instance;
        _coreDb = cfg != null ? cfg.ArrayCoreDatabase : null;
        _nodeDb = cfg != null ? cfg.ArrayNodeDatabase : null;
        _spellDb = cfg != null ? cfg.SpellDatabase : null;
        _slotBackground = LoadFirstSprite(SlotBackgroundResourceCandidates);
        _addButtonIcon = Resources.Load<Sprite>(AddButtonIconResourcePath);

        if (_coreDb == null)
            Debug.LogWarning(
                "SpellMergeRoomHomesteadTabController: ArrayCoreDatabase not assigned."
            );
        if (_nodeDb == null)
            Debug.LogWarning(
                "SpellMergeRoomHomesteadTabController: ArrayNodeDatabase not assigned."
            );
        if (_spellDb == null)
            Debug.LogWarning("SpellMergeRoomHomesteadTabController: SpellDatabase not assigned.");
    }

    private void RegisterCallbacks()
    {
        if (_coresButton != null)
            _coresButton.clicked += BuildCorePartsList;
        if (_nodesButton != null)
            _nodesButton.clicked += BuildNodePartsList;
        if (_spellsButton != null)
            _spellsButton.clicked += BuildSpellPartsList;
        if (_resetButton != null)
            _resetButton.clicked += ResetAll;

        RegisterAddButton(
            _addCore,
            _core,
            emptyTooltip: "Array Core",
            filledTooltip: null,
            onEmptyClick: BuildCorePartsList,
            onFilledClick: BuildSpellPartsList,
            hideWhenFilled: true,
            isFilled: IsCoreAssigned,
            onRightClick: null,
            getFilledTooltipOverride: null
        );
        RegisterAddButton(
            _addTop,
            _nodeTop,
            emptyTooltip: "Top Array Node",
            filledTooltip: "Choose Spell for Top Node",
            onEmptyClick: () => BuildNodePartsListForSlot(NodeSlot.Top),
            onFilledClick: () => BeginSpellSelection(NodeSlot.Top),
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Top),
            onRightClick: () => RemoveAssignedSpell(NodeSlot.Top),
            getFilledTooltipOverride: () => GetAssignedSpellTooltip(NodeSlot.Top)
        );
        RegisterAddButton(
            _addRight,
            _nodeRight,
            emptyTooltip: "Right Array Node",
            filledTooltip: "Choose Spell for Right Node",
            onEmptyClick: () => BuildNodePartsListForSlot(NodeSlot.Right),
            onFilledClick: () => BeginSpellSelection(NodeSlot.Right),
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Right),
            onRightClick: () => RemoveAssignedSpell(NodeSlot.Right),
            getFilledTooltipOverride: () => GetAssignedSpellTooltip(NodeSlot.Right)
        );
        RegisterAddButton(
            _addBottom,
            _nodeBottom,
            emptyTooltip: "Bottom Array Node",
            filledTooltip: "Choose Spell for Bottom Node",
            onEmptyClick: () => BuildNodePartsListForSlot(NodeSlot.Bottom),
            onFilledClick: () => BeginSpellSelection(NodeSlot.Bottom),
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Bottom),
            onRightClick: () => RemoveAssignedSpell(NodeSlot.Bottom),
            getFilledTooltipOverride: () => GetAssignedSpellTooltip(NodeSlot.Bottom)
        );
        RegisterAddButton(
            _addLeft,
            _nodeLeft,
            emptyTooltip: "Left Array Node",
            filledTooltip: "Choose Spell for Left Node",
            onEmptyClick: () => BuildNodePartsListForSlot(NodeSlot.Left),
            onFilledClick: () => BeginSpellSelection(NodeSlot.Left),
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Left),
            onRightClick: () => RemoveAssignedSpell(NodeSlot.Left),
            getFilledTooltipOverride: () => GetAssignedSpellTooltip(NodeSlot.Left)
        );

        RefreshAddButtonStates();

        RegisterClearHandlers();
        RegisterDropTargets();
    }

    private void UnregisterCallbacks()
    {
        if (_coresButton != null)
            _coresButton.clicked -= BuildCorePartsList;
        if (_nodesButton != null)
            _nodesButton.clicked -= BuildNodePartsList;
        if (_spellsButton != null)
            _spellsButton.clicked -= BuildSpellPartsList;
        if (_resetButton != null)
            _resetButton.clicked -= ResetAll;
    }

    private void RegisterDropTargets()
    {
        RegisterDropTarget(_core, () => TryDropOnCore());
        RegisterDropTarget(_addCore, () => TryDropOnCore());

        RegisterDropTarget(_nodeTop, () => TryDropOnNode(NodeSlot.Top));
        RegisterDropTarget(_addTop, () => TryDropOnNode(NodeSlot.Top));
        RegisterDropTarget(_nodeRight, () => TryDropOnNode(NodeSlot.Right));
        RegisterDropTarget(_addRight, () => TryDropOnNode(NodeSlot.Right));
        RegisterDropTarget(_nodeBottom, () => TryDropOnNode(NodeSlot.Bottom));
        RegisterDropTarget(_addBottom, () => TryDropOnNode(NodeSlot.Bottom));
        RegisterDropTarget(_nodeLeft, () => TryDropOnNode(NodeSlot.Left));
        RegisterDropTarget(_addLeft, () => TryDropOnNode(NodeSlot.Left));

        if (Root != null)
        {
            Root.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_dragKind == DragKind.None || _dragGhost == null)
                    return;
                UpdateDragGhostPosition(evt.position);
            });
            Root.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0)
                    ClearDrag();
            });
        }
    }

    private void RegisterDropTarget(VisualElement target, Func<bool> onDrop)
    {
        if (target == null || onDrop == null)
            return;

        target.pickingMode = PickingMode.Position;
        target.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            if (_dragKind == DragKind.None)
                return;

            if (onDrop())
                ClearDrag();
        });
    }

    private void StartDrag(DragKind kind, object item, Vector2 pointerPosition)
    {
        if (kind == DragKind.None || item == null || Root == null)
            return;

        _dragKind = kind;
        _dragItem = item;
        HideAllTooltips();

        var sprite = GetDragGhostSprite(kind, item);
        if (sprite == null)
            return;

        if (_dragGhost == null)
        {
            _dragGhost = new VisualElement();
            _dragGhost.name = "DragGhost";
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.position = Position.Absolute;
            Root.Add(_dragGhost);
        }

        _dragGhostSize = GetDragGhostSize(kind);
        _dragGhost.style.width = _dragGhostSize.x;
        _dragGhost.style.height = _dragGhostSize.y;
        _dragGhost.style.backgroundImage = new StyleBackground(sprite);
        _dragGhost.style.display = DisplayStyle.Flex;

        UpdateDragGhostPosition(pointerPosition);
    }

    private void ClearDrag()
    {
        _dragKind = DragKind.None;
        _dragItem = null;
        if (_dragGhost != null)
        {
            _dragGhost.RemoveFromHierarchy();
            _dragGhost = null;
        }
    }

    private void UpdateDragGhostPosition(Vector2 panelPosition)
    {
        if (_dragGhost == null || Root == null)
            return;

        var local = Root.WorldToLocal(panelPosition);
        _dragGhost.style.left = local.x - (_dragGhostSize.x * 0.5f);
        _dragGhost.style.top = local.y - (_dragGhostSize.y * 0.5f);
    }

    private static Sprite GetDragGhostSprite(DragKind kind, object item)
    {
        switch (kind)
        {
            case DragKind.Core:
                return item is ArrayCoreDefinitionSO core ? core.image : null;
            case DragKind.Node:
                return item is ArrayNodeDefinitionSO node ? (node.imageX ?? node.imageY) : null;
            case DragKind.Spell:
                return item is SpellDefinition spell ? spell.icon : null;
            default:
                return null;
        }
    }

    private static Vector2 GetDragGhostSize(DragKind kind)
    {
        switch (kind)
        {
            case DragKind.Node:
                return new Vector2(NodeIconWidthPx, NodeIconHeightPx);
            case DragKind.Core:
            case DragKind.Spell:
            default:
                return new Vector2(CoreIconSizePx, CoreIconSizePx);
        }
    }

    private bool TryDropOnCore()
    {
        if (_dragKind != DragKind.Core)
            return false;

        if (_dragItem is ArrayCoreDefinitionSO core)
        {
            OnCoreSelected(core);
            return true;
        }

        return false;
    }

    private bool TryDropOnNode(NodeSlot slot)
    {
        if (_dragKind == DragKind.Node)
        {
            if (_dragItem is ArrayNodeDefinitionSO node)
            {
                AssignNodeToSlot(node, slot);
                return true;
            }

            return false;
        }

        if (_dragKind == DragKind.Spell)
        {
            if (_dragItem is SpellDefinition spell)
                return AssignSpellToSlot(spell, slot);

            return false;
        }

        return false;
    }

    private void BuildCorePartsList()
    {
        _pendingNodeSlot = null;
        _pendingSpellSlot = null;
        RequestBuild(
            () =>
                BuildPartsList(
                    "Array Cores",
                    _coreDb != null ? _coreDb.All : null,
                    core => core.image,
                    core =>
                        string.IsNullOrWhiteSpace(core.displayName) ? core.id : core.displayName,
                    CoreSlotSizePx,
                    CoreSlotSizePx,
                    CoreIconSizePx,
                    CoreIconSizePx,
                    (slot, core, name) => RegisterArrayTooltip(slot, core, name),
                    core => OnCoreSelected(core),
                    (core, pointerPosition) => StartDrag(DragKind.Core, core, pointerPosition)
                )
        );
    }

    private void BuildNodePartsList()
    {
        _pendingNodeSlot = null;
        _pendingSpellSlot = null;
        BuildNodePartsListInternal();
    }

    private void BuildNodePartsListForSlot(NodeSlot slot)
    {
        _pendingNodeSlot = slot;
        _pendingSpellSlot = null;
        BuildNodePartsListInternal();
    }

    private void BuildNodePartsListInternal()
    {
        RequestBuild(
            () =>
                BuildPartsList(
                    "Array Nodes",
                    _nodeDb != null ? _nodeDb.All : null,
                    node => node.imageX,
                    node =>
                        string.IsNullOrWhiteSpace(node.displayName) ? node.id : node.displayName,
                    NodeSlotWidthPx,
                    NodeSlotHeightPx,
                    NodeIconWidthPx,
                    NodeIconHeightPx,
                    (slot, node, name) => RegisterArrayTooltip(slot, node, name),
                    node => OnNodeSelected(node),
                    (node, pointerPosition) => StartDrag(DragKind.Node, node, pointerPosition)
                )
        );
    }

    private void BuildSpellPartsList()
    {
        _pendingSpellSlot = null;
        BuildSpellPartsListInternal();
    }

    private void BeginSpellSelection(NodeSlot slot)
    {
        _pendingSpellSlot = slot;
        BuildSpellPartsListInternal();
    }

    private void BuildSpellPartsListInternal()
    {
        var learned = GetLearnedSpells();
        RequestBuild(
            () =>
                BuildPartsList(
                    "Spells",
                    learned,
                    spell => spell.icon,
                    spell =>
                        string.IsNullOrWhiteSpace(spell.displayName)
                            ? spell.spellId
                            : spell.displayName,
                    CoreSlotSizePx,
                    CoreSlotSizePx,
                    CoreIconSizePx,
                    CoreIconSizePx,
                    (slot, spell, name) => RegisterSpellTooltip(slot, spell, name),
                    spell => OnSpellSelected(spell),
                    (spell, pointerPosition) => StartDrag(DragKind.Spell, spell, pointerPosition)
                )
        );
    }

    private void BuildPartsList<T>(
        string label,
        IReadOnlyList<T> items,
        Func<T, Sprite> getSprite,
        Func<T, string> getName,
        int slotWidth,
        int slotHeight,
        int iconWidth,
        int iconHeight,
        Action<VisualElement, T, string> registerTooltip,
        Action<T> onItemClicked,
        Action<T, Vector2> onDragStart
    )
        where T : class
    {
        if (_partsHolder == null)
            return;

        _partsHolder.Clear();
        if (_partsLabel != null)
            _partsLabel.text = label;

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.AddToClassList("parts-scroll");
        _partsHolder.Add(scroll);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.alignContent = Align.FlexStart;
        grid.style.justifyContent = Justify.FlexStart;
        grid.AddToClassList("parts-grid");
        scroll.Add(grid);

        if (items == null)
            return;

        var snapshot = new List<T>(items);
        foreach (var item in snapshot)
        {
            if (item == null)
                continue;

            var slot = new VisualElement();
            slot.style.position = Position.Relative;
            slot.style.width = slotWidth;
            slot.style.height = slotHeight;
            slot.style.marginRight = SlotGapPx;
            slot.style.marginBottom = SlotGapPx;
            slot.style.paddingLeft = SlotPaddingPx;
            slot.style.paddingRight = SlotPaddingPx;
            slot.style.paddingTop = SlotPaddingPx;
            slot.style.paddingBottom = SlotPaddingPx;
            slot.pickingMode = PickingMode.Position;
            if (_slotBackground != null)
                slot.style.backgroundImage = new StyleBackground(_slotBackground);

            var name = getName != null ? getName(item) : string.Empty;
            slot.tooltip = null;

            var sprite = getSprite != null ? getSprite(item) : null;
            var icon = new VisualElement();
            icon.AddToClassList("part-icon");
            icon.style.width = iconWidth;
            icon.style.height = iconHeight;
            icon.style.alignSelf = Align.Center;
            icon.pickingMode = PickingMode.Ignore;
            if (sprite != null)
                icon.style.backgroundImage = new StyleBackground(sprite);
            else
                icon.style.backgroundImage = StyleKeyword.None;

            registerTooltip?.Invoke(slot, item, name);

            slot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    onDragStart?.Invoke(item, evt.position);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button == 1)
                {
                    onItemClicked?.Invoke(item);
                    evt.StopPropagation();
                }
            });

            slot.Add(icon);
            grid.Add(slot);
        }
    }

    private IReadOnlyList<SpellDefinition> GetLearnedSpells()
    {
        if (_spellDb == null || _spellDb.All == null)
            return Array.Empty<SpellDefinition>();

        if (!RunSession.IsInitialized || RunSession.Spellbook == null)
            return Array.Empty<SpellDefinition>();

        var result = new List<SpellDefinition>();
        foreach (var kvp in RunSession.Spellbook.Entries)
        {
            var entry = kvp.Value;
            if (entry == null || string.IsNullOrWhiteSpace(entry.spellId))
                continue;

            var def = _spellDb.GetById(entry.spellId);
            if (def != null)
                result.Add(def);
        }

        return result;
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

    private void RegisterArrayTooltip(VisualElement slot, ArrayCoreDefinitionSO core, string name)
    {
        if (slot == null || core == null || _swapper == null)
            return;

        slot.RegisterCallback<PointerEnterEvent>(evt =>
        {
            HideAllTooltips();
            var tooltip = _swapper.GetCustomTooltipElement("ArrayDetailTooltip");
            if (tooltip != null && ArrayDetailTooltipBuilder.TryPopulateForCore(tooltip, core))
            {
                _swapper.ShowCustomTooltipAtWorldPosition(tooltip, evt.position);
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                _swapper.ShowTooltipAtElement(slot, name);
            }
        });
        slot.RegisterCallback<PointerMoveEvent>(evt =>
        {
            var tooltip = _swapper.GetCustomTooltipElement("ArrayDetailTooltip");
            if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
                return;
            _swapper.PositionCustomTooltipAtWorldPosition(tooltip, evt.position);
        });
        slot.RegisterCallback<PointerLeaveEvent>(_ => HideAllTooltips());
        slot.RegisterCallback<PointerOutEvent>(_ => HideAllTooltips());
    }

    private void RegisterArrayTooltip(VisualElement slot, ArrayNodeDefinitionSO node, string name)
    {
        if (slot == null || node == null || _swapper == null)
            return;

        slot.RegisterCallback<PointerEnterEvent>(evt =>
        {
            HideAllTooltips();
            var tooltip = _swapper.GetCustomTooltipElement("ArrayDetailTooltip");
            if (tooltip != null && ArrayDetailTooltipBuilder.TryPopulateForNode(tooltip, node))
            {
                _swapper.ShowCustomTooltipAtWorldPosition(tooltip, evt.position);
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                _swapper.ShowTooltipAtElement(slot, name);
            }
        });
        slot.RegisterCallback<PointerMoveEvent>(evt =>
        {
            var tooltip = _swapper.GetCustomTooltipElement("ArrayDetailTooltip");
            if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
                return;
            _swapper.PositionCustomTooltipAtWorldPosition(tooltip, evt.position);
        });
        slot.RegisterCallback<PointerLeaveEvent>(_ => HideAllTooltips());
        slot.RegisterCallback<PointerOutEvent>(_ => HideAllTooltips());
    }

    private void RegisterSpellTooltip(VisualElement slot, SpellDefinition spell, string name)
    {
        if (slot == null || spell == null || _swapper == null)
            return;

        slot.RegisterCallback<PointerEnterEvent>(evt =>
        {
            HideAllTooltips();
            var entry =
                RunSession.Spellbook != null ? RunSession.Spellbook.Get(spell.spellId) : null;
            var display = !string.IsNullOrWhiteSpace(name) ? name : spell.spellId;
            var mastery = entry != null ? $"Mastery Level: {entry.level}" : "Mastery Level: ?";
            _swapper.ShowTooltipAtElement(slot, $"{display}\n{mastery}");
        });
        slot.RegisterCallback<PointerLeaveEvent>(_ => HideAllTooltips());
        slot.RegisterCallback<PointerOutEvent>(_ => HideAllTooltips());
    }

    private void HideAllTooltips()
    {
        _swapper?.HideCustomTooltip();
        _swapper?.HideTooltip();
    }

    private void RegisterAddButton(
        VisualElement button,
        VisualElement target,
        string emptyTooltip,
        string filledTooltip,
        Action onEmptyClick,
        Action onFilledClick,
        bool hideWhenFilled,
        Func<bool> isFilled,
        Func<bool> onRightClick,
        Func<string> getFilledTooltipOverride
    )
    {
        if (button == null || target == null)
            return;

        button.pickingMode = PickingMode.Position;

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            HideAllTooltips();
            var filled = isFilled != null ? isFilled() : HasBackgroundImage(target);
            if (hideWhenFilled)
                button.style.display = filled ? DisplayStyle.None : DisplayStyle.Flex;

            var overrideText =
                filled && getFilledTooltipOverride != null ? getFilledTooltipOverride() : null;
            var text = !string.IsNullOrWhiteSpace(overrideText)
                ? overrideText
                : (filled ? filledTooltip : emptyTooltip);
            if (!string.IsNullOrWhiteSpace(text))
                _swapper?.ShowTooltipAtElement(button, text);
        });
        button.RegisterCallback<PointerLeaveEvent>(_ => HideAllTooltips());
        button.RegisterCallback<PointerOutEvent>(_ => HideAllTooltips());
        button.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1)
            {
                if (onRightClick != null && onRightClick())
                    evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            var filled = isFilled != null ? isFilled() : HasBackgroundImage(target);
            if (filled)
                onFilledClick?.Invoke();
            else
                onEmptyClick?.Invoke();
            evt.StopPropagation();
        });
    }

    private void RefreshAddButtonStates()
    {
        UpdateAddButtonDisplay(_addCore, _core, hideWhenFilled: true, isFilled: IsCoreAssigned);
        UpdateAddButtonDisplay(
            _addTop,
            _nodeTop,
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Top)
        );
        UpdateAddButtonDisplay(
            _addRight,
            _nodeRight,
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Right)
        );
        UpdateAddButtonDisplay(
            _addBottom,
            _nodeBottom,
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Bottom)
        );
        UpdateAddButtonDisplay(
            _addLeft,
            _nodeLeft,
            hideWhenFilled: false,
            isFilled: () => IsNodeAssigned(NodeSlot.Left)
        );

        UpdateSpellButtonVisual(NodeSlot.Top);
        UpdateSpellButtonVisual(NodeSlot.Right);
        UpdateSpellButtonVisual(NodeSlot.Bottom);
        UpdateSpellButtonVisual(NodeSlot.Left);

        UpdateBottomPanelState();
    }

    private static void UpdateAddButtonDisplay(
        VisualElement button,
        VisualElement target,
        bool hideWhenFilled,
        Func<bool> isFilled
    )
    {
        if (button == null || target == null)
            return;

        if (!hideWhenFilled)
        {
            button.style.display = DisplayStyle.Flex;
            return;
        }

        var filled = isFilled != null ? isFilled() : HasBackgroundImage(target);
        button.style.display = filled ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private static bool HasBackgroundImage(VisualElement target)
    {
        if (target == null)
            return false;

        var bg = target.resolvedStyle.backgroundImage;
        return bg.texture != null;
    }

    private void RegisterClearHandlers()
    {
        RegisterClearHandler(_core, ClearCore);
        RegisterClearHandler(_nodeTop, () => ClearNode(NodeSlot.Top));
        RegisterClearHandler(_nodeRight, () => ClearNode(NodeSlot.Right));
        RegisterClearHandler(_nodeBottom, () => ClearNode(NodeSlot.Bottom));
        RegisterClearHandler(_nodeLeft, () => ClearNode(NodeSlot.Left));
    }

    private void RegisterClearHandler(VisualElement target, Action onClear)
    {
        if (target == null || onClear == null)
            return;

        target.pickingMode = PickingMode.Position;
        target.RegisterCallback<PointerDownEvent>(
            evt =>
            {
                if (evt.button != 1)
                    return;

                if (ReferenceEquals(target, _nodeTop))
                {
                    if (RemoveAssignedSpell(NodeSlot.Top))
                    {
                        evt.StopPropagation();
                        return;
                    }
                }
                else if (ReferenceEquals(target, _nodeRight))
                {
                    if (RemoveAssignedSpell(NodeSlot.Right))
                    {
                        evt.StopPropagation();
                        return;
                    }
                }
                else if (ReferenceEquals(target, _nodeBottom))
                {
                    if (RemoveAssignedSpell(NodeSlot.Bottom))
                    {
                        evt.StopPropagation();
                        return;
                    }
                }
                else if (ReferenceEquals(target, _nodeLeft))
                {
                    if (RemoveAssignedSpell(NodeSlot.Left))
                    {
                        evt.StopPropagation();
                        return;
                    }
                }

                onClear();
                evt.StopPropagation();
            },
            TrickleDown.TrickleDown
        );
    }

    private void ClearCore()
    {
        _selectedCoreId = null;
        if (_core != null)
            _core.style.backgroundImage = StyleKeyword.None;
        RefreshAddButtonStates();
    }

    private void ClearNode(NodeSlot slot)
    {
        if (IsSpellAssigned(slot))
            return;

        if (_selectedNodeIds.ContainsKey(slot))
            _selectedNodeIds.Remove(slot);

        var target = GetNodeElement(slot);
        if (target != null)
            target.style.backgroundImage = StyleKeyword.None;

        RefreshAddButtonStates();
    }

    private void ResetAll()
    {
        HideAllTooltips();
        ClearDrag();

        _selectedCoreId = null;
        _selectedNodeIds.Clear();
        _assignedSpellIds.Clear();
        _pendingNodeSlot = null;
        _pendingSpellSlot = null;
        _nextNodeSlot = NodeSlot.Top;

        if (_core != null)
            _core.style.backgroundImage = StyleKeyword.None;
        if (_nodeTop != null)
            _nodeTop.style.backgroundImage = StyleKeyword.None;
        if (_nodeRight != null)
            _nodeRight.style.backgroundImage = StyleKeyword.None;
        if (_nodeBottom != null)
            _nodeBottom.style.backgroundImage = StyleKeyword.None;
        if (_nodeLeft != null)
            _nodeLeft.style.backgroundImage = StyleKeyword.None;

        if (_addCore != null && _addButtonIcon != null)
        {
            _addCore.style.backgroundImage = new StyleBackground(_addButtonIcon);
            _addCore.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        UpdateSpellButtonVisual(NodeSlot.Top);
        UpdateSpellButtonVisual(NodeSlot.Right);
        UpdateSpellButtonVisual(NodeSlot.Bottom);
        UpdateSpellButtonVisual(NodeSlot.Left);

        RefreshAddButtonStates();
        BuildCorePartsList();
    }

    private bool IsCoreAssigned()
    {
        return !string.IsNullOrWhiteSpace(_selectedCoreId);
    }

    private bool IsNodeAssigned(NodeSlot slot)
    {
        return _selectedNodeIds.TryGetValue(slot, out var id) && !string.IsNullOrWhiteSpace(id);
    }

    private bool IsSpellAssigned(NodeSlot slot)
    {
        return _assignedSpellIds.TryGetValue(slot, out var id) && !string.IsNullOrWhiteSpace(id);
    }

    private int CountAssignedNodes()
    {
        int count = 0;
        foreach (var kvp in _selectedNodeIds)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
                count++;
        }
        return count;
    }

    private int CountAssignedSpells()
    {
        int count = 0;
        foreach (var kvp in _assignedSpellIds)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
                count++;
        }
        return count;
    }

    private void UpdateBottomPanelState()
    {
        if (_bottomPanel != null)
        {
            _bottomPanel.style.display = DisplayStyle.Flex;
        }

        if (_mergeButton != null)
        {
            bool hasCore = IsCoreAssigned();
            int nodeCount = CountAssignedNodes();
            int spellCount = CountAssignedSpells();
            bool allNodesHaveSpells = true;
            foreach (var kvp in _selectedNodeIds)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    if (
                        !_assignedSpellIds.TryGetValue(kvp.Key, out var spellId)
                        || string.IsNullOrWhiteSpace(spellId)
                    )
                    {
                        allNodesHaveSpells = false;
                        break;
                    }
                }
            }
            bool canMerge = hasCore && nodeCount >= 2 && spellCount >= 2 && allNodesHaveSpells;
            _mergeButton.SetEnabled(canMerge);
        }

        if (_costValueLabel != null)
            _costValueLabel.text = IsCoreAssigned() ? GetTotalManaStoneCost().ToString() : "0";
        if (_chanceValueLabel != null)
        {
            float finalChance = IsCoreAssigned() ? ComputeFinalChance() : 0f;
            _chanceValueLabel.text = IsCoreAssigned() ? $"{finalChance:0}%" : "0%";
        }
    }

    private void RequestBuild(Action build)
    {
        if (build == null)
            return;

        if (Root != null)
        {
            Root.schedule.Execute(() => build());
        }
        else
        {
            build();
        }
    }

    private void OnCoreSelected(ArrayCoreDefinitionSO core)
    {
        if (core == null || _core == null)
            return;

        _selectedCoreId = core.id;
        _core.style.backgroundImage =
            core.image != null ? new StyleBackground(core.image) : StyleKeyword.None;
        RefreshAddButtonStates();
    }

    private void OnNodeSelected(ArrayNodeDefinitionSO node)
    {
        if (node == null)
            return;

        var slot = _pendingNodeSlot ?? GetFirstAvailableNodeSlot() ?? _nextNodeSlot;
        AssignNodeToSlot(node, slot);
    }

    private void AssignNodeToSlot(ArrayNodeDefinitionSO node, NodeSlot slot)
    {
        if (node == null)
            return;

        var target = GetNodeElement(slot);
        if (target == null)
            return;

        _selectedNodeIds[slot] = node.id;
        ClearAssignedSpell(slot);
        var sprite = GetNodeSpriteForSlot(node, slot);
        target.style.backgroundImage =
            sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
        _pendingNodeSlot = null;
        _nextNodeSlot = GetNextNodeSlot(slot);

        RefreshAddButtonStates();
    }

    private VisualElement GetNodeElement(NodeSlot slot)
    {
        return slot switch
        {
            NodeSlot.Top => _nodeTop,
            NodeSlot.Right => _nodeRight,
            NodeSlot.Bottom => _nodeBottom,
            NodeSlot.Left => _nodeLeft,
            _ => null,
        };
    }

    private static Sprite GetNodeSpriteForSlot(ArrayNodeDefinitionSO node, NodeSlot slot)
    {
        if (node == null)
            return null;

        return slot == NodeSlot.Top || slot == NodeSlot.Bottom ? node.imageY : node.imageX;
    }

    private void OnSpellSelected(SpellDefinition spell)
    {
        if (spell == null)
            return;

        var slot = _pendingSpellSlot ?? GetFirstAvailableSpellSlot();
        if (slot == null)
            return;
        AssignSpellToSlot(spell, slot.Value);
    }

    private bool AssignSpellToSlot(SpellDefinition spell, NodeSlot slot)
    {
        if (spell == null)
            return false;
        if (!IsNodeAssigned(slot))
            return false;

        _assignedSpellIds[slot] = spell.spellId;
        UpdateSpellButtonVisual(slot);
        _pendingSpellSlot = null;
        UpdateBottomPanelState();
        return true;
    }

    private bool RemoveAssignedSpell(NodeSlot slot)
    {
        if (!_assignedSpellIds.ContainsKey(slot))
            return false;

        _assignedSpellIds.Remove(slot);
        UpdateSpellButtonVisual(slot);
        UpdateBottomPanelState();
        return true;
    }

    private void ClearAssignedSpell(NodeSlot slot)
    {
        if (!_assignedSpellIds.ContainsKey(slot))
            return;

        _assignedSpellIds.Remove(slot);
        UpdateSpellButtonVisual(slot);
        UpdateBottomPanelState();
    }

    private void UpdateSpellButtonVisual(NodeSlot slot)
    {
        var button = GetAddButton(slot);
        if (button == null)
            return;

        if (_assignedSpellIds.TryGetValue(slot, out var spellId) && _spellDb != null)
        {
            var spell = _spellDb.GetById(spellId);
            var icon = spell != null ? spell.icon : null;
            if (icon != null)
            {
                button.style.backgroundImage = new StyleBackground(icon);
                button.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                return;
            }
        }

        if (_addButtonIcon != null)
        {
            button.style.backgroundImage = new StyleBackground(_addButtonIcon);
            button.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
    }

    private string GetAssignedSpellTooltip(NodeSlot slot)
    {
        if (_spellDb == null)
            return null;

        if (!_assignedSpellIds.TryGetValue(slot, out var spellId))
            return null;

        var def = _spellDb.GetById(spellId);
        return def != null ? def.displayName : spellId;
    }

    private VisualElement GetAddButton(NodeSlot slot)
    {
        return slot switch
        {
            NodeSlot.Top => _addTop,
            NodeSlot.Right => _addRight,
            NodeSlot.Bottom => _addBottom,
            NodeSlot.Left => _addLeft,
            _ => null,
        };
    }

    private static NodeSlot GetNextNodeSlot(NodeSlot current)
    {
        return current switch
        {
            NodeSlot.Top => NodeSlot.Right,
            NodeSlot.Right => NodeSlot.Bottom,
            NodeSlot.Bottom => NodeSlot.Left,
            _ => NodeSlot.Top,
        };
    }

    private NodeSlot? GetFirstAvailableNodeSlot()
    {
        var order = new[] { NodeSlot.Top, NodeSlot.Right, NodeSlot.Bottom, NodeSlot.Left };
        for (int i = 0; i < order.Length; i++)
        {
            var slot = order[i];
            if (!IsNodeAssigned(slot))
                return slot;
        }

        return null;
    }

    private NodeSlot? GetFirstAvailableSpellSlot()
    {
        var order = new[] { NodeSlot.Top, NodeSlot.Right, NodeSlot.Bottom, NodeSlot.Left };
        for (int i = 0; i < order.Length; i++)
        {
            var slot = order[i];
            if (!IsNodeAssigned(slot))
                continue;
            if (IsSpellAssigned(slot))
                continue;
            return slot;
        }

        return null;
    }

    private int GetTotalManaStoneCost()
    {
        int total = 0;

        if (_coreDb != null && !string.IsNullOrWhiteSpace(_selectedCoreId))
        {
            var core = _coreDb.GetById(_selectedCoreId);
            if (core != null)
                total += core.manaStoneCost;
        }

        if (_nodeDb != null)
        {
            foreach (var kvp in _selectedNodeIds)
            {
                var id = kvp.Value;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var node = _nodeDb.GetById(id);
                if (node != null)
                    total += node.manaStoneCost;
            }
        }

        return total;
    }

    private float ComputeFinalChance()
    {
        // Keep or tweak this base chance as you like.
        float chance = 10f;

        if (!IsCoreAssigned() || _coreDb == null)
            return 0f;

        var coreDef = _coreDb.GetById(_selectedCoreId);
        if (coreDef == null)
            return 0f;

        // -------------------------
        // CORE bonuses
        // -------------------------

        // Core -> General (once)
        chance += SumGeneral(coreDef.bonuses);

        // Core -> PerMasteryLevel + MatchingTags
        // Applied using ALL spells on ALL assigned nodes
        foreach (var slot in GetNodeSlotsInOrder())
        {
            var spell = GetAssignedSpell(slot);
            if (spell == null)
                continue;

            int masteryLevel = GetSpellMasteryLevel(spell.spellId);

            // Core per level: value * masteryLevel (applied per spell)
            chance += SumPerMasteryLevel(coreDef.bonuses, masteryLevel);

            // Core matching tags: if core.tags overlaps spell.damageTag, add all MatchingTags bonus values (applied per spell)
            chance += SumMatchingTags(coreDef.bonuses, coreDef.tags, GetSpellTags(spell));
        }

        // -------------------------
        // NODE bonuses (per slot)
        // -------------------------
        if (_nodeDb != null)
        {
            foreach (var kvp in _selectedNodeIds)
            {
                var slot = kvp.Key;
                var nodeId = kvp.Value;

                if (string.IsNullOrWhiteSpace(nodeId))
                    continue;

                var nodeDef = _nodeDb.GetById(nodeId);
                if (nodeDef == null)
                    continue;

                // Node -> General (always when node is present)
                chance += SumGeneral(nodeDef.bonuses);

                // Node -> PerMasteryLevel + MatchingTags
                // ONLY from the spell on the corresponding node
                var spell = GetAssignedSpell(slot);
                if (spell == null)
                    continue;

                int masteryLevel = GetSpellMasteryLevel(spell.spellId);

                chance += SumPerMasteryLevel(nodeDef.bonuses, masteryLevel);
                chance += SumMatchingTags(nodeDef.bonuses, nodeDef.tags, GetSpellTags(spell));
            }
        }

        return Mathf.Clamp(chance, 0f, 100f);
    }

    private static float SumGeneral(List<ArrayBonusEntry> bonuses)
    {
        if (bonuses == null)
            return 0f;

        float sum = 0f;
        foreach (var b in bonuses)
        {
            if (b == null)
                continue;
            if (b.bonusType == ArrayBonusType.General)
                sum += b.value;
        }
        return sum;
    }

    private static float SumPerMasteryLevel(List<ArrayBonusEntry> bonuses, int masteryLevel)
    {
        if (bonuses == null || masteryLevel <= 0)
            return 0f;

        float sum = 0f;
        foreach (var b in bonuses)
        {
            if (b == null)
                continue;
            if (b.bonusType == ArrayBonusType.PerMasteryLevel)
                sum += b.value * masteryLevel;
        }
        return sum;
    }

    /// <summary>
    /// Adds up all MatchingTags bonuses if there is ANY overlap between the two tag arrays.
    /// (Currently applied "once per spell" when used in loops.)
    /// </summary>
    private static float SumMatchingTags(
        List<ArrayBonusEntry> bonuses,
        DamageType[] sourceTags,
        DamageType[] spellTags
    )
    {
        if (bonuses == null)
            return 0f;

        if (!HasAnyMatchingTags(sourceTags, spellTags))
            return 0f;

        float sum = 0f;
        foreach (var b in bonuses)
        {
            if (b == null)
                continue;
            if (b.bonusType == ArrayBonusType.MatchingTags)
                sum += b.value;
        }
        return sum;
    }

    private static bool HasAnyMatchingTags(DamageType[] a, DamageType[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            for (int j = 0; j < b.Length; j++)
            {
                if (EqualityComparer<DamageType>.Default.Equals(a[i], b[j]))
                    return true;
            }
        }
        return false;
    }

    private IEnumerable<NodeSlot> GetNodeSlotsInOrder()
    {
        yield return NodeSlot.Top;
        yield return NodeSlot.Right;
        yield return NodeSlot.Bottom;
        yield return NodeSlot.Left;
    }

    private SpellDefinition GetAssignedSpell(NodeSlot slot)
    {
        if (_spellDb == null)
            return null;

        if (!_assignedSpellIds.TryGetValue(slot, out var spellId))
            return null;

        if (string.IsNullOrWhiteSpace(spellId))
            return null;

        return _spellDb.GetById(spellId);
    }

    private int GetSpellMasteryLevel(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return 0;

        var entry = RunSession.Spellbook != null ? RunSession.Spellbook.Get(spellId) : null;
        return entry != null ? Mathf.Max(0, entry.level) : 0;
    }

    // ✅ Now we can implement this properly:
    private DamageType[] GetSpellTags(SpellDefinition spell)
    {
        return spell != null ? spell.damageTag : null;
    }
}
