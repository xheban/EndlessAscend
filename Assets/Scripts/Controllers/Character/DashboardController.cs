// DashboardController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DashboardController : MonoBehaviour, IScreenController
{
    private const string PickedClass = "panel-picked";

    private VisualElement _root;
    private ScreenSwapper _swapper;

    // headers
    private VisualElement _hCharacter;
    private VisualElement _hSpells;
    private VisualElement _hInventory;
    private VisualElement _hTalents;

    // panels
    private VisualElement _pCharacter;
    private VisualElement _pSpells;
    private VisualElement _pInventory;
    private VisualElement _pTalents;

    private readonly List<VisualElement> _headers = new();
    private readonly List<VisualElement> _panels = new();

    // callbacks (so we can unregister)
    private EventCallback<PointerDownEvent> _onCharacterTabDown;
    private EventCallback<PointerDownEvent> _onSpellsTabDown;
    private EventCallback<PointerDownEvent> _onInventoryTabDown;
    private EventCallback<PointerDownEvent> _onTalentsTabDown;

    // Section controllers
    private CharacterSectionController _characterSection;
    private SpellSectionController _spellSection;

    [SerializeField]
    private VisualTreeAsset _spellRowTemplate;

    private SpellsListSectionController _spellsListSection;
    private FooterSectionController _footer;

    // ✅ Option B signature
    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;

        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "Character");

        // Header VisualElements (names from your UXML)
        _hCharacter = _root.Q<VisualElement>("CharacterTab");
        _hSpells = _root.Q<VisualElement>("SpellsTab");
        _hInventory = _root.Q<VisualElement>("InventoryTab");
        _hTalents = _root.Q<VisualElement>("TalentsTab");

        _headers.Clear();
        _headers.AddRange(new[] { _hCharacter, _hSpells, _hInventory, _hTalents });

        // Body panels
        _pCharacter = _root.Q<VisualElement>("CharacterPanel");
        _pSpells = _root.Q<VisualElement>("SpellsPanel");
        _pInventory = _root.Q<VisualElement>("InventoryPanel");
        _pTalents = _root.Q<VisualElement>("TalentsPanel");

        _panels.Clear();
        _panels.AddRange(new[] { _pCharacter, _pSpells, _pInventory, _pTalents });

        // Ensure header elements can receive pointer events
        foreach (var h in _headers)
        {
            if (h == null)
                continue;

            h.pickingMode = PickingMode.Position;

            // Optional: ignore picking on the inner label so the whole header is clickable
            var label = h.Q<Label>();
            if (label != null)
                label.pickingMode = PickingMode.Ignore;
        }

        // ✅ Create callbacks ONCE per bind so we can unregister later
        _onCharacterTabDown = _ => SelectTab(_hCharacter, _pCharacter);
        _onSpellsTabDown = _ => SelectTab(_hSpells, _pSpells);
        _onInventoryTabDown = _ => SelectTab(_hInventory, _pInventory);
        _onTalentsTabDown = _ => SelectTab(_hTalents, _pTalents);

        // ✅ Register callbacks
        RegisterHeader(_hCharacter, _onCharacterTabDown);
        RegisterHeader(_hSpells, _onSpellsTabDown);
        RegisterHeader(_hInventory, _onInventoryTabDown);
        RegisterHeader(_hTalents, _onTalentsTabDown);

        // Bind sections
        _characterSection = new CharacterSectionController();
        _characterSection.Bind(_pCharacter);

        _spellSection = new SpellSectionController(_spellRowTemplate);
        _spellSection.Bind(_pSpells, swapper);

        // Default tab
        SelectTab(_hCharacter, _pCharacter);
    }

    public void Unbind()
    {
        // ✅ Unregister tab callbacks (prevents double events after re-open)
        UnregisterHeader(_hCharacter, _onCharacterTabDown);
        UnregisterHeader(_hSpells, _onSpellsTabDown);
        UnregisterHeader(_hInventory, _onInventoryTabDown);
        UnregisterHeader(_hTalents, _onTalentsTabDown);

        _onCharacterTabDown = null;
        _onSpellsTabDown = null;
        _onInventoryTabDown = null;
        _onTalentsTabDown = null;

        _characterSection?.Unbind();
        _characterSection = null;

        _spellSection?.Unbind();
        _spellSection = null;

        _spellsListSection?.Unbind();
        _spellsListSection = null;

        _footer?.Unbind();
        _footer = null;

        _headers.Clear();
        _panels.Clear();

        _root = null;
        _swapper = null;

        _hCharacter = null;
        _hSpells = null;
        _hInventory = null;
        _hTalents = null;

        _pCharacter = null;
        _pSpells = null;
        _pInventory = null;
        _pTalents = null;
    }

    private static void RegisterHeader(VisualElement header, EventCallback<PointerDownEvent> cb)
    {
        if (header == null || cb == null)
            return;
        header.RegisterCallback(cb);
    }

    private static void UnregisterHeader(VisualElement header, EventCallback<PointerDownEvent> cb)
    {
        if (header == null || cb == null)
            return;
        header.UnregisterCallback(cb);
    }

    private void SelectTab(VisualElement selectedHeader, VisualElement selectedPanel)
    {
        if (selectedPanel == null)
            return;

        // 1) Picked class
        foreach (var h in _headers)
            h?.RemoveFromClassList(PickedClass);

        selectedHeader?.AddToClassList(PickedClass);

        // 2) Panels show/hide
        foreach (var p in _panels)
            if (p != null)
                p.style.display = DisplayStyle.None;

        selectedPanel.style.display = DisplayStyle.Flex;

        // 3) Tab shown hooks
        if (selectedPanel == _pSpells)
        {
            _spellSection?.OnTabShown();
        }
    }
}
