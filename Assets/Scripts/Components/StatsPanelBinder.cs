using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable controller for the StatsPanel UXML template.
/// - Binds to a root VisualElement containing the StatsPanel markup.
/// - Manages free points, +/-, rendering, and final stats calculation.
/// </summary>
public sealed class StatsPanelBinder
{
    // Matches your UXML row names: "Str", "Agi", "Int", "End", "Spr"
    public enum StatId
    {
        Str,
        Agi,
        Int,
        End,
        Spr,
    }

    private class StatRow
    {
        public StatId Id;
        public Button Minus;
        public Button Plus;
        public Label BaseValueLabel; // "Value"
        public Label AddedValueLabel; // "AddedValue"
        public Label BonusValueLabel; // "BonusValue" (optional)
        public int BaseValue;
        public int Added;
        public string BonusText;
        public EventCallback<PointerEnterEvent> OnPlusEnter;
        public EventCallback<PointerEnterEvent> OnMinusEnter;
    }

    private VisualElement _statsRoot; // the element that contains rows (e.g. the element you currently call "Stats")
    private Label _freePointsLabel; // "Points"
    private int _freePoints;
    private int _startingFreePoints;

    private ScreenSwapper _swapper;

    private readonly Dictionary<StatId, StatRow> _rows = new();

    /// <summary>
    /// Optional: notify listeners whenever values change.
    /// </summary>
    public event Action Changed;

    /// <summary>
    /// Bind to the StatsPanel instance in the UI.
    /// statsRoot = the element that contains the row elements (Str/Agi/Int/End/Spr).
    /// freePointsLabel = label that shows remaining points.
    /// </summary>
    public void Bind(
        VisualElement statsRoot,
        Label freePointsLabel,
        int startingFreePoints = 3,
        ScreenSwapper swapper = null
    )
    {
        _statsRoot = statsRoot;
        _freePointsLabel = freePointsLabel;
        _startingFreePoints = startingFreePoints;
        _swapper = swapper;

        if (_statsRoot == null)
        {
            Debug.LogError(
                "StatsPanelBinder.Bind: statsRoot is null (expected element containing Str/Agi/Int/End/Spr)."
            );
            return;
        }

        if (_freePointsLabel == null)
        {
            Debug.LogError(
                "StatsPanelBinder.Bind: freePointsLabel is null (expected Label named 'Points')."
            );
            return;
        }

        _rows.Clear();

        BindRow(StatId.Str, "Str", swapper);
        BindRow(StatId.Agi, "Agi", swapper);
        BindRow(StatId.Int, "Int", swapper);
        BindRow(StatId.End, "End", swapper);
        BindRow(StatId.Spr, "Spr", swapper);

        ResetAllocations(); // start at default free points
        RenderAll();
    }

    public void Unbind()
    {
        // remove click handlers safely
        foreach (var row in _rows.Values)
        {
            if (row?.Plus != null)
                ClearButtonClick(row.Plus);
            if (row?.Minus != null)
                ClearButtonClick(row.Minus);
            if (row?.Plus != null && row.OnPlusEnter != null)
                row.Plus.UnregisterCallback(row.OnPlusEnter);
            if (row?.Minus != null && row.OnMinusEnter != null)
                row.Minus.UnregisterCallback(row.OnMinusEnter);
        }

        _rows.Clear();
        _statsRoot = null;
        _freePointsLabel = null;
        _swapper = null;
        Changed = null;
    }

    public void SetBonusText(StatId id, string text)
    {
        if (_rows.TryGetValue(id, out var row) && row != null)
        {
            row.BonusText = text;
            RenderRow(row);
        }
    }

    /// <summary>
    /// Sets new base stats (class/spec/etc.) and resets allocations.
    /// </summary>
    public void SetBaseStats(Stats baseStats, int? startingFreePointsOverride = null)
    {
        if (startingFreePointsOverride.HasValue)
            _startingFreePoints = startingFreePointsOverride.Value;

        SetBaseValue(StatId.Str, baseStats.strength);
        SetBaseValue(StatId.Agi, baseStats.agility);
        SetBaseValue(StatId.Int, baseStats.intelligence);
        SetBaseValue(StatId.End, baseStats.endurance);
        SetBaseValue(StatId.Spr, baseStats.spirit);

        ResetAllocations();
        RenderAll();
        Changed?.Invoke();
    }

    public int FreePoints => _freePoints;

    public Stats GetFinalStats()
    {
        int str = GetRow(StatId.Str).BaseValue + GetRow(StatId.Str).Added;
        int agi = GetRow(StatId.Agi).BaseValue + GetRow(StatId.Agi).Added;
        int intel = GetRow(StatId.Int).BaseValue + GetRow(StatId.Int).Added;
        int end = GetRow(StatId.End).BaseValue + GetRow(StatId.End).Added;
        int spr = GetRow(StatId.Spr).BaseValue + GetRow(StatId.Spr).Added;

        return new Stats
        {
            strength = str,
            agility = agi,
            intelligence = intel,
            endurance = end,
            spirit = spr,
        };
    }

    // ---------------- Internals ----------------

    private void BindRow(StatId id, string rowName, ScreenSwapper swapper)
    {
        var rowVe = _statsRoot.Q<VisualElement>(rowName);
        if (rowVe == null)
        {
            Debug.LogError($"StatsPanelBinder: Could not find stat row '{rowName}'.");
            return;
        }

        var minus = rowVe.Q<Button>("Minus");
        var plus = rowVe.Q<Button>("Plus");
        var name = rowVe.Q<Label>("Name");
        var value = rowVe.Q<Label>("Value");
        var added = rowVe.Q<Label>("AddedValue");
        var bonus = rowVe.Q<Label>("BonusValue");
        var icon = rowVe.Q<VisualElement>("Icon");

        if (minus == null || plus == null || value == null || added == null)
        {
            Debug.LogError(
                $"StatsPanelBinder: Row '{rowName}' missing Minus/Plus/Value/AddedValue."
            );
            return;
        }

        var row = new StatRow
        {
            Id = id,
            Minus = minus,
            Plus = plus,
            BaseValueLabel = value,
            AddedValueLabel = added,
            BonusValueLabel = bonus,
            BaseValue = 0,
            Added = 0,
            BonusText = string.Empty,
        };

        if (swapper != null)
        {
            string tooltip = StatTooltipLibrary.GetBaseStatTooltip(rowName);

            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                if (name != null)
                    name.EnableTooltip(swapper, tooltip, offset: 10f, maxWidth: 320f);
                if (icon != null)
                    icon.EnableTooltip(swapper, tooltip, offset: 10f, maxWidth: 320f);

                // If user is clicking +/-, hide any tooltip so it doesn't linger.
                row.OnPlusEnter = _ => swapper.HideTooltip();
                row.OnMinusEnter = _ => swapper.HideTooltip();
                plus.RegisterCallback(row.OnPlusEnter);
                minus.RegisterCallback(row.OnMinusEnter);
            }
        }

        SetButtonClick(plus, () => OnPlusClicked(row));
        SetButtonClick(minus, () => OnMinusClicked(row));

        _rows[id] = row;
    }

    private StatRow GetRow(StatId id)
    {
        if (!_rows.TryGetValue(id, out var row) || row == null)
            throw new InvalidOperationException($"StatsPanelBinder: Row '{id}' not bound.");
        return row;
    }

    private void ResetAllocations()
    {
        _freePoints = _startingFreePoints;

        foreach (var row in _rows.Values)
            row.Added = 0;
    }

    private void SetBaseValue(StatId id, int value)
    {
        if (_rows.TryGetValue(id, out var row))
            row.BaseValue = value;
    }

    private void RenderAll()
    {
        foreach (var row in _rows.Values)
            RenderRow(row);

        UpdateFreePointsLabel();
    }

    private void RenderRow(StatRow row)
    {
        row.BaseValueLabel.text = row.BaseValue.ToString();
        row.AddedValueLabel.text = row.Added > 0 ? $"+{row.Added}" : string.Empty;

        if (row.BonusValueLabel != null)
            row.BonusValueLabel.text = string.IsNullOrWhiteSpace(row.BonusText)
                ? string.Empty
                : row.BonusText;

        row.Plus.SetEnabled(_freePoints > 0);
        row.Minus.SetEnabled(row.Added > 0);
    }

    private void UpdateFreePointsLabel()
    {
        if (_freePointsLabel != null)
            _freePointsLabel.text = _freePoints.ToString();
    }

    private void OnPlusClicked(StatRow row)
    {
        if (_freePoints <= 0)
            return;

        row.Added += 1;
        _freePoints -= 1;

        RenderAll();
        Changed?.Invoke();
    }

    private void OnMinusClicked(StatRow row)
    {
        if (row.Added <= 0)
            return;

        row.Added -= 1;
        _freePoints += 1;

        RenderAll();
        Changed?.Invoke();
    }

    // --- safe rebinding helper ---

    private static void SetButtonClick(Button btn, Action onClick)
    {
        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = onClick;
        btn.clicked += onClick;
    }

    private static void ClearButtonClick(Button btn)
    {
        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = null;
    }

    public Stats GetAllocatedStatsDelta()
    {
        // returns ONLY the points allocated via +/-
        return new Stats
        {
            strength = GetRow(StatId.Str).Added,
            agility = GetRow(StatId.Agi).Added,
            intelligence = GetRow(StatId.Int).Added,
            endurance = GetRow(StatId.End).Added,
            spirit = GetRow(StatId.Spr).Added,
        };
    }

    public int GetAllocatedPointsTotal()
    {
        return GetRow(StatId.Str).Added
            + GetRow(StatId.Agi).Added
            + GetRow(StatId.Int).Added
            + GetRow(StatId.End).Added
            + GetRow(StatId.Spr).Added;
    }
}
