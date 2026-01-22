using System;
using System.Collections.Generic;
using System.Text;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Inventory;
using MyGame.Progression;
using MyGame.Run;
using MyGame.Save;
using MyGame.UI; // PixelProgressBar
using UnityEngine;
using UnityEngine.UIElements;

public sealed class CharacterSectionController
{
    private const string DefaultUnknownName = "Unknown";

    private VisualElement _root;
    private ScreenSwapper _swapper;

    // UI fields inside CharacterPanel
    private Label _charNameLabel; // name="CharName"
    private Label _levelLabel; // name="Level"
    private Label _tierLabel; // name="Tier" (exists in your UXML)
    private PixelProgressBar _expBar; // name="ExpBar"
    private PixelProgressBar _healthBar; // name="CurrentHp"
    private PixelProgressBar _manaBar; // name="CurrentMana"
    private Button _addPointsButton; // name="AddPoints"

    // Class/spec bonus icons (BasicInfo/Other/ClassSpecBonuses)
    private VisualElement _classIcon;
    private VisualElement _specIcon;

    private EventCallback<PointerEnterEvent> _onClassIconEnter;
    private EventCallback<PointerEnterEvent> _onSpecIconEnter;
    private EventCallback<PointerLeaveEvent> _onIconLeave;
    private EventCallback<PointerOutEvent> _onIconOut;

    private DerivedCombatStats _combatStats;
    private SaveData _saveData;

    // StatsPanel binder
    private readonly StatsPanelBinder _statsBinder = new();

    private readonly CharacterAdvancedStatsPresenter _advancedStatsPresenter = new();

    private PlayerEquipment _cachedEquipment;
    private ClassSO _cachedClassSo;
    private SpecSO _cachedSpecSo;

    public void Bind(VisualElement characterPanelRoot, ScreenSwapper swapper = null)
    {
        _root = characterPanelRoot;
        _swapper = swapper;

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

        // Query UI
        _charNameLabel = _root.Q<Label>("CharName");
        _levelLabel = _root.Q<Label>("Level");
        _tierLabel = _root.Q<Label>("Tier");
        _expBar = _root.Q<PixelProgressBar>("ExpBar");
        _healthBar = _root.Q<PixelProgressBar>("CurrentHp");
        _manaBar = _root.Q<PixelProgressBar>("CurrentMana");
        _addPointsButton = _root.Q<Button>("AddPoints");

        _classIcon = _root.Q<VisualElement>("ClassIcon");
        _specIcon = _root.Q<VisualElement>("SpecIcon");

        BindClassSpecIconTooltips();

        if (_addPointsButton == null)
        {
            Debug.LogError("CharacterSectionController: Could not find Button named 'AddPoints'.");
            return;
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
        _statsBinder.Bind(statsPanelRoot, pointsLabel, startingFreePoints: 0, swapper: _swapper);
        _statsBinder.Changed += OnBaseStatsChanged;

        // Advanced Stats root (yours is duplicated, so grab the outer one then find the inner)
        var advancedPanel = _root.Q<VisualElement>("AdvancedStats");
        if (advancedPanel == null)
        {
            Debug.LogError("Could not find AdvancedStats root.");
            return;
        }

        _advancedStatsPresenter.Bind(advancedPanel, _swapper);

        // Populate everything from the loaded save
        RefreshFromSave();
        UpdateAddPointsButtonState();
    }

    public void Unbind()
    {
        if (_addPointsButton != null)
            _addPointsButton.clicked -= OnAddPointsClicked;

        UnbindClassSpecIconTooltips();

        _statsBinder.Changed -= OnBaseStatsChanged;
        _statsBinder.Unbind();

        _root = null;
        _charNameLabel = null;
        _levelLabel = null;
        _tierLabel = null;
        _expBar = null;
        _healthBar = null;
        _manaBar = null;
        _addPointsButton = null;

        _classIcon = null;
        _specIcon = null;
        _swapper = null;

        _advancedStatsPresenter.Unbind();
    }

    private void OnBaseStatsChanged()
    {
        UpdateAddPointsButtonState();
        RefreshBaseStatBonusLabels();
    }

    public void RefreshFromSave()
    {
        if (!SaveSession.HasSave)
            return;

        var save = SaveSession.Current;

        // Always keep a current pointer
        _saveData = save;

        // Name
        if (_charNameLabel != null)
        {
            _charNameLabel.text = string.IsNullOrWhiteSpace(save.characterName)
                ? DefaultUnknownName
                : save.characterName;
        }

        // Level label
        if (_levelLabel != null)
        {
            int level = Mathf.Max(1, save.level);
            _levelLabel.text = $"Level {level}";
        }
        // Tier label
        if (_tierLabel != null)
        {
            string tier = HelperFunctions.ToTierRoman(save.tier);
            _tierLabel.text = $"{tier}";
        }

        // EXP bar
        if (_expBar != null)
        {
            float max = Mathf.Max(1, PlayerLevelUp.GetXpRequiredForLevel(save.level));
            float val = Mathf.Clamp(save.exp, 0, max);

            _expBar.SetRange(0, max);
            _expBar.SetValue(val);
        }

        // Load equipment once; effective base stats (class/spec + equipped base-stat rolls)
        // should drive derived calculations.
        var equipment = RunSession.Equipment ?? InventorySaveMapper.LoadEquipmentFromSave(save);
        _cachedEquipment = equipment;

        // While the user is allocating points, the binder's numbers are the preview base stats.
        // When refreshing from save, we want the committed save stats.
        var effectiveBaseStats = PlayerBaseStatsResolver.BuildEffectiveBaseStats(save, equipment);

        // ✅ Derived combat stats should be recalculated every refresh
        var derived = CombatStatCalculator.CalculateAll(effectiveBaseStats, save.level, save.tier);

        // Apply class/spec derived modifiers so the panel matches combat.
        var classDb =
            GameConfigProvider.Instance != null
                ? GameConfigProvider.Instance.PlayerClassDatabase
                : null;

        ClassSO classSo = null;
        SpecSO specSo = null;

        if (classDb != null)
        {
            classSo = classDb.GetClass(save.classId);
            if (classSo != null)
                DerivedModifierApplier.ApplyAll(ref derived, classSo.derivedStatMods);

            specSo = classDb.GetSpec(save.specId);
            if (specSo != null)
                DerivedModifierApplier.ApplyAll(ref derived, specSo.derivedStatMods);
        }

        _cachedClassSo = classSo;
        _cachedSpecSo = specSo;

        // Apply equipped rolled derived modifiers.
        if (equipment != null)
        {
            foreach (var inst in equipment.GetEquippedInstances())
            {
                if (inst?.rolledDerivedStatMods == null || inst.rolledDerivedStatMods.Count == 0)
                    continue;
                DerivedModifierApplier.ApplyAll(ref derived, inst.rolledDerivedStatMods);
            }
        }

        RefreshClassSpecIcons(classSo, specSo);

        // HP bar
        if (_healthBar != null)
        {
            int maxHp = Mathf.Max(1, derived.maxHp);
            int val = Mathf.Clamp(save.currentHp, 0, maxHp);

            _healthBar.SetRange(0, maxHp);
            _healthBar.SetValue(val);
        }

        // Mana bar
        if (_manaBar != null)
        {
            int maxMana = Mathf.Max(0, derived.maxMana);
            int val = Mathf.Clamp(save.currentMana, 0, maxMana);

            _manaBar.SetRange(0, maxMana);
            _manaBar.SetValue(val);
        }

        // Base stats + free points
        _statsBinder.SetBaseStats(
            save.finalStats,
            startingFreePointsOverride: Mathf.Max(0, save.unspentStatPoints)
        );

        RefreshBaseStatBonusLabels();

        // ✅ Advanced stats section
        _advancedStatsPresenter.RefreshAdvancedStats(save, derived, effectiveBaseStats);
        _advancedStatsPresenter.RefreshCombatBonuses(equipment, derived);

        UpdateAddPointsButtonState();
    }

    private void RefreshBaseStatBonusLabels()
    {
        // Show class/spec + equipped base-stat bonuses in the stats panel.
        // Percent bonuses are calculated from the current base values (including allocated points preview).
        var baseForPct = _statsBinder.GetFinalStats();

        int flatStr = 0,
            flatAgi = 0,
            flatInt = 0,
            flatEnd = 0,
            flatSpr = 0;
        float pctStr = 0f,
            pctAgi = 0f,
            pctInt = 0f,
            pctEnd = 0f,
            pctSpr = 0f;

        AccumulateBaseMods(
            _cachedClassSo?.baseStatMods,
            ref flatStr,
            ref flatAgi,
            ref flatInt,
            ref flatEnd,
            ref flatSpr,
            ref pctStr,
            ref pctAgi,
            ref pctInt,
            ref pctEnd,
            ref pctSpr
        );
        AccumulateBaseMods(
            _cachedSpecSo?.baseStatMods,
            ref flatStr,
            ref flatAgi,
            ref flatInt,
            ref flatEnd,
            ref flatSpr,
            ref pctStr,
            ref pctAgi,
            ref pctInt,
            ref pctEnd,
            ref pctSpr
        );

        if (_cachedEquipment != null)
        {
            foreach (var inst in _cachedEquipment.GetEquippedInstances())
            {
                if (inst?.rolledBaseStatMods == null || inst.rolledBaseStatMods.Count == 0)
                    continue;
                AccumulateBaseMods(
                    inst.rolledBaseStatMods,
                    ref flatStr,
                    ref flatAgi,
                    ref flatInt,
                    ref flatEnd,
                    ref flatSpr,
                    ref pctStr,
                    ref pctAgi,
                    ref pctInt,
                    ref pctEnd,
                    ref pctSpr
                );
            }
        }

        _statsBinder.SetBonusText(
            StatsPanelBinder.StatId.Str,
            FormatBaseBonus(flatStr, pctStr, baseForPct.strength)
        );
        _statsBinder.SetBonusText(
            StatsPanelBinder.StatId.Agi,
            FormatBaseBonus(flatAgi, pctAgi, baseForPct.agility)
        );
        _statsBinder.SetBonusText(
            StatsPanelBinder.StatId.Int,
            FormatBaseBonus(flatInt, pctInt, baseForPct.intelligence)
        );
        _statsBinder.SetBonusText(
            StatsPanelBinder.StatId.End,
            FormatBaseBonus(flatEnd, pctEnd, baseForPct.endurance)
        );
        _statsBinder.SetBonusText(
            StatsPanelBinder.StatId.Spr,
            FormatBaseBonus(flatSpr, pctSpr, baseForPct.spirit)
        );
    }

    private static void AccumulateBaseMods(
        IList<BaseStatModifier> mods,
        ref int flatStr,
        ref int flatAgi,
        ref int flatInt,
        ref int flatEnd,
        ref int flatSpr,
        ref float pctStr,
        ref float pctAgi,
        ref float pctInt,
        ref float pctEnd,
        ref float pctSpr
    )
    {
        if (mods == null)
            return;

        for (int i = 0; i < mods.Count; i++)
        {
            var m = mods[i];
            switch (m.stat)
            {
                case BaseStatType.Strength:
                    if (m.op == ModOp.Flat)
                        flatStr += Mathf.RoundToInt(m.value);
                    else if (m.op == ModOp.Percent)
                        pctStr += m.value;
                    break;
                case BaseStatType.Agility:
                    if (m.op == ModOp.Flat)
                        flatAgi += Mathf.RoundToInt(m.value);
                    else if (m.op == ModOp.Percent)
                        pctAgi += m.value;
                    break;
                case BaseStatType.Intelligence:
                    if (m.op == ModOp.Flat)
                        flatInt += Mathf.RoundToInt(m.value);
                    else if (m.op == ModOp.Percent)
                        pctInt += m.value;
                    break;
                case BaseStatType.Endurance:
                    if (m.op == ModOp.Flat)
                        flatEnd += Mathf.RoundToInt(m.value);
                    else if (m.op == ModOp.Percent)
                        pctEnd += m.value;
                    break;
                case BaseStatType.Spirit:
                    if (m.op == ModOp.Flat)
                        flatSpr += Mathf.RoundToInt(m.value);
                    else if (m.op == ModOp.Percent)
                        pctSpr += m.value;
                    break;
            }
        }
    }

    private static string FormatBaseBonus(int flat, float pct, int baseValue)
    {
        int pctFromBase = Mathf.RoundToInt(baseValue * (pct / 100f));
        int totalBonus = flat + pctFromBase;

        if (totalBonus == 0)
            return string.Empty;

        return totalBonus > 0 ? $"+{totalBonus}" : totalBonus.ToString();
    }

    private void RefreshClassSpecIcons(ClassSO classSo, SpecSO specSo)
    {
        if (_classIcon != null)
        {
            _classIcon.style.backgroundImage =
                classSo != null && classSo.icon != null
                    ? new StyleBackground(classSo.icon)
                    : StyleKeyword.None;
        }

        if (_specIcon != null)
        {
            _specIcon.style.backgroundImage =
                specSo != null && specSo.icon != null
                    ? new StyleBackground(specSo.icon)
                    : StyleKeyword.None;
        }
    }

    private void BindClassSpecIconTooltips()
    {
        if (_swapper == null)
            return;

        if (_classIcon != null)
            _classIcon.pickingMode = PickingMode.Position;
        if (_specIcon != null)
            _specIcon.pickingMode = PickingMode.Position;

        _onIconLeave = _ => _swapper?.HideTooltip();
        _onIconOut = _ => _swapper?.HideTooltip();

        _onClassIconEnter = evt =>
        {
            if (!TryGetCurrentClassSpec(out var classSo, out var _unusedSpecSo))
                return;
            _swapper?.ShowTooltipAtElement(
                _classIcon,
                BuildClassTooltip(classSo),
                offsetPx: 10f,
                maxWidth: 380f
            );
        };

        _onSpecIconEnter = evt =>
        {
            if (!TryGetCurrentClassSpec(out var _unusedClassSo, out var specSo))
                return;
            _swapper?.ShowTooltipAtElement(
                _specIcon,
                BuildSpecTooltip(specSo),
                offsetPx: 10f,
                maxWidth: 380f
            );
        };

        if (_classIcon != null)
        {
            _classIcon.RegisterCallback(_onClassIconEnter);
            _classIcon.RegisterCallback(_onIconLeave);
            _classIcon.RegisterCallback(_onIconOut);
        }

        if (_specIcon != null)
        {
            _specIcon.RegisterCallback(_onSpecIconEnter);
            _specIcon.RegisterCallback(_onIconLeave);
            _specIcon.RegisterCallback(_onIconOut);
        }
    }

    private void UnbindClassSpecIconTooltips()
    {
        if (_classIcon != null)
        {
            if (_onClassIconEnter != null)
                _classIcon.UnregisterCallback(_onClassIconEnter);
            if (_onIconLeave != null)
                _classIcon.UnregisterCallback(_onIconLeave);
            if (_onIconOut != null)
                _classIcon.UnregisterCallback(_onIconOut);
        }

        if (_specIcon != null)
        {
            if (_onSpecIconEnter != null)
                _specIcon.UnregisterCallback(_onSpecIconEnter);
            if (_onIconLeave != null)
                _specIcon.UnregisterCallback(_onIconLeave);
            if (_onIconOut != null)
                _specIcon.UnregisterCallback(_onIconOut);
        }

        _onClassIconEnter = null;
        _onSpecIconEnter = null;
        _onIconLeave = null;
        _onIconOut = null;
    }

    private bool TryGetCurrentClassSpec(out ClassSO classSo, out SpecSO specSo)
    {
        classSo = null;
        specSo = null;

        if (!SaveSession.HasSave)
            return false;

        var classDb =
            GameConfigProvider.Instance != null
                ? GameConfigProvider.Instance.PlayerClassDatabase
                : null;
        if (classDb == null)
            return false;

        var save = SaveSession.Current;
        classSo = classDb.GetClass(save.classId);
        specSo = classDb.GetSpec(save.specId);
        return classSo != null || specSo != null;
    }

    private static string BuildClassTooltip(ClassSO classDef)
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

    private static string BuildSpecTooltip(SpecSO specDef)
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
            sb.Append(CharacterDashboardText.NiceEnum(mod.stat.ToString()));
            sb.Append(' ');
            sb.AppendLine(CharacterDashboardText.FormatModValue(mod.op, mod.value));
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
            sb.Append(CharacterDashboardText.NiceEnum(mod.stat.ToString()));
            sb.Append(' ');
            sb.AppendLine(CharacterDashboardText.FormatModValue(mod.op, mod.value));
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

        // Recalculate everything from the committed save (derived + advanced stats).
        RefreshFromSave();
    }

    private void UpdateAddPointsButtonState()
    {
        if (_addPointsButton == null)
            return;

        // Enabled only when the player has allocated something
        _addPointsButton.SetEnabled(_statsBinder.GetAllocatedPointsTotal() > 0);
    }

    public void RefreshVitalsOnly()
    {
        if (!SaveSession.HasSave)
            return;

        var save = SaveSession.Current;

        // Recalculate max values from current save (including ALL bonuses)
        // so bars stay consistent with the derived stats shown elsewhere and in combat.
        var derived = PlayerDerivedStatsResolver.BuildEffectiveDerivedStats(save);
        int maxHp = Mathf.Max(1, derived.maxHp);
        int maxMana = Mathf.Max(0, derived.maxMana);

        if (_healthBar != null)
        {
            _healthBar.SetRange(0, maxHp);
            _healthBar.SetValue(Mathf.Clamp(save.currentHp, 0, maxHp));
        }

        if (_manaBar != null)
        {
            _manaBar.SetRange(0, maxMana);
            _manaBar.SetValue(Mathf.Clamp(save.currentMana, 0, maxMana));
        }
    }
}
