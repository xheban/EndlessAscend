using System.Collections.Generic;
using MyGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SettingsController : MonoBehaviour, IOverlayController
{
    private sealed class TabEntry
    {
        public VisualElement Header;
        public VisualElement Panel;
        public ISettingsTabController Controller;
        public string Name; // optional, for debugging
    }

    private const string PickedClass = "panel-picked";

    private VisualElement _root;
    private VisualElement _settings;
    private VisualElement _headerRoot;
    private VisualElement _bodyRoot;

    private ScreenSwapper _swapper;
    private object _context;

    // header tabs
    private VisualElement _hGame;
    private VisualElement _hCombat;
    private readonly List<VisualElement> _headers = new();

    // body panels
    private VisualElement _pGame;
    private VisualElement _pCombat;

    private readonly List<TabEntry> _tabs = new();
    private TabEntry _activeTab;

    private PixelSlider _musicSlider;
    private System.Action<float> _onMusicSliderChanged;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;
        _context = context;

        if (_root == null)
        {
            Debug.LogError("SettingsController.Bind: screenHost is null.");
            return;
        }

        _settings = _root.Q<VisualElement>("Settings");

        if (_settings == null)
        {
            Debug.LogError(
                "SettingsController.Bind: Could not find 'Settings' element under screenHost."
            );
            return;
        }

        _musicSlider = _settings.Q<PixelSlider>("MusicSlider");
        _headerRoot = _settings.Q<VisualElement>("Header");
        _bodyRoot = _settings.Q<VisualElement>("Body");

        if (_headerRoot == null || _bodyRoot == null || _musicSlider == null)
        {
            Debug.LogError(
                $"SettingsController.Bind: Missing roots. Header={_headerRoot != null}, Body={_bodyRoot != null}, MusicSlider={_musicSlider != null}"
            );
            return;
        }

        _onMusicSliderChanged = value =>
        {
            if (MusicManager.Instance == null)
                return;

            float volume01 = Mathf.InverseLerp(_musicSlider.Min, _musicSlider.Max, value);
            MusicManager.Instance.ApplyVolume(volume01);
        };

        _musicSlider.ValueChanged += _onMusicSliderChanged;

        if (MusicManager.Instance != null)
        {
            float initial = Mathf.Lerp(
                _musicSlider.Min,
                _musicSlider.Max,
                MusicManager.Instance.Volume
            );
            _musicSlider.SetValueWithoutNotify(initial);
        }

        // Query header tabs (scoped)
        _hGame = _headerRoot.Q<VisualElement>("GameSettings");
        _hCombat = _headerRoot.Q<VisualElement>("CombatSettings");

        _headers.Clear();
        _headers.AddRange(new[] { _hGame, _hCombat });

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

        // Query body panels (scoped)
        _pGame = _bodyRoot.Q<VisualElement>("GameSettings");
        _pCombat = _bodyRoot.Q<VisualElement>("CombatSettings");

        if (_pGame == null || _pCombat == null)
        {
            Debug.LogError(
                $"SettingsController.Bind: Missing panels. Game={_pGame != null}, Combat={_pCombat != null}"
            );
            return;
        }

        // Build tabs list (Dashboard style)
        _tabs.Clear();
        _tabs.Add(
            new TabEntry
            {
                Name = "Game",
                Header = _hGame,
                Panel = _pGame,
                Controller = new SettingsGameTabController(),
            }
        );

        _tabs.Add(
            new TabEntry
            {
                Name = "Combat",
                Header = _hCombat,
                Panel = _pCombat,
                Controller = new SettingsCombatTabController(_swapper), // keybinds live here
            }
        );

        // Bind each controller ONCE to its panel (no populating required here)
        foreach (var tab in _tabs)
        {
            if (tab.Panel == null || tab.Controller == null)
                continue;

            tab.Controller.Bind(tab.Panel, _context);

            // start hidden; ShowTab will turn one on
            tab.Panel.style.display = DisplayStyle.None;
        }

        // Register clicks (generic)
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

        // Default tab
        SelectHeader(_hGame);
        ShowTab(_tabs[0]);
    }

    public void Unbind()
    {
        if (_musicSlider != null && _onMusicSliderChanged != null)
            _musicSlider.ValueChanged -= _onMusicSliderChanged;
        _onMusicSliderChanged = null;

        if (_activeTab != null)
            _activeTab.Controller?.OnHide();

        foreach (var tab in _tabs)
            tab.Controller?.Unbind();

        _tabs.Clear();
        _activeTab = null;

        _headers.Clear();
        _hGame = _hCombat = null;

        _pGame = _pCombat = null;

        _headerRoot = null;
        _bodyRoot = null;
        _settings = null;

        _musicSlider = null;

        _context = null;
        _swapper = null;
        _root = null;
    }

    private void ShowTab(TabEntry tab)
    {
        if (tab == null || tab.Panel == null || tab.Controller == null)
            return;

        if (tab == _activeTab)
            return;

        // hide current
        if (_activeTab != null)
        {
            _activeTab.Panel.style.display = DisplayStyle.None;
            _activeTab.Controller.OnHide();
        }

        // show new
        tab.Panel.style.display = DisplayStyle.Flex;
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
