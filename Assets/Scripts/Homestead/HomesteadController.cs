using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HomesteadController : MonoBehaviour, IScreenController
{
    private sealed class TabEntry
    {
        public string Name;
        public VisualElement Header;
        public VisualTreeAsset Template;
        public IHomesteadTabController Controller;
        public VisualElement View;
    }

    private sealed class PlaceholderHomesteadTabController : SimpleHomesteadTabControllerBase
    {
        private readonly string _title;

        public PlaceholderHomesteadTabController(string title)
        {
            _title = title;
        }

        protected override string Title => _title;
    }

    private const string PickedClass = "panel-picked";

    private VisualElement _root;
    private FooterSectionController _footer;

    [Header("Homestead Tab Templates")]
    [SerializeField]
    private VisualTreeAsset _houseTabUxml;

    [SerializeField]
    private VisualTreeAsset _spellMergeRoomTabUxml;

    [SerializeField]
    private VisualTreeAsset _smithyTabUxml;

    [SerializeField]
    private VisualTreeAsset _alchemyRoomTabUxml;

    [SerializeField]
    private VisualTreeAsset _tbd1TabUxml;

    [SerializeField]
    private VisualTreeAsset _tbd2TabUxml;

    [SerializeField]
    private VisualTreeAsset _tbd3TabUxml;

    private VisualElement _headerRoot;
    private VisualElement _bodyRoot;
    private VisualElement _contentHost;

    // headers
    private VisualElement _hHouse;
    private VisualElement _hSpellMergeRoom;
    private VisualElement _hSmithy;
    private VisualElement _hAlchemyRoom;
    private VisualElement _hTbd1;
    private VisualElement _hTbd2;
    private VisualElement _hTbd3;
    private readonly List<VisualElement> _headers = new();

    private readonly List<TabEntry> _tabs = new();
    private TabEntry _activeTab;

    private ScreenSwapper _swapper;
    private object _context;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;
        _context = context;

        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "Homestead");

        if (_root == null)
        {
            Debug.LogError("HomesteadController.Bind: screenHost is null.");
            return;
        }

        var main = _root.Q<VisualElement>("MainCanvas");
        var top = main != null ? main.Q<VisualElement>("TopPart") : null;

        var card =
            (top != null ? top.Q<VisualElement>("MainFrame") : null)
            ?? (top != null ? top.Q<VisualElement>("VisualElement") : null)
            ?? (main != null ? main.Q<VisualElement>("MainFrame") : null)
            ?? (main != null ? main.Q<VisualElement>("VisualElement") : null)
            ?? _root.Q<VisualElement>("MainFrame")
            ?? _root.Q<VisualElement>("VisualElement");

        _headerRoot = card != null ? card.Q<VisualElement>("Header") : null;
        _bodyRoot = card != null ? card.Q<VisualElement>("Body") : null;
        _contentHost = _bodyRoot != null ? _bodyRoot.Q<VisualElement>("ContentHost") : null;

        if (_headerRoot == null || _bodyRoot == null || _contentHost == null)
        {
            Debug.LogError(
                $"HomesteadController.Bind: Missing roots. FrameFound={card != null}, Header={_headerRoot != null}, Body={_bodyRoot != null}, ContentHost={_contentHost != null}"
            );
            return;
        }

        // Query headers (scoped)
        _hHouse = _headerRoot.Q<VisualElement>("HouseTab");
        _hSpellMergeRoom = _headerRoot.Q<VisualElement>("SpellMergeRoomTab");
        _hSmithy = _headerRoot.Q<VisualElement>("SmithyTab");
        _hAlchemyRoom = _headerRoot.Q<VisualElement>("AlchemyRoomTab");
        _hTbd1 = _headerRoot.Q<VisualElement>("TBD1");
        _hTbd2 = _headerRoot.Q<VisualElement>("TBD2");
        _hTbd3 = _headerRoot.Q<VisualElement>("TBD3");

        _headers.Clear();
        _headers.AddRange(
            new[] { _hHouse, _hSpellMergeRoom, _hSmithy, _hAlchemyRoom, _hTbd1, _hTbd2, _hTbd3 }
        );

        // Make header blocks clickable (ignore inner label)
        foreach (var h in _headers)
        {
            if (h == null)
                continue;

            h.pickingMode = PickingMode.Position;
            var label = h.Q<Label>();
            if (label != null)
                label.pickingMode = PickingMode.Ignore;
        }

        // Build tabs list
        _tabs.Clear();
        _tabs.Add(
            new TabEntry
            {
                Name = "House",
                Header = _hHouse,
                Template = _houseTabUxml,
                Controller = new PlaceholderHomesteadTabController("House"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "SpellMergeRoom",
                Header = _hSpellMergeRoom,
                Template = _spellMergeRoomTabUxml,
                Controller = new PlaceholderHomesteadTabController("Spell Merge Room"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "Smithy",
                Header = _hSmithy,
                Template = _smithyTabUxml,
                Controller = new PlaceholderHomesteadTabController("Smithy"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "AlchemyRoom",
                Header = _hAlchemyRoom,
                Template = _alchemyRoomTabUxml,
                Controller = new PlaceholderHomesteadTabController("Alchemy Room"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "TBD1",
                Header = _hTbd1,
                Template = _tbd1TabUxml,
                Controller = new PlaceholderHomesteadTabController("TBD1"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "TBD2",
                Header = _hTbd2,
                Template = _tbd2TabUxml,
                Controller = new PlaceholderHomesteadTabController("TBD2"),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "TBD3",
                Header = _hTbd3,
                Template = _tbd3TabUxml,
                Controller = new PlaceholderHomesteadTabController("TBD3"),
            }
        );

        // Register clicks
        foreach (var tab in _tabs)
        {
            if (tab.Header == null)
                continue;

            tab.Header.RegisterCallback<PointerDownEvent>(_ =>
            {
                SelectHeader(tab.Header);
                ShowTab(tab);
            });
        }

        // Default
        SelectHeader(_hHouse);
        ShowTab(_tabs[0]);
    }

    public void Unbind()
    {
        if (_activeTab != null)
            _activeTab.Controller?.OnHide();

        foreach (var tab in _tabs)
            tab.Controller?.Unbind();

        _tabs.Clear();
        _activeTab = null;

        _headers.Clear();

        _hHouse = null;
        _hSpellMergeRoom = null;
        _hSmithy = null;
        _hAlchemyRoom = null;
        _hTbd1 = null;
        _hTbd2 = null;
        _hTbd3 = null;

        _headerRoot = null;
        _bodyRoot = null;
        _contentHost = null;

        _footer?.Unbind();
        _footer = null;

        _context = null;
        _swapper = null;
        _root = null;
    }

    private void ShowTab(TabEntry tab)
    {
        if (tab == null)
            return;

        if (tab == _activeTab)
            return;

        // hide current
        if (_activeTab != null)
        {
            if (_activeTab.View != null)
                _activeTab.View.style.display = DisplayStyle.None;
            _activeTab.Controller?.OnHide();
        }

        // create if needed
        if (tab.View == null)
        {
            tab.View = tab.Template != null ? tab.Template.CloneTree() : new VisualElement();
            tab.View.style.flexGrow = 1;
            tab.View.style.minHeight = 0;

            _contentHost.Add(tab.View);
            tab.Controller?.Bind(tab.View, _context);

            tab.View.style.display = DisplayStyle.None;
        }

        // show new
        tab.View.style.display = DisplayStyle.Flex;
        tab.Controller?.OnShow();

        _activeTab = tab;
    }

    private void SelectHeader(VisualElement selectedHeader)
    {
        foreach (var h in _headers)
            h?.RemoveFromClassList(PickedClass);

        selectedHeader?.AddToClassList(PickedClass);
    }
}
