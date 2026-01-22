using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DashboardController : MonoBehaviour, IScreenController
{
    private sealed class TabEntry
    {
        public VisualElement Header;
        public VisualTreeAsset Template;
        public IDashboardTabController Controller;
        public VisualElement View;
    }

    private const string PickedClass = "panel-picked";

    [SerializeField]
    private VisualTreeAsset _characterTabUxml;

    [SerializeField]
    private VisualTreeAsset _spellsTabUxml;

    [SerializeField]
    private VisualTreeAsset _inventoryTabUxml;

    [SerializeField]
    private VisualTreeAsset _talentsTabUxml;

    [SerializeField]
    private VisualTreeAsset _spellRowTemplate; // needed by SpellSectionController

    private VisualElement _root;
    private ScreenSwapper _swapper;
    private object _context;

    private VisualElement _contentHost;

    // headers
    private VisualElement _hCharacter;
    private VisualElement _hSpells;
    private VisualElement _hInventory;
    private VisualElement _hTalents;
    private readonly List<VisualElement> _headers = new();

    private readonly List<TabEntry> _tabs = new();
    private TabEntry _activeTab;

    private MyGame.Inventory.PlayerEquipment _subscribedEquipment;
    private System.Action _onEquipmentChanged;

    private FooterSectionController _footer;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;
        _context = context;

        // footer
        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "Character");

        // query headers
        _hCharacter = _root.Q<VisualElement>("CharacterTab");
        _hSpells = _root.Q<VisualElement>("SpellsTab");
        _hInventory = _root.Q<VisualElement>("InventoryTab");
        _hTalents = _root.Q<VisualElement>("TalentsTab");

        _headers.Clear();
        _headers.AddRange(new[] { _hCharacter, _hSpells, _hInventory, _hTalents });

        // make whole header clickable (ignore inner label)
        foreach (var h in _headers)
        {
            if (h == null)
                continue;
            h.pickingMode = PickingMode.Position;

            var label = h.Q<Label>();
            if (label != null)
                label.pickingMode = PickingMode.Ignore;
        }

        // content host
        _contentHost = _root.Q<VisualElement>("ContentHost");

        // build tabs (clear first!)
        _tabs.Clear();
        _tabs.Add(
            new TabEntry
            {
                Header = _hCharacter,
                Template = _characterTabUxml,
                Controller = new CharacterTabController(_swapper),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Header = _hSpells,
                Template = _spellsTabUxml,
                Controller = new SpellsTabController(_spellRowTemplate, _swapper), // we’ll write this wrapper
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Header = _hInventory,
                Template = _inventoryTabUxml,
                Controller = new InventoryTabController(_swapper),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Header = _hTalents,
                Template = _talentsTabUxml,
                Controller = new TalentsTabController(),
            }
        );

        // register clicks (generic)
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

        // default
        SelectHeader(_hCharacter);
        ShowTab(_tabs[0]);

        SubscribeToEquipmentChanges();
    }

    public void Unbind()
    {
        UnsubscribeFromEquipmentChanges();

        // Note: If you want to unregister callbacks, we can do that next step.
        // For now keep simple: screen is recreated, so safe.

        if (_activeTab != null)
            _activeTab.Controller?.OnHide();

        foreach (var tab in _tabs)
            tab.Controller?.Unbind();

        _tabs.Clear();
        _activeTab = null;

        _footer?.Unbind();
        _footer = null;

        _headers.Clear();

        _hCharacter = _hSpells = _hInventory = _hTalents = null;
        _contentHost = null;

        _context = null;
        _swapper = null;
        _root = null;
    }

    private void SubscribeToEquipmentChanges()
    {
        var eq = MyGame.Run.RunSession.Equipment;
        if (eq == null)
            return;

        if (ReferenceEquals(eq, _subscribedEquipment))
            return;

        UnsubscribeFromEquipmentChanges();

        _subscribedEquipment = eq;
        _onEquipmentChanged ??= OnEquipmentChanged;
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

    private void OnEquipmentChanged()
    {
        // Equipment changes affect stats/derived panels. Mark tabs dirty so they refresh on show.
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i]?.Controller?.MarkDirty();
    }

    private void ShowTab(TabEntry tab)
    {
        if (tab == null || tab.Template == null || tab.Controller == null)
            return;

        if (tab == _activeTab)
            return;

        // hide current
        if (_activeTab != null)
        {
            if (_activeTab.View != null)
                _activeTab.View.style.display = DisplayStyle.None;

            _activeTab.Controller.OnHide();
        }

        // create if needed
        if (tab.View == null)
        {
            tab.View = tab.Template.CloneTree();
            tab.View.style.flexGrow = 1;
            tab.View.style.minHeight = 0;

            _contentHost.Add(tab.View);
            tab.Controller.Bind(tab.View, _context);

            tab.View.style.display = DisplayStyle.None;
        }

        // show new
        tab.View.style.display = DisplayStyle.Flex;
        tab.Controller.OnShow();

        _activeTab = tab;
    }

    private void SelectHeader(VisualElement selectedHeader)
    {
        foreach (var h in _headers)
            h?.RemoveFromClassList(PickedClass);

        selectedHeader?.AddToClassList(PickedClass);
    }
}
