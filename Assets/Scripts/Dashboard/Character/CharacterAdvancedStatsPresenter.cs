using System.Collections.Generic;
using System.Text;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Inventory;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class CharacterAdvancedStatsPresenter
{
    private ScreenSwapper _swapper;

    private VisualElement _maxHpRow;
    private VisualElement _maxManaRow;
    private VisualElement _attackPowerRow;
    private VisualElement _magicPowerRow;
    private VisualElement _attackSpeedRow;
    private VisualElement _castSpeedRow;
    private VisualElement _physDefRow;
    private VisualElement _magDefRow;
    private VisualElement _evasionRow;
    private VisualElement _accuracyRow;

    private Label _maxHpValue;
    private Label _maxManaValue;
    private Label _attackPowerValue;
    private Label _magicPowerValue;
    private Label _attackSpeedValue;
    private Label _castSpeedValue;
    private Label _physDefValue;
    private Label _magDefValue;
    private Label _evasionValue;
    private Label _accuracyValue;

    private Label _manaRegenValue;
    private Label _hpRegenValue;

    private VisualElement _bonusesRoot;

    private SaveData _lastSave;
    private Stats _lastEffectiveBaseStats;
    private DerivedCombatStats _lastDerived;

    private EventCallback<PointerLeaveEvent> _onLeave;
    private EventCallback<PointerOutEvent> _onOut;

    private EventCallback<PointerEnterEvent> _onEnterMaxHp;
    private EventCallback<PointerEnterEvent> _onEnterMaxMana;
    private EventCallback<PointerEnterEvent> _onEnterAttackPower;
    private EventCallback<PointerEnterEvent> _onEnterMagicPower;
    private EventCallback<PointerEnterEvent> _onEnterAttackSpeed;
    private EventCallback<PointerEnterEvent> _onEnterCastSpeed;
    private EventCallback<PointerEnterEvent> _onEnterPhysDef;
    private EventCallback<PointerEnterEvent> _onEnterMagDef;
    private EventCallback<PointerEnterEvent> _onEnterEvasion;
    private EventCallback<PointerEnterEvent> _onEnterAccuracy;

    public void Bind(VisualElement advancedPanel, ScreenSwapper swapper)
    {
        if (advancedPanel == null)
        {
            Unbind();
            return;
        }

        _swapper = swapper;

        _maxHpRow = advancedPanel.Q<VisualElement>("MaximumHealth");
        _maxManaRow = advancedPanel.Q<VisualElement>("MaximumMana");
        _attackPowerRow = advancedPanel.Q<VisualElement>("AttackPower");
        _magicPowerRow = advancedPanel.Q<VisualElement>("MagicPower");
        _attackSpeedRow = advancedPanel.Q<VisualElement>("AttackSpeed");
        _castSpeedRow = advancedPanel.Q<VisualElement>("CastingSpeed");
        _physDefRow = advancedPanel.Q<VisualElement>("PhysicalDefence");
        _magDefRow = advancedPanel.Q<VisualElement>("MagicalDefence");
        _evasionRow = advancedPanel.Q<VisualElement>("Evasion");
        _accuracyRow = advancedPanel.Q<VisualElement>("Accuracy");

        _maxHpValue = _maxHpRow?.Q<Label>("Value");
        _maxManaValue = _maxManaRow?.Q<Label>("Value");
        _attackPowerValue = _attackPowerRow?.Q<Label>("Value");
        _magicPowerValue = _magicPowerRow?.Q<Label>("Value");
        _attackSpeedValue = _attackSpeedRow?.Q<Label>("Value");
        _castSpeedValue = _castSpeedRow?.Q<Label>("Value");
        _physDefValue = _physDefRow?.Q<Label>("Value");
        _magDefValue = _magDefRow?.Q<Label>("Value");
        _evasionValue = _evasionRow?.Q<Label>("Value");
        _accuracyValue = _accuracyRow?.Q<Label>("Value");

        _manaRegenValue = advancedPanel.Q<VisualElement>("ManaRegen")?.Q<Label>("Value");
        _hpRegenValue = advancedPanel.Q<VisualElement>("HealthRegen")?.Q<Label>("Value");

        _bonusesRoot = advancedPanel.Q<VisualElement>("Bonuses");

        BindTooltips();
    }

    public void Unbind()
    {
        UnbindTooltips();

        _swapper = null;

        _maxHpRow = null;
        _maxManaRow = null;
        _attackPowerRow = null;
        _magicPowerRow = null;
        _attackSpeedRow = null;
        _castSpeedRow = null;
        _physDefRow = null;
        _magDefRow = null;
        _evasionRow = null;
        _accuracyRow = null;

        _maxHpValue = null;
        _maxManaValue = null;
        _attackPowerValue = null;
        _magicPowerValue = null;
        _attackSpeedValue = null;
        _castSpeedValue = null;
        _physDefValue = null;
        _magDefValue = null;
        _evasionValue = null;
        _accuracyValue = null;
        _manaRegenValue = null;
        _hpRegenValue = null;
        _bonusesRoot = null;

        _lastSave = null;
        _lastEffectiveBaseStats = default;
        _lastDerived = default;
    }

    public void RefreshAdvancedStats(
        SaveData save,
        DerivedCombatStats derived,
        Stats effectiveBaseStats
    )
    {
        if (save == null)
            return;

        _lastSave = save;
        _lastDerived = derived;
        _lastEffectiveBaseStats = effectiveBaseStats;

        SetLabelInt(_maxHpValue, derived.maxHp);
        SetLabelInt(_maxManaValue, derived.maxMana);
        SetLabelInt(_attackPowerValue, derived.attackPower);
        SetLabelInt(_magicPowerValue, derived.magicPower);
        SetLabelInt(_attackSpeedValue, derived.attackSpeed);
        SetLabelInt(_castSpeedValue, derived.castSpeed);
        SetLabelInt(_physDefValue, derived.physicalDefense);
        SetLabelInt(_magDefValue, derived.magicalDefense);
        SetLabelInt(_evasionValue, derived.evasion);
        SetLabelInt(_accuracyValue, derived.accuracy);

        int hpRegen10 = CombatStatCalculator.CalculateOutOfCombatHpRegenPer10s(
            derived.maxHp,
            effectiveBaseStats,
            save.level,
            save.tier
        );

        int manaRegen10 = CombatStatCalculator.CalculateOutOfCombatManaRegenPer10s(
            derived.maxMana,
            effectiveBaseStats,
            save.level,
            save.tier
        );

        SetLabelInt(_hpRegenValue, hpRegen10);
        SetLabelInt(_manaRegenValue, manaRegen10);
    }

    public void RefreshCombatBonuses(PlayerEquipment equipment, DerivedCombatStats derived)
    {
        if (_bonusesRoot == null)
            return;

        _bonusesRoot.Clear();

        var title = new Label("Combat Bonuses") { pickingMode = PickingMode.Ignore };
        title.AddToClassList("header-xs");
        title.style.unityTextAlign = TextAnchor.UpperCenter;
        title.style.marginBottom = 8;
        _bonusesRoot.Add(title);

        // Dynamic combat bonuses from equipped rolls (damage overrides + other combat modifiers).
        List<string> lines = EquipmentBonusLinesBuilder.BuildCombatBonusLines(equipment);
        if (lines.Count == 0)
            lines.Add("(none)");

        for (int i = 0; i < lines.Count; i++)
        {
            var l = new Label(lines[i]) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("label-sm");
            l.style.whiteSpace = WhiteSpace.Normal;
            _bonusesRoot.Add(l);
        }
    }

    private void BindTooltips()
    {
        if (_swapper == null)
            return;

        _onLeave = _ => _swapper?.HideTooltip();
        _onOut = _ => _swapper?.HideTooltip();

        _onEnterMaxHp = _ => ShowTooltip(_maxHpRow, BuildMaxHpTooltip());
        _onEnterMaxMana = _ => ShowTooltip(_maxManaRow, BuildMaxManaTooltip());
        _onEnterAttackPower = _ => ShowTooltip(_attackPowerRow, BuildAttackPowerTooltip());
        _onEnterMagicPower = _ => ShowTooltip(_magicPowerRow, BuildMagicPowerTooltip());
        _onEnterAttackSpeed = _ => ShowTooltip(_attackSpeedRow, BuildAttackSpeedTooltip());
        _onEnterCastSpeed = _ => ShowTooltip(_castSpeedRow, BuildCastSpeedTooltip());
        _onEnterPhysDef = _ => ShowTooltip(_physDefRow, BuildPhysDefTooltip());
        _onEnterMagDef = _ => ShowTooltip(_magDefRow, BuildMagDefTooltip());
        _onEnterEvasion = _ => ShowTooltip(_evasionRow, BuildEvasionTooltip());
        _onEnterAccuracy = _ => ShowTooltip(_accuracyRow, BuildAccuracyTooltip());

        RegisterTooltip(_maxHpRow, _onEnterMaxHp);
        RegisterTooltip(_maxManaRow, _onEnterMaxMana);
        RegisterTooltip(_attackPowerRow, _onEnterAttackPower);
        RegisterTooltip(_magicPowerRow, _onEnterMagicPower);
        RegisterTooltip(_attackSpeedRow, _onEnterAttackSpeed);
        RegisterTooltip(_castSpeedRow, _onEnterCastSpeed);
        RegisterTooltip(_physDefRow, _onEnterPhysDef);
        RegisterTooltip(_magDefRow, _onEnterMagDef);
        RegisterTooltip(_evasionRow, _onEnterEvasion);
        RegisterTooltip(_accuracyRow, _onEnterAccuracy);
    }

    private void UnbindTooltips()
    {
        UnregisterTooltip(_maxHpRow, _onEnterMaxHp);
        UnregisterTooltip(_maxManaRow, _onEnterMaxMana);
        UnregisterTooltip(_attackPowerRow, _onEnterAttackPower);
        UnregisterTooltip(_magicPowerRow, _onEnterMagicPower);
        UnregisterTooltip(_attackSpeedRow, _onEnterAttackSpeed);
        UnregisterTooltip(_castSpeedRow, _onEnterCastSpeed);
        UnregisterTooltip(_physDefRow, _onEnterPhysDef);
        UnregisterTooltip(_magDefRow, _onEnterMagDef);
        UnregisterTooltip(_evasionRow, _onEnterEvasion);
        UnregisterTooltip(_accuracyRow, _onEnterAccuracy);

        _onLeave = null;
        _onOut = null;

        _onEnterMaxHp = null;
        _onEnterMaxMana = null;
        _onEnterAttackPower = null;
        _onEnterMagicPower = null;
        _onEnterAttackSpeed = null;
        _onEnterCastSpeed = null;
        _onEnterPhysDef = null;
        _onEnterMagDef = null;
        _onEnterEvasion = null;
        _onEnterAccuracy = null;
    }

    private void RegisterTooltip(VisualElement row, EventCallback<PointerEnterEvent> onEnter)
    {
        if (row == null || onEnter == null)
            return;

        row.pickingMode = PickingMode.Position;
        row.RegisterCallback(onEnter);
        if (_onLeave != null)
            row.RegisterCallback(_onLeave);
        if (_onOut != null)
            row.RegisterCallback(_onOut);
    }

    private void UnregisterTooltip(VisualElement row, EventCallback<PointerEnterEvent> onEnter)
    {
        if (row == null)
            return;

        if (onEnter != null)
            row.UnregisterCallback(onEnter);
        if (_onLeave != null)
            row.UnregisterCallback(_onLeave);
        if (_onOut != null)
            row.UnregisterCallback(_onOut);
    }

    private void ShowTooltip(VisualElement anchor, string text)
    {
        if (_swapper == null || anchor == null)
            return;
        if (string.IsNullOrWhiteSpace(text))
            return;

        _swapper.ShowTooltipAtElement(anchor, text, offsetPx: 10f, maxWidth: 420f);
    }

    private string BuildMaxHpTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Maximum Health");
        sb.AppendLine($"Shown: {_lastDerived.maxHp}");
        sb.AppendLine();
        sb.AppendLine(
            "Calculated from effective base stats (includes class/spec + equipped base-stat rolls):"
        );
        sb.AppendLine(
            $"END={_lastEffectiveBaseStats.endurance}, STR={_lastEffectiveBaseStats.strength}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Formula:");
        sb.AppendLine("50 + END*12 + STR*1 + Level*10 + TierBonus(25 per tier)");
        return sb.ToString().TrimEnd();
    }

    private string BuildMaxManaTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Maximum Mana");
        sb.AppendLine($"Shown: {_lastDerived.maxMana}");
        sb.AppendLine();
        sb.AppendLine(
            "Calculated from effective base stats (includes class/spec + equipped base-stat rolls):"
        );
        sb.AppendLine(
            $"INT={_lastEffectiveBaseStats.intelligence}, SPI={_lastEffectiveBaseStats.spirit}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Formula:");
        sb.AppendLine("20 + INT*8 + SPI*8 + Level*6 + TierBonus(15 per tier)");
        return sb.ToString().TrimEnd();
    }

    private string BuildAttackPowerTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Attack Power");
        sb.AppendLine($"Shown: {_lastDerived.attackPower}");
        sb.AppendLine();
        sb.AppendLine(
            "Calculated from effective base stats (includes class/spec + equipped base-stat rolls):"
        );
        sb.AppendLine(
            $"STR={_lastEffectiveBaseStats.strength}, AGI={_lastEffectiveBaseStats.agility}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Notes:");
        sb.AppendLine("- This is the derived stat value.");
        sb.AppendLine(
            "- Combat modifiers like +Attack Power flat/% affect spells/attacks at runtime and are listed under Combat Bonuses."
        );
        return sb.ToString().TrimEnd();
    }

    private string BuildMagicPowerTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Magic Power");
        sb.AppendLine($"Shown: {_lastDerived.magicPower}");
        sb.AppendLine();
        sb.AppendLine(
            "Calculated from effective base stats (includes class/spec + equipped base-stat rolls):"
        );
        sb.AppendLine(
            $"INT={_lastEffectiveBaseStats.intelligence}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Notes:");
        sb.AppendLine("- This is the derived stat value.");
        sb.AppendLine(
            "- Combat modifiers like +Magic Power flat/% affect spell damage at runtime and are listed under Combat Bonuses."
        );
        return sb.ToString().TrimEnd();
    }

    private string BuildAttackSpeedTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Attack Speed");
        sb.AppendLine($"Shown: {_lastDerived.attackSpeed}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"AGI={_lastEffectiveBaseStats.agility}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine(
            "Combat modifiers (flat/% attack speed) apply at runtime; see Combat Bonuses."
        );
        return sb.ToString().TrimEnd();
    }

    private string BuildCastSpeedTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Casting Speed");
        sb.AppendLine($"Shown: {_lastDerived.castSpeed}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"AGI={_lastEffectiveBaseStats.agility}, INT={_lastEffectiveBaseStats.intelligence}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine(
            "Combat modifiers (flat/% casting speed) apply at runtime; see Combat Bonuses."
        );
        return sb.ToString().TrimEnd();
    }

    private string BuildPhysDefTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Physical Defence");
        sb.AppendLine($"Shown: {_lastDerived.physicalDefense}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"END={_lastEffectiveBaseStats.endurance}, STR={_lastEffectiveBaseStats.strength}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Combat defence flats/% apply at runtime; see Combat Bonuses.");
        return sb.ToString().TrimEnd();
    }

    private string BuildMagDefTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Magic Defence");
        sb.AppendLine($"Shown: {_lastDerived.magicalDefense}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"END={_lastEffectiveBaseStats.endurance}, INT={_lastEffectiveBaseStats.intelligence}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        sb.AppendLine();
        sb.AppendLine("Combat defence flats/% apply at runtime; see Combat Bonuses.");
        return sb.ToString().TrimEnd();
    }

    private string BuildEvasionTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Evasion");
        sb.AppendLine($"Shown: {_lastDerived.evasion}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"AGI={_lastEffectiveBaseStats.agility}, SPI={_lastEffectiveBaseStats.spirit}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        return sb.ToString().TrimEnd();
    }

    private string BuildAccuracyTooltip()
    {
        if (_lastSave == null)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Accuracy");
        sb.AppendLine($"Shown: {_lastDerived.accuracy}");
        sb.AppendLine();
        sb.AppendLine("Calculated from effective base stats:");
        sb.AppendLine(
            $"SPI={_lastEffectiveBaseStats.spirit}, Level={_lastSave.level}, Tier={_lastSave.tier}"
        );
        return sb.ToString().TrimEnd();
    }

    private static void SetLabelInt(Label label, int value)
    {
        if (label == null)
            return;

        label.text = value.ToString();
    }
}
