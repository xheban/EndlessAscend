using System;
using System.Collections.Generic;
using System.Text;
using MyGame.Common;
using MyGame.Economy;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using MyName.Equipment;
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

    private VisualElement _avatarImage; // the inner "Model" element
    private VisualElement _avatarButtonsRoot; // "Buttons"
    private VisualElement _avatarLeft; // "Left"
    private VisualElement _avatarRight;
    private IReadOnlyList<PlayerAvatarEntry> _availableAvatars;
    private int _avatarIndex = 0;
    private string _selectedAvatarId = null;
    private PlayerAvatarDatabase _avatarDb;

    // store handlers so we can unregister in Unbind
    private EventCallback<ClickEvent> _onAvatarLeftClick;
    private EventCallback<ClickEvent> _onAvatarRightClick;

    // Stats Panel
    private VisualElement _statsRoot;
    private Label _freePointsLabel;
    private readonly StatsPanelBinder _statsBinder = new();

    private EventCallback<FocusOutEvent> _onNameFocusOut;

    private readonly Dictionary<
        VisualElement,
        (EventCallback<PointerEnterEvent> enter, EventCallback<PointerLeaveEvent> leave)
    > _tooltipHandlers =
        new Dictionary<
            VisualElement,
            (EventCallback<PointerEnterEvent>, EventCallback<PointerLeaveEvent>)
        >();

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

        _avatarDb = GameConfigProvider.Instance?.PlayerAvatarDatabase; // or keep serialized

        var holder = screenHost.Q<VisualElement>("ModelHolder");
        _avatarImage = holder?.Q<VisualElement>("Model");

        _avatarButtonsRoot = screenHost.Q<VisualElement>("Buttons");
        _avatarLeft = screenHost.Q<VisualElement>("Left");
        _avatarRight = screenHost.Q<VisualElement>("right"); // yes, lowercase in your UXML

        // Start empty (no class selected yet)
        SetAvatarUIVisible(false);
        ClearAvatarPreview();

        // Make arrows clickable (VisualElement -> ClickEvent)
        _onAvatarLeftClick = _ => StepAvatar(-1);
        _onAvatarRightClick = _ => StepAvatar(+1);

        _avatarLeft?.RegisterCallback(_onAvatarLeftClick);
        _avatarRight?.RegisterCallback(_onAvatarRightClick);

        // prevent arrows from blocking clicks when hidden
        if (_avatarLeft != null)
            _avatarLeft.pickingMode = PickingMode.Position;
        if (_avatarRight != null)
            _avatarRight.pickingMode = PickingMode.Position;

        _backButton = screenHost.Q<Button>("Back");
        if (_backButton != null)
            _backButton.clicked += OnBackClicked;

        _classButtons = _classContainer.Query<Button>().ToList().ToArray();
        _specButtons = _specContainer.Query<Button>().ToList().ToArray();

        _createCharacterButton = screenHost.Q<Button>("Create");
        if (_createCharacterButton != null)
            _createCharacterButton.clicked += OnCreateClick;

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

        _statsBinder.Bind(_statsRoot, _freePointsLabel, StartingFreePoints, _screenSwapper);
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
        ClearAllTooltips();

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
            _createCharacterButton.clicked -= OnCreateClick;

        if (_name != null && _onNameFocusOut != null)
            _name.UnregisterCallback(_onNameFocusOut);

        if (_avatarLeft != null && _onAvatarLeftClick != null)
            _avatarLeft.UnregisterCallback(_onAvatarLeftClick);

        if (_avatarRight != null && _onAvatarRightClick != null)
            _avatarRight.UnregisterCallback(_onAvatarRightClick);

        _onAvatarLeftClick = null;
        _onAvatarRightClick = null;

        _avatarImage = null;
        _avatarButtonsRoot = null;
        _avatarLeft = null;
        _avatarRight = null;

        _availableAvatars = null;
        _selectedAvatarId = null;
        _avatarIndex = 0;

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
                ClearTooltip(btn);
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

            SetTooltip(btn, btn, BuildClassTooltip(classDef));
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
                ClearTooltip(btn);
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

            SetTooltip(btn, btn, BuildSpecTooltip(specDef));
        }
    }

    private void OnClassSelected(Button clickedButton, ClassSO classDef)
    {
        _screenSwapper?.HideTooltip();

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
        RebuildAvatarsForClass(classDef.id);
        UpdateCreateButtonState();
    }

    private void OnSpecSelected(Button clickedButton, SpecSO specDef)
    {
        _screenSwapper?.HideTooltip();

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

    // --- Tooltip hover helpers ---

    private void ClearAllTooltips()
    {
        if (_tooltipHandlers == null || _tooltipHandlers.Count == 0)
            return;

        foreach (var kvp in _tooltipHandlers)
        {
            if (kvp.Key == null)
                continue;

            kvp.Key.UnregisterCallback(kvp.Value.enter);
            kvp.Key.UnregisterCallback(kvp.Value.leave);
        }

        _tooltipHandlers.Clear();
    }

    private void SetTooltip(VisualElement element, VisualElement anchor, string text)
    {
        if (element == null)
            return;

        ClearTooltip(element);

        if (string.IsNullOrWhiteSpace(text) || _screenSwapper == null)
            return;

        EventCallback<PointerEnterEvent> onEnter = _ =>
        {
            if (_screenSwapper == null)
                return;

            var resolvedAnchor = anchor ?? element;
            _screenSwapper.ShowTooltipAtElement(
                resolvedAnchor,
                text,
                offsetPx: 8f,
                maxWidth: 380f,
                maxHeight: 9999f
            );
        };

        EventCallback<PointerLeaveEvent> onLeave = _ => _screenSwapper?.HideTooltip();

        element.RegisterCallback(onEnter);
        element.RegisterCallback(onLeave);
        _tooltipHandlers[element] = (onEnter, onLeave);
    }

    private void ClearTooltip(VisualElement element)
    {
        if (element == null)
            return;

        if (_tooltipHandlers.TryGetValue(element, out var handlers))
        {
            element.UnregisterCallback(handlers.enter);
            element.UnregisterCallback(handlers.leave);
            _tooltipHandlers.Remove(element);
        }
    }

    private string BuildClassTooltip(ClassSO classDef)
    {
        if (classDef == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(
            string.IsNullOrWhiteSpace(classDef.displayName) ? classDef.id : classDef.displayName
        );

        AppendBaseStatMods(sb, classDef.baseStatMods);
        AppendDerivedStatMods(sb, classDef.derivedStatMods);

        return sb.ToString().TrimEnd();
    }

    private string BuildSpecTooltip(SpecSO specDef)
    {
        if (specDef == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(
            string.IsNullOrWhiteSpace(specDef.displayName) ? specDef.id : specDef.displayName
        );

        AppendStartingStatsBonus(sb, specDef.statBonus);
        AppendBaseStatMods(sb, specDef.baseStatMods);
        AppendDerivedStatMods(sb, specDef.derivedStatMods);

        return sb.ToString().TrimEnd();
    }

    private static void AppendStartingStatsBonus(StringBuilder sb, Stats stats)
    {
        if (
            stats.strength == 0
            && stats.agility == 0
            && stats.intelligence == 0
            && stats.spirit == 0
            && stats.endurance == 0
        )
            return;

        sb.AppendLine("\nStarting bonus:");
        AppendSignedLine(sb, "Strength", stats.strength);
        AppendSignedLine(sb, "Agility", stats.agility);
        AppendSignedLine(sb, "Intelligence", stats.intelligence);
        AppendSignedLine(sb, "Spirit", stats.spirit);
        AppendSignedLine(sb, "Endurance", stats.endurance);
    }

    private static void AppendBaseStatMods(StringBuilder sb, List<BaseStatModifier> mods)
    {
        if (mods == null || mods.Count == 0)
            return;

        sb.AppendLine("\nBonuses:");
        foreach (var mod in mods)
        {
            sb.Append("- ");
            sb.Append(NiceEnum(mod.stat.ToString()));
            sb.Append(' ');
            sb.AppendLine(FormatModValue(mod.op, mod.value));
        }
    }

    private static void AppendDerivedStatMods(StringBuilder sb, List<DerivedStatModifier> mods)
    {
        if (mods == null || mods.Count == 0)
            return;

        sb.AppendLine("\nModifiers:");
        foreach (var mod in mods)
        {
            sb.Append("- ");
            sb.Append(NiceEnum(mod.stat.ToString()));
            sb.Append(' ');
            sb.AppendLine(FormatModValue(mod.op, mod.value));
        }
    }

    private static void AppendSignedLine(StringBuilder sb, string name, int value)
    {
        if (value == 0)
            return;

        sb.Append("- ").Append(name).Append(' ');
        if (value > 0)
            sb.Append('+');
        sb.AppendLine(value.ToString());
    }

    private static string FormatModValue(ModOp op, float value)
    {
        switch (op)
        {
            case ModOp.Flat:
            {
                // usually whole numbers
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
            if (i > 0 && char.IsUpper(c) && char.IsLower(raw[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }

        return sb.ToString().Replace("Hp", "HP");
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

    private void OnCreateClick()
    {
        var final = _statsBinder.GetFinalStats();
        string nowUtc = DateTime.UtcNow.ToString("O");

        var data = new SaveData
        {
            characterName = _name.value.Trim(),
            classId = _selectedClass.id,
            specId = _selectedSpec.id,
            finalStats = final,
            createdUtc = nowUtc,
            lastSavedUtc = nowUtc,
            avatarId = _selectedAvatarId,
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

        ApplyStarterInventory(data);

        // Start at full vitals based on final stats + starter gear bonuses.
        var derived = PlayerDerivedStatsResolver.BuildEffectiveDerivedStats(data);
        data.currentHp = Mathf.Max(1, derived.maxHp);
        data.currentMana = Mathf.Max(0, derived.maxMana);

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

    private void ApplyStarterInventory(SaveData data)
    {
        if (data == null)
            return;

        // Starter consumables
        AddOrIncrementItem(data, "lesser_health_potion", 3);
        AddOrIncrementItem(data, "lesser_mana_potion", 3);

        // Starter equipment
        GrantAndEquipEquipment(data, "rusty_boots");

        var weaponId = GetStarterWeaponId();
        if (!string.IsNullOrWhiteSpace(weaponId))
            GrantAndEquipEquipment(data, weaponId);
    }

    private string GetStarterWeaponId()
    {
        string classId = _selectedClass != null ? _selectedClass.id : string.Empty;
        string className = _selectedClass != null ? _selectedClass.displayName : string.Empty;
        string specId = _selectedSpec != null ? _selectedSpec.id : string.Empty;
        string specName = _selectedSpec != null ? _selectedSpec.displayName : string.Empty;

        var c = (classId + " " + className).Trim().ToLowerInvariant();
        var s = (specId + " " + specName).Trim().ToLowerInvariant();

        if (c.Contains("warrior"))
            return "rusty_sword";

        // "wand for all mage classes"
        if (c.Contains("mage"))
            return "wooden_wand";

        // Ranger specializations
        if (c.Contains("ranger"))
        {
            if (s.Contains("sharp"))
                return "wooden_bow";

            if (s.Contains("assa"))
                return "rusty_dagger";

            // Fallback for other ranger specs
            return "wooden_bow";
        }

        return null;
    }

    private static void AddOrIncrementItem(SaveData data, string itemId, int quantity)
    {
        if (data == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            return;

        data.items ??= new List<SavedItemStackEntry>();

        for (int i = 0; i < data.items.Count; i++)
        {
            var e = data.items[i];
            if (e == null)
                continue;

            if (string.Equals(e.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                e.quantity += quantity;
                return;
            }
        }

        data.items.Add(new SavedItemStackEntry { itemId = itemId, quantity = quantity });
    }

    private static void GrantAndEquipEquipment(SaveData data, string equipmentId)
    {
        InventoryGrantService.GrantAndEquipEquipment(data, equipmentId);
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

    private void RebuildAvatarsForClass(string classId)
    {
        _selectedAvatarId = null;
        _avatarIndex = 0;

        if (_avatarDb == null)
        {
            SetAvatarUIVisible(false);
            ClearAvatarPreview();
            return;
        }

        _availableAvatars = _avatarDb.GetForClass(classId);

        if (_availableAvatars == null || _availableAvatars.Count == 0)
        {
            SetAvatarUIVisible(false);
            ClearAvatarPreview();
            return;
        }

        SetAvatarUIVisible(true);
        ApplyAvatarFromIndex();
    }

    private void StepAvatar(int delta)
    {
        if (_availableAvatars == null || _availableAvatars.Count == 0)
            return;

        int count = _availableAvatars.Count;

        _avatarIndex = (_avatarIndex + delta) % count;
        if (_avatarIndex < 0)
            _avatarIndex += count;

        ApplyAvatarFromIndex();
    }

    private void ApplyAvatarFromIndex()
    {
        if (_availableAvatars == null)
            return;
        if (_avatarIndex < 0 || _avatarIndex >= _availableAvatars.Count)
            return;

        var entry = _availableAvatars[_avatarIndex];
        _selectedAvatarId = entry.id;

        if (_avatarImage != null)
        {
            _avatarImage.style.backgroundImage =
                entry?.sprite != null ? new StyleBackground(entry.sprite) : StyleKeyword.None;
        }
    }

    private void ClearAvatarPreview()
    {
        if (_avatarImage != null)
            _avatarImage.style.backgroundImage = StyleKeyword.None;
    }

    /// <summary>
    /// Hide arrows without shifting layout (NOT display:none).
    /// </summary>
    private void SetAvatarUIVisible(bool visible)
    {
        if (_avatarButtonsRoot == null)
            return;

        _avatarButtonsRoot.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;

        // Optional: also block input when hidden
        if (_avatarLeft != null)
            _avatarLeft.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        if (_avatarRight != null)
            _avatarRight.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
}
