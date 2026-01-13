using System;
using System.Collections.Generic;
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
        public int BaseValue;
        public int Added;
    }

    private VisualElement _statsRoot; // the element that contains rows (e.g. the element you currently call "Stats")
    private Label _freePointsLabel; // "Points"
    private int _freePoints;
    private int _startingFreePoints;

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
    public void Bind(VisualElement statsRoot, Label freePointsLabel, int startingFreePoints = 3)
    {
        _statsRoot = statsRoot;
        _freePointsLabel = freePointsLabel;
        _startingFreePoints = startingFreePoints;

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

        BindRow(StatId.Str, "Str");
        BindRow(StatId.Agi, "Agi");
        BindRow(StatId.Int, "Int");
        BindRow(StatId.End, "End");
        BindRow(StatId.Spr, "Spr");

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
        }

        _rows.Clear();
        _statsRoot = null;
        _freePointsLabel = null;
        Changed = null;
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

    private void BindRow(StatId id, string rowName)
    {
        var rowVe = _statsRoot.Q<VisualElement>(rowName);
        if (rowVe == null)
        {
            Debug.LogError($"StatsPanelBinder: Could not find stat row '{rowName}'.");
            return;
        }

        var minus = rowVe.Q<Button>("Minus");
        var plus = rowVe.Q<Button>("Plus");
        var value = rowVe.Q<Label>("Value");
        var added = rowVe.Q<Label>("AddedValue");

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
            BaseValue = 0,
            Added = 0,
        };

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
