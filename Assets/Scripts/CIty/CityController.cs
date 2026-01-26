using UnityEngine;
using UnityEngine.UIElements;

public class CityController : MonoBehaviour, IScreenController
{
    private sealed class TabEntry
    {
        public string Name;
        public VisualElement Header;
        public VisualTreeAsset Template;
        public ICityTabController Controller;
        public VisualElement View;
    }

    private const string PickedClass = "panel-picked";

    private VisualElement _root;
    private FooterSectionController _footer;

    [Header("City Tab Templates")]
    [SerializeField]
    private VisualTreeAsset _trainerTabUxml;

    [Header("City Data")]
    [SerializeField]
    private TrainerDatabaseSO _trainerDatabase;

    [SerializeField]
    private VisualTreeAsset _adventureHubTabUxml;

    [SerializeField]
    private VisualTreeAsset _blacksmithTabUxml;

    [SerializeField]
    private VisualTreeAsset _alchemistTabUxml;

    [SerializeField]
    private VisualTreeAsset _arrayMasterTabUxml;

    [SerializeField]
    private VisualTreeAsset _equipmentMerchantTabUxml;

    [SerializeField]
    private VisualTreeAsset _itemsMerchantTabUxml;

    private VisualElement _headerRoot;
    private VisualElement _bodyRoot;
    private VisualElement _contentHost;

    // headers
    private VisualElement _hTrainer;
    private VisualElement _hAdventureHub;
    private VisualElement _hBlacksmith;
    private VisualElement _hAlchemist;
    private VisualElement _hArrayMaster;
    private VisualElement _hEquipmentMerchant;
    private VisualElement _hItemsMerchant;
    private readonly System.Collections.Generic.List<VisualElement> _headers = new();

    private readonly System.Collections.Generic.List<TabEntry> _tabs = new();
    private TabEntry _activeTab;

    private ScreenSwapper _swapper;
    private object _context;
    private CityTabContext _tabContext;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;

        _swapper = swapper;
        _context = context;

        _tabContext = new CityTabContext
        {
            Swapper = swapper,
            ScreenContext = context,
            TrainerDatabase = _trainerDatabase,
        };

        // Optional: keep footer highlight on "City"
        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "City");

        if (_root == null)
        {
            Debug.LogError("CityController.Bind: screenHost is null.");
            return;
        }

        // Scope to City root (MainCanvas exists by name in UXML)
        var main = _root.Q<VisualElement>("MainCanvas");
        var top = main != null ? main.Q<VisualElement>("TopPart") : null;

        // UI Builder may rename the main frame. Support both older and current names.
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
                $"CityController.Bind: Missing roots. FrameFound={card != null}, Header={_headerRoot != null}, Body={_bodyRoot != null}, ContentHost={_contentHost != null}"
            );
            return;
        }

        // Query headers (scoped)
        _hTrainer = _headerRoot.Q<VisualElement>("TrainerTab");
        _hAdventureHub = _headerRoot.Q<VisualElement>("AdventureHubTab");
        _hBlacksmith = _headerRoot.Q<VisualElement>("BlacksmithTab");
        _hAlchemist = _headerRoot.Q<VisualElement>("AlchemistTab");
        _hArrayMaster = _headerRoot.Q<VisualElement>("ArrayMasterTab");
        _hEquipmentMerchant = _headerRoot.Q<VisualElement>("EquipmentMerchantTab");
        _hItemsMerchant = _headerRoot.Q<VisualElement>("ItemsMerchantTab");

        _headers.Clear();
        _headers.AddRange(
            new[]
            {
                _hTrainer,
                _hAdventureHub,
                _hBlacksmith,
                _hAlchemist,
                _hArrayMaster,
                _hEquipmentMerchant,
                _hItemsMerchant,
            }
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
                Name = "Trainer",
                Header = _hTrainer,
                Template = _trainerTabUxml,
                Controller = new TrainerCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "AdventureHub",
                Header = _hAdventureHub,
                Template = _adventureHubTabUxml,
                Controller = new AdventureHubCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "Blacksmith",
                Header = _hBlacksmith,
                Template = _blacksmithTabUxml,
                Controller = new BlacksmithCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "Alchemist",
                Header = _hAlchemist,
                Template = _alchemistTabUxml,
                Controller = new AlchemistCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "ArrayMaster",
                Header = _hArrayMaster,
                Template = _arrayMasterTabUxml,
                Controller = new ArrayMasterCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "EquipmentMerchant",
                Header = _hEquipmentMerchant,
                Template = _equipmentMerchantTabUxml,
                Controller = new EquipmentMerchantCityTabController(),
            }
        );
        _tabs.Add(
            new TabEntry
            {
                Name = "ItemsMerchant",
                Header = _hItemsMerchant,
                Template = _itemsMerchantTabUxml,
                Controller = new ItemsMerchantCityTabController(),
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
        SelectHeader(_hTrainer);
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

        _hTrainer = null;
        _hAdventureHub = null;
        _hBlacksmith = null;
        _hAlchemist = null;
        _hArrayMaster = null;
        _hEquipmentMerchant = null;
        _hItemsMerchant = null;

        _headerRoot = null;
        _bodyRoot = null;
        _contentHost = null;

        _footer?.Unbind();
        _footer = null;

        _context = null;
        _swapper = null;
        _tabContext = null;
        _root = null;
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
            tab.Controller.Bind(tab.View, _tabContext);

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
