using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using UnityEngine;

[Serializable]
public struct BaseStatRollRule
{
    public BaseStatType stat;
    public ModOp op;

    [Tooltip("Min roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float min;

    [Tooltip("Max roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float max;

    [Min(0)]
    public int weight;

    [Tooltip("If Flat, round value to int.")]
    public bool roundFlatToInt;
}

[Serializable]
public struct DerivedStatRollRule
{
    public DerivedStatType stat;
    public ModOp op;

    [Tooltip("Min roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float min;

    [Tooltip("Max roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float max;

    [Min(0)]
    public int weight;

    [Tooltip("If Flat, round value to int.")]
    public bool roundFlatToInt;
}

[Serializable]
public struct SpellVariableOverrideRollRule
{
    public MyGame.Combat.SpellVariableOverride overrideValue;

    [Min(0)]
    public int weight;
}

[Serializable]
public struct CombatModRollRule
{
    public EffectStat stat;
    public EffectOp op;

    [Tooltip("Used only for *ByType stats.")]
    public DamageType damageType;

    [Tooltip("Min roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float min;

    [Tooltip("Max roll value (Flat = whole-ish, Percent = percent like 10 = +10%).")]
    public float max;

    [Min(0)]
    public int weight;

    [Tooltip("If Flat, round value to int.")]
    public bool roundFlatToInt;
}

public static class EquipmentRoller
{
    private const int GuaranteedRollWeightBlock = 100;

    public static int GetRarityTotalRolls(Rarity rarity)
    {
        // Each rarity rank adds +1 roll.
        // Common (0) => 1 roll, Uncommon (1) => 2 rolls, ..., Forbidden (6) => 7 rolls.
        return Mathf.Max(0, (int)rarity) + 1;
    }

    public static void RollAllByRarity(
        EquipmentDefinitionSO def,
        System.Random rng,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.CombatStatModifier> outCombatMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (def == null)
            return;

        if (rng == null)
            rng = new System.Random();

        outBaseMods?.Clear();
        outDerivedMods?.Clear();
        outCombatMods?.Clear();
        outOverrides?.Clear();

        int totalRolls = GetRarityTotalRolls(def.rarity);
        if (totalRolls <= 0)
            return;

        var usedBase = def.allowDuplicateBaseRolls ? null : new HashSet<int>();
        var usedDerived = def.allowDuplicateDerivedRolls ? null : new HashSet<int>();
        var usedCombat = def.allowDuplicateCombatModRolls ? null : new HashSet<int>();

        // -------------------------
        // Per-category max constraints + guaranteed rolls via weight blocks.
        // -------------------------
        // Groups: 0 Base, 1 Derived, 2 Combat
        // Rule weight encoding:
        // - Each full 100 weight guarantees one roll of that rule (e.g. 260 => 2 guaranteed).
        // - Remainder (weight % 100) participates in normal weighted rolling for remaining rolls.
        var rolledByGroup = new int[3];
        var maxByGroup = new int[3];

        int eligibleBase0 = RemainingEligible(def.baseRollTable, usedBase);
        int eligibleDerived0 = RemainingEligible(def.derivedRollTable, usedDerived);
        int eligibleCombat0 = RemainingEligibleCombat(def.combatModRollTable, usedCombat);
        // Back-compat: if a table exists but max is 0, treat it as "uncapped" (up to totalRolls).
        // Use the table/weight itself to disable a category.
        maxByGroup[0] =
            (eligibleBase0 > 0 && def.maxBaseRolls <= 0) ? totalRolls : def.maxBaseRolls;
        maxByGroup[1] =
            (eligibleDerived0 > 0 && def.maxDerivedRolls <= 0) ? totalRolls : def.maxDerivedRolls;
        maxByGroup[2] =
            (eligibleCombat0 > 0 && def.maxCombatModRolls <= 0)
                ? totalRolls
                : def.maxCombatModRolls;
        // Clamp max to what's actually possible if duplicates are NOT allowed.
        if (!def.allowDuplicateBaseRolls)
            maxByGroup[0] = Mathf.Min(maxByGroup[0], eligibleBase0);
        if (!def.allowDuplicateDerivedRolls)
            maxByGroup[1] = Mathf.Min(maxByGroup[1], eligibleDerived0);
        if (!def.allowDuplicateCombatModRolls)
            maxByGroup[2] = Mathf.Min(maxByGroup[2], eligibleCombat0);
        // Precompute per-table remainder weights (weight % 100) used for the normal weighted stage.
        // These weights are not written back to the SO; they are used only during rolling.
        var baseRemainder = BuildRemainderWeights(def.baseRollTable);
        var derivedRemainder = BuildRemainderWeights(def.derivedRollTable);
        var combatRemainder = BuildRemainderWeights(def.combatModRollTable);
        // 1) Apply guaranteed rolls implied by weights (each full 100 guarantees one roll).
        // If the definition guarantees more rolls than the rarity would normally grant,
        // we still honor the guarantees (rarity rolls are the "bonus" rolls on top).
        int guaranteedTotal =
            CountGuaranteedRolls(def.baseRollTable, def.allowDuplicateBaseRolls, maxByGroup[0])
            + CountGuaranteedRolls(
                def.derivedRollTable,
                def.allowDuplicateDerivedRolls,
                maxByGroup[1]
            )
            + CountGuaranteedRollsCombat(
                def.combatModRollTable,
                def.allowDuplicateCombatModRolls,
                maxByGroup[2]
            )
            ;

        totalRolls = Mathf.Max(totalRolls, guaranteedTotal);
        int rollsRemaining = totalRolls;

        ApplyGuaranteedRolls(
            def,
            rng,
            maxByGroup,
            rolledByGroup,
            usedBase,
            usedDerived,
            usedCombat,
            outBaseMods,
            outDerivedMods,
            outCombatMods,
            outOverrides,
            ref rollsRemaining
        );

        // 2) Allocate remaining rolls by remainder weights, respecting per-category max.
        for (int roll = 0; roll < rollsRemaining; roll++)
        {
            int wBase = ComputeGroupWeightByRemainder(
                group: 0,
                rolledByGroup,
                maxByGroup,
                def.baseRollTable,
                usedBase,
                baseRemainder
            );
            int wDerived = ComputeGroupWeightByRemainder(
                group: 1,
                rolledByGroup,
                maxByGroup,
                def.derivedRollTable,
                usedDerived,
                derivedRemainder
            );
            int wCombat = ComputeGroupWeightByRemainderCombat(
                group: 2,
                rolledByGroup,
                maxByGroup,
                def.combatModRollTable,
                usedCombat,
                combatRemainder
            );
            int totalW = wBase + wDerived + wCombat;
            if (totalW <= 0)
                break;

            int pick = rng.Next(0, totalW);

            // Try the chosen group first, but fall back to others if it fails.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int group = PickGroupIndex(pick, wBase, wDerived, wCombat);
                if (
                    TryRollOneFromGroupWithWeights(
                        def,
                        group,
                        rng,
                        usedBase,
                        usedDerived,
                        usedCombat,
                        outBaseMods,
                        outDerivedMods,
                        outCombatMods,
                        outOverrides,
                        baseRemainder,
                        derivedRemainder,
                        combatRemainder
                    )
                )
                {
                    rolledByGroup[group]++;
                    break;
                }

                // Exclude the failed group and retry.
                switch (group)
                {
                    case 0:
                        wBase = 0;
                        break;
                    case 1:
                        wDerived = 0;
                        break;
                    case 2:
                        wCombat = 0;
                        break;
                }

                totalW = wBase + wDerived + wCombat;
                if (totalW <= 0)
                    break;
                pick = rng.Next(0, totalW);
            }
        }
    }

    private static int ComputeGroupWeight(
        int group,
        int[] rolledByGroup,
        int[] maxByGroup,
        int eligibleCount
    )
    {
        if (maxByGroup == null || rolledByGroup == null)
            return 0;

        int cap = maxByGroup[group];
        if (cap <= 0)
            return 0;

        int remainingCap = cap - rolledByGroup[group];
        if (remainingCap <= 0)
            return 0;

        return Mathf.Min(eligibleCount, remainingCap);
    }

    private static int[] BuildRemainderWeights(IReadOnlyList<BaseStatRollRule> table)
    {
        if (table == null || table.Count == 0)
            return null;

        var w = new int[table.Count];
        for (int i = 0; i < table.Count; i++)
            w[i] = GetRemainderWeight(table[i].weight);
        return w;
    }

    private static int[] BuildRemainderWeights(IReadOnlyList<DerivedStatRollRule> table)
    {
        if (table == null || table.Count == 0)
            return null;

        var w = new int[table.Count];
        for (int i = 0; i < table.Count; i++)
            w[i] = GetRemainderWeight(table[i].weight);
        return w;
    }

    private static int[] BuildRemainderWeights(IReadOnlyList<CombatModRollRule> table)
    {
        if (table == null || table.Count == 0)
            return null;

        var w = new int[table.Count];
        for (int i = 0; i < table.Count; i++)
            w[i] = GetRemainderWeight(table[i].weight);
        return w;
    }

    private static int GetRemainderWeight(int weight)
    {
        if (weight <= 0)
            return 0;
        int rem = weight % GuaranteedRollWeightBlock;
        return Mathf.Max(0, rem);
    }

    private static int CountGuaranteedRolls(
        IReadOnlyList<BaseStatRollRule> table,
        bool allowDuplicates,
        int maxByGroup
    )
    {
        if (maxByGroup <= 0 || table == null || table.Count == 0)
            return 0;

        int total = 0;
        for (int i = 0; i < table.Count; i++)
        {
            int w = table[i].weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int g = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                g = Mathf.Min(1, g);
            total += g;
            if (total >= maxByGroup)
                return maxByGroup;
        }
        return Mathf.Min(total, maxByGroup);
    }

    private static int CountGuaranteedRolls(
        IReadOnlyList<DerivedStatRollRule> table,
        bool allowDuplicates,
        int maxByGroup
    )
    {
        if (maxByGroup <= 0 || table == null || table.Count == 0)
            return 0;

        int total = 0;
        for (int i = 0; i < table.Count; i++)
        {
            int w = table[i].weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int g = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                g = Mathf.Min(1, g);
            total += g;
            if (total >= maxByGroup)
                return maxByGroup;
        }
        return Mathf.Min(total, maxByGroup);
    }

    private static int CountGuaranteedRollsCombat(
        IReadOnlyList<CombatModRollRule> table,
        bool allowDuplicates,
        int maxByGroup
    )
    {
        if (maxByGroup <= 0 || table == null || table.Count == 0)
            return 0;

        int total = 0;
        for (int i = 0; i < table.Count; i++)
        {
            var r = table[i];
            if (!IsEligibleCombatRule(r))
                continue;

            int w = r.weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int g = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                g = Mathf.Min(1, g);
            total += g;
            if (total >= maxByGroup)
                return maxByGroup;
        }

        return Mathf.Min(total, maxByGroup);
    }

    private static void ApplyGuaranteedRolls(
        EquipmentDefinitionSO def,
        System.Random rng,
        int[] maxByGroup,
        int[] rolledByGroup,
        HashSet<int> usedBase,
        HashSet<int> usedDerived,
        HashSet<int> usedCombat,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.CombatStatModifier> outCombatMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        ref int rollsRemaining
    )
    {
        if (def == null)
            return;

        // Base
        ApplyGuaranteedRollsBase(
            def.baseRollTable,
            def.allowDuplicateBaseRolls,
            rng,
            maxByGroup[0],
            rolledByGroup,
            usedBase,
            outBaseMods,
            ref rollsRemaining
        );
        // Derived
        ApplyGuaranteedRollsDerived(
            def.derivedRollTable,
            def.allowDuplicateDerivedRolls,
            rng,
            maxByGroup[1],
            rolledByGroup,
            usedDerived,
            outDerivedMods,
            ref rollsRemaining
        );
        // Combat
        ApplyGuaranteedRollsCombat(
            def.combatModRollTable,
            def.allowDuplicateCombatModRolls,
            rng,
            maxByGroup[2],
            rolledByGroup,
            usedCombat,
            outCombatMods,
            outOverrides,
            ref rollsRemaining
        );
    }

    private static void ApplyGuaranteedRollsBase(
        IReadOnlyList<BaseStatRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<BaseStatModifier> outMods,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outMods == null)
            return;

        int group = 0;
        for (
            int i = 0;
            i < table.Count && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
            i++
        )
        {
            int w = table[i].weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int guaranteed = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                guaranteed = Mathf.Min(1, guaranteed);

            for (
                int k = 0;
                k < guaranteed && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
                k++
            )
            {
                if (used != null && used.Contains(i))
                    break;

                if (!TryRollOneBaseExact(table, i, rng, used, outMods))
                    break;

                rolledByGroup[group]++;
                rollsRemaining--;
            }
        }
    }

    private static void ApplyGuaranteedRollsDerived(
        IReadOnlyList<DerivedStatRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<DerivedStatModifier> outMods,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outMods == null)
            return;

        int group = 1;
        for (
            int i = 0;
            i < table.Count && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
            i++
        )
        {
            int w = table[i].weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int guaranteed = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                guaranteed = Mathf.Min(1, guaranteed);

            for (
                int k = 0;
                k < guaranteed && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
                k++
            )
            {
                if (used != null && used.Contains(i))
                    break;

                if (!TryRollOneDerivedExact(table, i, rng, used, outMods))
                    break;

                rolledByGroup[group]++;
                rollsRemaining--;
            }
        }
    }

    private static void ApplyGuaranteedRollsCombat(
        IReadOnlyList<CombatModRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<MyGame.Combat.CombatStatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outMods == null || outOverrides == null)
            return;

        int group = 2;
        for (
            int i = 0;
            i < table.Count && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
            i++
        )
        {
            var r = table[i];
            if (!IsEligibleCombatRule(r))
                continue;

            int w = r.weight;
            if (w < GuaranteedRollWeightBlock)
                continue;

            int guaranteed = w / GuaranteedRollWeightBlock;
            if (!allowDuplicates)
                guaranteed = Mathf.Min(1, guaranteed);

            for (
                int k = 0;
                k < guaranteed && rollsRemaining > 0 && rolledByGroup[group] < groupCap;
                k++
            )
            {
                if (used != null && used.Contains(i))
                    break;

                if (!TryRollOneCombatExact(table, i, rng, used, outMods, outOverrides))
                    break;

                rolledByGroup[group]++;
                rollsRemaining--;
            }
        }
    }

    private static int ComputeGroupWeightByRemainder<T>(
        int group,
        int[] rolledByGroup,
        int[] maxByGroup,
        IReadOnlyList<T> table,
        HashSet<int> used,
        int[] remainderWeights
    )
    {
        if (rolledByGroup == null || maxByGroup == null)
            return 0;

        int cap = maxByGroup[group];
        if (cap <= 0)
            return 0;

        if (rolledByGroup[group] >= cap)
            return 0;

        if (table == null || table.Count == 0 || remainderWeights == null)
            return 0;

        int total = 0;
        int count = Mathf.Min(table.Count, remainderWeights.Length);
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            if (!IsEligible(table[i]))
                continue;

            int w = remainderWeights[i];
            if (w <= 0)
                continue;

            total += w;
        }

        return total;
    }

    private static int ComputeGroupWeightByRemainderCombat(
        int group,
        int[] rolledByGroup,
        int[] maxByGroup,
        IReadOnlyList<CombatModRollRule> table,
        HashSet<int> used,
        int[] remainderWeights
    )
    {
        if (rolledByGroup == null || maxByGroup == null)
            return 0;

        int cap = maxByGroup[group];
        if (cap <= 0)
            return 0;

        if (rolledByGroup[group] >= cap)
            return 0;

        if (table == null || table.Count == 0 || remainderWeights == null)
            return 0;

        int total = 0;
        int count = Mathf.Min(table.Count, remainderWeights.Length);
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            var r = table[i];
            if (!IsEligibleCombatRule(r))
                continue;

            int w = remainderWeights[i];
            if (w <= 0)
                continue;

            total += w;
        }

        return total;
    }

    public static void RollBase(
        IReadOnlyList<BaseStatRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<BaseStatModifier> outMods
    )
    {
        if (outMods == null)
            return;

        outMods.Clear();

        if (table == null || table.Count == 0)
            return;

        if (rng == null)
            rng = new System.Random();

        int count = ClampRollCount(minRolls, maxRolls, rng);
        if (count <= 0)
            return;

        var used = allowDuplicates ? null : new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx = PickWeightedIndex(table, used, rng);
            if (idx < 0)
                break;

            if (used != null)
                used.Add(idx);

            var r = table[idx];
            if (!IsEligible(r))
                continue;
            float v = RollValue(r.min, r.max, rng);

            int iv = Mathf.RoundToInt(v);

            outMods.Add(
                new BaseStatModifier
                {
                    stat = r.stat,
                    op = r.op,
                    value = iv,
                }
            );
        }
    }

    public static void RollDerived(
        IReadOnlyList<DerivedStatRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<DerivedStatModifier> outMods
    )
    {
        if (outMods == null)
            return;

        outMods.Clear();

        if (table == null || table.Count == 0)
            return;

        if (rng == null)
            rng = new System.Random();

        int count = ClampRollCount(minRolls, maxRolls, rng);
        if (count <= 0)
            return;

        var used = allowDuplicates ? null : new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx = PickWeightedIndex(table, used, rng);
            if (idx < 0)
                break;

            if (used != null)
                used.Add(idx);

            var r = table[idx];
            float v = RollValue(r.min, r.max, rng);

            int iv = Mathf.RoundToInt(v);

            outMods.Add(
                new DerivedStatModifier
                {
                    stat = r.stat,
                    op = r.op,
                    value = iv,
                }
            );
        }
    }

    public static void RollSpellOverrides(
        IReadOnlyList<SpellVariableOverrideRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return;

        outOverrides.Clear();

        if (table == null || table.Count == 0)
            return;

        if (rng == null)
            rng = new System.Random();

        int count = ClampRollCount(minRolls, maxRolls, rng);
        if (count <= 0)
            return;

        var used = allowDuplicates ? null : new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx = PickWeightedIndex(table, used, rng);
            if (idx < 0)
                break;

            if (used != null)
                used.Add(idx);

            outOverrides.Add(table[idx].overrideValue);
        }
    }

    public static void RollCombatMods(
        IReadOnlyList<CombatModRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.CombatStatModifier> outMods
    )
    {
        if (outMods == null)
            return;

        outMods.Clear();

        if (table == null || table.Count == 0)
            return;

        if (rng == null)
            rng = new System.Random();

        int count = ClampRollCount(minRolls, maxRolls, rng);
        if (count <= 0)
            return;

        var used = allowDuplicates ? null : new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx = PickWeightedIndex(table, used, rng);
            if (idx < 0)
                break;

            if (used != null)
                used.Add(idx);

            var r = table[idx];

            // Positive-only.
            float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
            float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

            if (r.op == EffectOp.Flat && r.roundFlatToInt)
                min = Mathf.Max(1f, min);
            else if (r.op == EffectOp.Percent)
                min = Mathf.Max(0.01f, min);

            float v = RollValue(min, max, rng);
            var stat = r.stat;
            if (stat == EffectStat.None)
                continue;

            int iv;
            if (r.op == EffectOp.Flat && IsPowerScalingStat(stat) && max <= 1f)
            {
                iv = Mathf.RoundToInt(v * 100f);
            }
            else
            {
                if (r.op == EffectOp.Flat && r.roundFlatToInt)
                    v = Mathf.RoundToInt(v);
                iv = Mathf.RoundToInt(v);
            }

            outMods.Add(
                new MyGame.Combat.CombatStatModifier
                {
                    stat = stat,
                    op = r.op,
                    damageType = r.damageType,
                    value = iv,
                }
            );
        }
    }

    private static int ClampRollCount(int min, int max, System.Random rng)
    {
        if (min < 0)
            min = 0;
        if (max < 0)
            max = 0;
        if (max < min)
            max = min;

        if (max == min)
            return min;

        // Inclusive range
        return rng.Next(min, max + 1);
    }

    private static float RollValue(float min, float max, System.Random rng)
    {
        if (max < min)
        {
            var t = min;
            min = max;
            max = t;
        }

        if (Mathf.Approximately(min, max))
            return min;

        double t2 = rng.NextDouble();
        return (float)(min + (max - min) * t2);
    }

    private static int PickWeightedIndex(
        IReadOnlyList<BaseStatRollRule> rules,
        HashSet<int> used,
        System.Random rng
    )
    {
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<BaseStatRollRule> rules,
        int[] weights,
        HashSet<int> used,
        System.Random rng
    )
    {
        if (rules == null || rules.Count == 0 || weights == null || weights.Length == 0)
            return -1;

        int total = 0;
        int count = Mathf.Min(rules.Count, weights.Length);
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<DerivedStatRollRule> rules,
        HashSet<int> used,
        System.Random rng
    )
    {
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<DerivedStatRollRule> rules,
        int[] weights,
        HashSet<int> used,
        System.Random rng
    )
    {
        if (rules == null || rules.Count == 0 || weights == null || weights.Length == 0)
            return -1;

        int total = 0;
        int count = Mathf.Min(rules.Count, weights.Length);
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<SpellVariableOverrideRollRule> rules,
        HashSet<int> used,
        System.Random rng
    )
    {
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<CombatModRollRule> rules,
        int[] weights,
        HashSet<int> used,
        System.Random rng
    )
    {
        if (rules == null || rules.Count == 0 || weights == null || weights.Length == 0)
            return -1;

        int total = 0;
        int count = Mathf.Min(rules.Count, weights.Length);
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            var rr = rules[i];
            if (!IsEligibleCombatRule(rr))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;
            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            var rr = rules[i];
            if (!IsEligibleCombatRule(rr))
                continue;

            int w = weights[i];
            if (w <= 0)
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickWeightedIndex(
        IReadOnlyList<CombatModRollRule> rules,
        HashSet<int> used,
        System.Random rng
    )
    {
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;

            if (!IsEligibleCombatRule(rules[i]))
                continue;

            total += w;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            int w = rules[i].weight;
            if (w <= 0)
                continue;

            if (!IsEligibleCombatRule(rules[i]))
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickGroupIndex(int pick, int wBase, int wDerived, int wCombat)
    {
        int acc = wBase;
        if (pick < acc)
            return 0;
        acc += wDerived;
        if (pick < acc)
            return 1;
        acc += wCombat;
        if (pick < acc)
            return 2;
        return 2;
    }

    private static bool TryRollOneFromGroup(
        EquipmentDefinitionSO def,
        int group,
        System.Random rng,
        HashSet<int> usedBase,
        HashSet<int> usedDerived,
        HashSet<int> usedCombat,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.CombatStatModifier> outCombatMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        switch (group)
        {
            case 0:
                return TryRollOneBase(def.baseRollTable, rng, usedBase, outBaseMods);
            case 1:
                return TryRollOneDerived(def.derivedRollTable, rng, usedDerived, outDerivedMods);
            case 2:
                return TryRollOneCombat(
                    def.combatModRollTable,
                    rng,
                    usedCombat,
                    outCombatMods,
                    outOverrides
                );
            default:
                return false;
        }
    }

    private static bool TryRollOneFromGroupWithWeights(
        EquipmentDefinitionSO def,
        int group,
        System.Random rng,
        HashSet<int> usedBase,
        HashSet<int> usedDerived,
        HashSet<int> usedCombat,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.CombatStatModifier> outCombatMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        int[] baseWeights,
        int[] derivedWeights,
        int[] combatWeights
    )
    {
        switch (group)
        {
            case 0:
                return TryRollOneBase(def.baseRollTable, baseWeights, rng, usedBase, outBaseMods);
            case 1:
                return TryRollOneDerived(
                    def.derivedRollTable,
                    derivedWeights,
                    rng,
                    usedDerived,
                    outDerivedMods
                );
            case 2:
                return TryRollOneCombat(
                    def.combatModRollTable,
                    combatWeights,
                    rng,
                    usedCombat,
                    outCombatMods,
                    outOverrides
                );
            default:
                return false;
        }
    }

    private static bool TryRollOneBase(
        IReadOnlyList<BaseStatRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<BaseStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);

        var r = table[idx];
        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneBase(
        IReadOnlyList<BaseStatRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<BaseStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);

        var r = table[idx];
        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneBaseExact(
        IReadOnlyList<BaseStatRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<BaseStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;
        if (idx < 0 || idx >= table.Count)
            return false;
        if (used != null && used.Contains(idx))
            return false;

        var r = table[idx];
        if (r.weight <= 0)
            return false;

        used?.Add(idx);

        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );

        return true;
    }

    private static bool TryRollOneDerived(
        IReadOnlyList<DerivedStatRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<DerivedStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);

        var r = table[idx];
        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneDerived(
        IReadOnlyList<DerivedStatRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<DerivedStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);

        var r = table[idx];
        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneDerivedExact(
        IReadOnlyList<DerivedStatRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<DerivedStatModifier> outMods
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;
        if (idx < 0 || idx >= table.Count)
            return false;
        if (used != null && used.Contains(idx))
            return false;

        var r = table[idx];
        if (r.weight <= 0)
            return false;

        used?.Add(idx);

        float v = RollValue(r.min, r.max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        int iv = Mathf.RoundToInt(v);

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = iv,
            }
        );

        return true;
    }

    private static bool TryRollOneCombat(
        IReadOnlyList<CombatModRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.CombatStatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        if (!IsEligibleCombatRule(r))
            return false;

        // Positive-only.
        float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
        float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

        if (r.op == EffectOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == EffectOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        var stat = r.stat;
        int iv;
        if (r.op == EffectOp.Flat && IsPowerScalingStat(stat) && max <= 1f)
        {
            iv = Mathf.RoundToInt(v * 100f);
        }
        else
        {
            if (r.op == EffectOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);
            iv = Mathf.RoundToInt(v);
        }

        if (stat == EffectStat.None)
            return false;

        outMods.Add(
            new MyGame.Combat.CombatStatModifier
            {
                stat = stat,
                op = r.op,
                damageType = r.damageType,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneCombat(
        IReadOnlyList<CombatModRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.CombatStatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        if (!IsEligibleCombatRule(r))
            return false;

        // Positive-only.
        float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
        float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

        if (r.op == EffectOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == EffectOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        var stat = r.stat;
        int iv;
        if (r.op == EffectOp.Flat && IsPowerScalingStat(stat) && max <= 1f)
        {
            iv = Mathf.RoundToInt(v * 100f);
        }
        else
        {
            if (r.op == EffectOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);
            iv = Mathf.RoundToInt(v);
        }

        if (stat == EffectStat.None)
            return false;

        outMods.Add(
            new MyGame.Combat.CombatStatModifier
            {
                stat = stat,
                op = r.op,
                damageType = r.damageType,
                value = iv,
            }
        );
        return true;
    }

    private static bool TryRollOneCombatExact(
        IReadOnlyList<CombatModRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.CombatStatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null)
            return false;
        if (table == null || table.Count == 0)
            return false;
        if (idx < 0 || idx >= table.Count)
            return false;
        if (used != null && used.Contains(idx))
            return false;

        var r = table[idx];
        if (!IsEligibleCombatRule(r))
            return false;

        used?.Add(idx);

        // Positive-only.
        float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
        float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

        if (r.op == EffectOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == EffectOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        var stat = r.stat;
        int iv;
        if (r.op == EffectOp.Flat && IsPowerScalingStat(stat) && max <= 1f)
        {
            iv = Mathf.RoundToInt(v * 100f);
        }
        else
        {
            if (r.op == EffectOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);
            iv = Mathf.RoundToInt(v);
        }

        if (stat == EffectStat.None)
            return false;

            outMods.Add(
                new MyGame.Combat.CombatStatModifier
                {
                    stat = stat,
                    op = r.op,
                    damageType = r.damageType,
                value = iv,
            }
        );
        return true;
    }

    private static bool IsEligibleCombatRule(CombatModRollRule r)
    {
        if (r.weight <= 0)
            return false;
        float max = Mathf.Max(r.min, r.max);
        if (max <= 0f)
            return false;
        if (r.stat == EffectStat.None)
            return false;
        if (RequiresDamageType(r.stat) && r.damageType == DamageType.None)
            return false;
        return true;
    }

    private static bool RequiresDamageType(EffectStat stat)
    {
        switch (stat)
        {
            case EffectStat.AttackerBonusByType:
            case EffectStat.AttackerWeakenByType:
            case EffectStat.DefenderVulnerabilityByType:
            case EffectStat.DefenderResistByType:
                return true;
            default:
                return false;
        }
    }

    private static bool IsPowerScalingStat(EffectStat stat)
    {
        switch (stat)
        {
            case EffectStat.PowerScalingAll:
            case EffectStat.PowerScalingPhysical:
            case EffectStat.PowerScalingMagic:
                return true;
            default:
                return false;
        }
    }

    private static int RemainingEligible<T>(IReadOnlyList<T> table, HashSet<int> used)
    {
        if (table == null || table.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < table.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            if (IsEligible(table[i]))
                count++;
        }

        return count;
    }

    private static int RemainingEligibleCombat(
        IReadOnlyList<CombatModRollRule> table,
        HashSet<int> used
    )
    {
        if (table == null || table.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < table.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            var r = table[i];
            if (!IsEligibleCombatRule(r))
                continue;
            count++;
        }

        return count;
    }

    private static bool IsEligible<T>(T rule)
    {
        switch (rule)
        {
            case BaseStatRollRule r:
                return r.weight > 0;
            case DerivedStatRollRule r:
                return r.weight > 0;
            default:
                return true;
        }
    }
}
