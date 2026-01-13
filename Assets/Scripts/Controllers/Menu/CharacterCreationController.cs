using System;
using System.Collections.Generic;
using MyGame.Common;
using MyGame.Economy;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public class CharacterCreationController : MonoBehaviour, IScreenController
{
    [Header("Data")]
    [SerializeField]
    private PlayerClassDatabase _classDb;

    private ScreenSwapper _screenSwapper;

    private VisualElement _classContainer;
    private VisualElement _specContainer;
    private VisualElement _startingSpellContainer;

    private Button[] _startingSpellButtons;
    private Button[] _classButtons;
    private Button[] _specButtons;

    private Button _createCharacterButton;
    private Button _backButton;

    private ClassSO _selectedClass;
    private SpecSO _selectedSpec;
    private SpellDefinition _selectedStartingSpell;

    private TextField _name;

    // Stats Panel
    private VisualElement _statsRoot;
    private Label _freePointsLabel;
    private readonly StatsPanelBinder _statsBinder = new();

    private EventCallback<FocusOutEvent> _onNameFocusOut;

    private const int StartingFreePoints = 3;
    private const string SelectedClass = "spec-selected-button";

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _screenSwapper = swapper;

        _classContainer = screenHost.Q<VisualElement>("ClassSelection");
        _specContainer = screenHost.Q<VisualElement>("SpecSelection");

        if (_classContainer == null || _specContainer == null)
        {
            Debug.LogError("CharacterCreationController: Missing class/spec containers.");
            return;
        }

        // Pull from GameConfigProvider
        _classDb = GameConfigProvider.Instance?.PlayerClassDatabase;
        if (_classDb == null)
        {
            Debug.LogError(
                "CharacterCreationController: PlayerClassDatabase not assigned in GameConfigProvider."
            );
            return;
        }

        _backButton = screenHost.Q<Button>("Back");
        if (_backButton != null)
            _backButton.clicked += OnBackClicked;

        _classButtons = _classContainer.Query<Button>().ToList().ToArray();
        _specButtons = _specContainer.Query<Button>().ToList().ToArray();

        _createCharacterButton = screenHost.Q<Button>("Create");
        if (_createCharacterButton != null)
            _createCharacterButton.clicked += OnCreatteClick;

        _name = screenHost.Q<TextField>("Name");

        _startingSpellContainer = screenHost.Q<VisualElement>("StartingSpells");
        _startingSpellButtons = _startingSpellContainer?.Query<Button>().ToList().ToArray();

        // Initial state
        _selectedStartingSpell = null;
        BindStartingSpells(null);

        _createCharacterButton?.SetEnabled(false);

        // ----- Stats panel -----
        _statsRoot = screenHost.Q<VisualElement>("Stats");
        _freePointsLabel = screenHost.Q<Label>("Points");

        _statsBinder.Bind(_statsRoot, _freePointsLabel, StartingFreePoints);
        _statsBinder.Changed += UpdateCreateButtonState;

        _onNameFocusOut = _ => UpdateCreateButtonState();
        _name?.RegisterCallback(_onNameFocusOut);

        _selectedClass = null;
        _selectedSpec = null;

        BindClasses();
        BindSpecs(null);

        ApplyBaseStats();
        UpdateCreateButtonState();
    }

    public void Unbind()
    {
        if (_classButtons != null)
            foreach (var b in _classButtons)
                ClearButtonClick(b);

        if (_specButtons != null)
            foreach (var b in _specButtons)
                ClearButtonClick(b);

        if (_startingSpellButtons != null)
            foreach (var b in _startingSpellButtons)
                ClearButtonClick(b);

        if (_backButton != null)
            _backButton.clicked -= OnBackClicked;

        if (_createCharacterButton != null)
            _createCharacterButton.clicked -= OnCreatteClick;

        if (_name != null && _onNameFocusOut != null)
            _name.UnregisterCallback(_onNameFocusOut);

        _statsBinder.Changed -= UpdateCreateButtonState;
        _statsBinder.Unbind();

        _onNameFocusOut = null;
        _screenSwapper = null;

        _classContainer = null;
        _specContainer = null;
        _startingSpellContainer = null;

        _classButtons = null;
        _specButtons = null;
        _startingSpellButtons = null;

        _selectedClass = null;
        _selectedSpec = null;
        _selectedStartingSpell = null;

        _createCharacterButton = null;
        _backButton = null;
        _name = null;

        _statsRoot = null;
        _freePointsLabel = null;
    }

    // -----------------------
    // CLASSES (from database)
    // -----------------------

    private void BindClasses()
    {
        if (_classDb == null)
        {
            Debug.LogError("CharacterCreationController: _classDb is null.");
            return;
        }

        var allClasses = _classDb.AllClasses;
        if (allClasses == null)
        {
            Debug.LogError("CharacterCreationController: _classDb.AllClasses is null.");
            return;
        }

        for (int i = 0; i < _classButtons.Length; i++)
        {
            var btn = _classButtons[i];
            if (btn == null)
                continue;

            if (i >= allClasses.Count || allClasses[i] == null)
            {
                btn.style.display = DisplayStyle.None;
                ClearButtonClick(btn);
                continue;
            }

            btn.style.display = DisplayStyle.Flex;

            var classDef = allClasses[i];

            var iconVe = btn.Q<VisualElement>("Icon");
            var label = btn.Q<Label>("Label");

            if (iconVe == null || label == null)
            {
                Debug.LogError($"Class Button '{btn.name}' is missing Icon or Label.");
                ClearButtonClick(btn);
                continue;
            }

            iconVe.style.backgroundImage =
                classDef.icon != null ? new StyleBackground(classDef.icon) : StyleKeyword.None;

            iconVe.pickingMode = PickingMode.Ignore;

            label.text = string.IsNullOrWhiteSpace(classDef.displayName)
                ? classDef.id
                : classDef.displayName;

            SetButtonClick(btn, () => OnClassSelected(btn, classDef));
        }
    }

    private void BindSpecs(ClassSO classDef)
    {
        for (int i = 0; i < _specButtons.Length; i++)
        {
            var btn = _specButtons[i];
            if (btn == null)
                continue;

            // Hide until class selected, or if not enough specs
            if (
                classDef == null
                || classDef.spec == null
                || i >= classDef.spec.Count
                || classDef.spec[i] == null
            )
            {
                btn.style.display = DisplayStyle.None;
                ClearButtonClick(btn);
                continue;
            }

            btn.style.display = DisplayStyle.Flex;

            var specDef = classDef.spec[i];

            var iconVe = btn.Q<VisualElement>("Icon");
            var label = btn.Q<Label>("Label");

            if (iconVe == null || label == null)
            {
                Debug.LogError($"Spec Button '{btn.name}' is missing Icon or Label.");
                ClearButtonClick(btn);
                continue;
            }

            iconVe.style.backgroundImage =
                specDef.icon != null ? new StyleBackground(specDef.icon) : StyleKeyword.None;

            iconVe.pickingMode = PickingMode.Ignore;

            label.text = string.IsNullOrWhiteSpace(specDef.displayName)
                ? specDef.id
                : specDef.displayName;

            SetButtonClick(btn, () => OnSpecSelected(btn, specDef));
        }
    }

    private void OnClassSelected(Button clickedButton, ClassSO classDef)
    {
        _selectedClass = classDef;
        _selectedSpec = null;

        SetSelected(clickedButton, _classButtons);

        // Rebuild specs and clear previous selection UI
        ClearSelected(_specButtons);
        BindSpecs(classDef);

        _selectedStartingSpell = null;
        ClearSelected(_startingSpellButtons);
        BindStartingSpells(classDef);

        ApplyBaseStats();
        UpdateCreateButtonState();
    }

    private void OnSpecSelected(Button clickedButton, SpecSO specDef)
    {
        _selectedSpec = specDef;

        SetSelected(clickedButton, _specButtons);

        ApplyBaseStats();
        UpdateCreateButtonState();
    }

    // --- Button click helpers (safe rebinding) ---

    private void SetButtonClick(Button btn, Action onClick)
    {
        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = onClick;
        btn.clicked += onClick;
    }

    private void ClearButtonClick(Button btn)
    {
        if (btn == null)
            return;

        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = null;
    }

    private void SetSelected(Button selected, Button[] group)
    {
        foreach (var btn in group)
        {
            if (btn == null)
                continue;

            if (btn == selected)
                btn.AddToClassList(SelectedClass);
            else
                btn.RemoveFromClassList(SelectedClass);
        }
    }

    private void ClearSelected(Button[] group)
    {
        if (group == null)
            return;

        foreach (var btn in group)
            btn?.RemoveFromClassList(SelectedClass);
    }

    private void UpdateCreateButtonState()
    {
        bool hasName = _name != null && !string.IsNullOrWhiteSpace(_name.value);
        bool hasClass = _selectedClass != null;
        bool hasSpec = _selectedSpec != null;
        bool hasStartingSpell = _selectedStartingSpell != null;

        bool noFreePointsLeft = _statsBinder.FreePoints == 0;

        _createCharacterButton?.SetEnabled(
            hasName && hasClass && hasSpec && noFreePointsLeft && hasStartingSpell
        );
    }

    private void OnCreatteClick()
    {
        var final = _statsBinder.GetFinalStats();
        string nowUtc = DateTime.UtcNow.ToString("O");

        var data = new SaveData
        {
            characterName = _name.value.Trim(),
            classId = _selectedClass.id,
            specId = _selectedSpec.id,
            finalStats = final,
            playerIconId = "default",
            createdUtc = nowUtc,
            lastSavedUtc = nowUtc,
        };

        if (_selectedStartingSpell != null)
        {
            data.spells ??= new List<SavedSpellEntry>();
            data.spells.Add(
                new SavedSpellEntry
                {
                    spellId = _selectedStartingSpell.spellId,
                    level = 1,
                    experience = 0,
                    cooldownRemainingTurns = 0,
                    activeSlotIndex = 0,
                }
            );
        }

        if (SaveService.TryCreateNewSave(data, out int outSlot))
        {
            SaveSession.SetCurrent(outSlot, data);

            RunSession.InitializeFromSave(
                save: data,
                db: GameConfigProvider.Instance.SpellDatabase,
                progression: GameConfigProvider.Instance.SpellProgression
            );

            _screenSwapper.ShowScreen("character");
            return;
        }

        var ctx = new SaveSlotOverlayContext
        {
            mode = SaveSlotOverlayMode.Overwrite,
            pendingSave = data,
        };

        _screenSwapper.ShowOverlay("load_game", ctx);
    }

    private void ApplyBaseStats()
    {
        Stats baseStats = default;

        if (_selectedClass != null)
            baseStats = _selectedClass.baseStats;

        if (_selectedSpec != null)
            baseStats += _selectedSpec.statBonus;

        _statsBinder.SetBaseStats(baseStats, startingFreePointsOverride: StartingFreePoints);
    }

    private void OnBackClicked()
    {
        _screenSwapper.ShowScreen("main_menu");
    }

    // ------------- Starting spells (unchanged from your version) -------------

    private void BindStartingSpells(ClassSO classDef)
    {
        if (_startingSpellButtons == null || _startingSpellButtons.Length == 0)
            return;

        if (classDef == null)
        {
            for (int i = 0; i < _startingSpellButtons.Length; i++)
            {
                _startingSpellButtons[i].style.display = DisplayStyle.None;
                ClearButtonClick(_startingSpellButtons[i]);
            }
            return;
        }

        var config = GameConfigProvider.Instance.StartingSpellConfig;
        if (config == null)
        {
            Debug.LogError(
                "CharacterCreationController: StartingSpellConfig is not assigned in GameConfigProvider."
            );
            return;
        }

        var curated = config.GetForClassId(classDef.id);

        CharacterClass playerClass = MapClassIdToEnum(classDef.id);

        var finalList = new List<SpellDefinition>();
        foreach (var s in curated)
        {
            if (s == null)
                continue;
            if (!s.CanBeUsedBy(playerClass))
                continue;
            finalList.Add(s);
        }

        for (int i = 0; i < _startingSpellButtons.Length; i++)
        {
            var btn = _startingSpellButtons[i];
            if (btn == null)
                continue;

            if (i >= finalList.Count)
            {
                btn.style.display = DisplayStyle.None;
                ClearButtonClick(btn);
                continue;
            }

            btn.style.display = DisplayStyle.Flex;

            var spell = finalList[i];

            var iconVe = btn.Q<VisualElement>("Icon");
            var label = btn.Q<Label>("Label");

            if (iconVe == null || label == null)
            {
                Debug.LogError($"Starting Spell Button '{btn.name}' is missing Icon or Label.");
                continue;
            }

            iconVe.style.backgroundImage =
                spell.icon != null ? new StyleBackground(spell.icon) : StyleKeyword.None;

            iconVe.pickingMode = PickingMode.Ignore;

            label.text = spell.displayName;

            SetButtonClick(btn, () => OnStartingSpellSelected(btn, spell));
        }
    }

    private void OnStartingSpellSelected(Button clickedButton, SpellDefinition spell)
    {
        _selectedStartingSpell = spell;
        SetSelected(clickedButton, _startingSpellButtons);
        UpdateCreateButtonState();
    }

    private CharacterClass MapClassIdToEnum(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
            return CharacterClass.None;

        classId = classId.Trim().ToLowerInvariant();

        return classId switch
        {
            "mage" => CharacterClass.Mage,
            "warrior" => CharacterClass.Warrior,
            "ranger" => CharacterClass.Ranger,
            _ => CharacterClass.None,
        };
    }
}
