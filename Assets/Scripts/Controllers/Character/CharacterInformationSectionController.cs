using MyGame.Combat;
using MyGame.Progression;
using MyGame.Save;
using MyGame.UI; // PixelProgressBar
using UnityEngine;
using UnityEngine.UIElements;

public sealed class CharacterSectionController
{
    private const string DefaultUnknownName = "Unknown";

    private VisualElement _root;

    // UI fields inside CharacterPanel
    private Label _charNameLabel; // name="CharName"
    private Label _levelLabel; // name="Level"
    private PixelProgressBar _expBar; // name="ExpBar"
    private PixelProgressBar _healthBar; // name="ExpBar"
    private PixelProgressBar _manaBar; // name="ExpBar"
    private Button _addPointsButton; // name="AddPoints"
    private DerivedCombatStats _combatStats;
    private SaveData _saveData;

    // StatsPanel binder
    private readonly StatsPanelBinder _statsBinder = new();

    public void Bind(VisualElement characterPanelRoot)
    {
        _root = characterPanelRoot;

        if (_root == null)
        {
            Debug.LogError("CharacterSectionController.Bind: characterPanelRoot is null.");
            return;
        }

        if (!SaveSession.HasSave)
        {
            Debug.LogError("CharacterSectionController.Bind: No save loaded. Load a slot first.");
            return;
        }

        _saveData = SaveSession.Current;
        _combatStats = CombatStatCalculator.CalculateAll(
            _saveData.finalStats,
            _saveData.level,
            _saveData.tier
        );

        // Query UI
        _charNameLabel = _root.Q<Label>("CharName");
        _levelLabel = _root.Q<Label>("Level");
        _expBar = _root.Q<PixelProgressBar>("ExpBar");
        _healthBar = _root.Q<PixelProgressBar>("CurrentHp");
        _manaBar = _root.Q<PixelProgressBar>("CurrentMana");
        _addPointsButton = _root.Q<Button>("AddPoints");

        if (_addPointsButton == null)
        {
            Debug.LogError("CharacterSectionController: Could not find Button named 'AddPoints'.");
            return;
        }

        // Equipment aspect ratio lock (your existing logic)
        var equipment = _root.Q<VisualElement>("Equipment");
        if (equipment != null)
        {
            UIAspectRatioHelper.LockWidthToHeight(
                target: equipment,
                layoutSource: _root,
                widthOverHeight: 303f / 361f
            );
        }

        // Bind Add Points
        _addPointsButton.clicked += OnAddPointsClicked;
        _addPointsButton.SetEnabled(false); // disabled by default

        // Bind StatsPanel (template instance)
        var statsHost = _root.Q<VisualElement>("Stats");
        var statsPanelRoot = statsHost?.Q<VisualElement>("StatsPanel");
        var pointsLabel = statsPanelRoot?.Q<Label>("Points");

        if (statsHost == null || statsPanelRoot == null || pointsLabel == null)
        {
            Debug.LogError(
                "CharacterSectionController: Stats panel elements not found (Stats/StatsPanel/Points)."
            );
            return;
        }

        // Bind binder (we will set actual save values below in RefreshFromSave)
        _statsBinder.Bind(statsPanelRoot, pointsLabel, startingFreePoints: 0);
        _statsBinder.Changed += UpdateAddPointsButtonState;

        // Populate everything from the loaded save
        RefreshFromSave();

        UpdateAddPointsButtonState();
    }

    public void Unbind()
    {
        if (_addPointsButton != null)
            _addPointsButton.clicked -= OnAddPointsClicked;

        _statsBinder.Changed -= UpdateAddPointsButtonState;
        _statsBinder.Unbind();

        _root = null;
        _charNameLabel = null;
        _levelLabel = null;
        _expBar = null;
        _addPointsButton = null;
    }

    /// <summary>
    /// Reads SaveSession.Current and updates Character panel UI.
    /// Call after gaining XP, changing name, etc.
    /// </summary>
    public void RefreshFromSave()
    {
        if (!SaveSession.HasSave)
            return;

        var save = SaveSession.Current;

        // Name
        if (_charNameLabel != null)
        {
            _charNameLabel.text = string.IsNullOrWhiteSpace(save.characterName)
                ? DefaultUnknownName
                : save.characterName;
        }

        // Level
        if (_levelLabel != null)
        {
            int level = Mathf.Max(1, save.level);
            _levelLabel.text = $"Level {level}";
        }

        // EXP bar
        if (_expBar != null)
        {
            // keep values clamped/sane
            int max = Mathf.Max(1, PlayerLevelUp.GetXpRequiredForLevel(save.level));
            int val = Mathf.Clamp(save.exp, 0, max);

            _expBar.SetRange(0, max);
            _expBar.SetValue(val);
        }

        //HpBar
        if (_healthBar != null)
        {
            // keep values clamped/sane
            int max = Mathf.Max(1, _combatStats.maxHp);
            int val = Mathf.Clamp(_saveData.currentHp, 1, max);
            _healthBar.SetRange(0, max);
            _healthBar.SetValue(val);
        }

        //Mana Bar
        if (_manaBar != null)
        {
            // keep values clamped/sane
            int max = Mathf.Max(1, _combatStats.maxMana);
            int val = Mathf.Clamp(_saveData.currentMana, 1, max);

            _manaBar.SetRange(0, max);
            _manaBar.SetValue(val);
        }

        // Stats + free points
        _statsBinder.SetBaseStats(
            save.finalStats,
            startingFreePointsOverride: Mathf.Max(0, save.unspentStatPoints)
        );

        // Button state depends on allocations (Added > 0)
        UpdateAddPointsButtonState();
    }

    private void OnAddPointsClicked()
    {
        int spent = _statsBinder.GetAllocatedPointsTotal();
        if (spent <= 0)
            return;

        // Commit to save
        SaveSession.Current.finalStats = _statsBinder.GetFinalStats();
        SaveSession.Current.unspentStatPoints = _statsBinder.FreePoints;

        // Persist into the loaded slot immediately
        SaveSession.SaveNow();

        // Reset allocations: final becomes the new base; remaining points stay
        _statsBinder.SetBaseStats(
            SaveSession.Current.finalStats,
            startingFreePointsOverride: SaveSession.Current.unspentStatPoints
        );

        UpdateAddPointsButtonState();
    }

    private void UpdateAddPointsButtonState()
    {
        if (_addPointsButton == null)
            return;

        // Enabled only when the player has allocated something
        _addPointsButton.SetEnabled(_statsBinder.GetAllocatedPointsTotal() > 0);
    }
}
