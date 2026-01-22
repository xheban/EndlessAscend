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
public struct SpellCombatModifierRollRule
{
    public SpellCombatModifier modifier;

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
public struct DamageTypeRollRule
{
    public MyGame.Combat.DamageType damageType;

    [Min(0)]
    public int weight;
}

[Serializable]
public struct DamageKindRollRule
{
    public MyGame.Combat.DamageKind damageKind;

    [Min(0)]
    public int weight;
}

[Serializable]
public struct DamageRangeRollRule
{
    public MyGame.Combat.DamageRangeType damageRangeType;

    [Min(0)]
    public int weight;
}

public enum CombatModRollTarget
{
    // NOTE: Excludes anything already covered by derived stats.
    Damage,
    PhysicalDamage,
    MagicDamage,

    SpellBase,
    PhysicalSpellBase,
    MagicSpellBase,

    PowerScaling,
    AttackPowerScaling,
    MagicPowerScaling,

    IgnoreDefence,
}

[Serializable]
public struct CombatModRollRule
{
    public CombatModRollTarget target;
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
        List<MyGame.Combat.SpellCombatModifier> outCombatMods,
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
        var usedKind = def.allowDuplicateDamageKindRolls ? null : new HashSet<int>();
        var usedRange = def.allowDuplicateDamageRangeRolls ? null : new HashSet<int>();
        var usedType = def.allowDuplicateDamageTypeRolls ? null : new HashSet<int>();

        // -------------------------
        // Per-category max constraints + guaranteed rolls via weight blocks.
        // -------------------------
        // Groups: 0 Base, 1 Derived, 2 Combat, 3 DamageKind, 4 DamageRange, 5 DamageType
        // Rule weight encoding:
        // - Each full 100 weight guarantees one roll of that rule (e.g. 260 => 2 guaranteed).
        // - Remainder (weight % 100) participates in normal weighted rolling for remaining rolls.
        var rolledByGroup = new int[6];
        var maxByGroup = new int[6];

        int eligibleBase0 = RemainingEligible(def.baseRollTable, usedBase);
        int eligibleDerived0 = RemainingEligible(def.derivedRollTable, usedDerived);
        int eligibleCombat0 = RemainingEligibleCombat(def.combatModRollTable, usedCombat);
        int eligibleKind0 = RemainingEligible(def.damageKindRollTable, usedKind);
        int eligibleRange0 = RemainingEligible(def.damageRangeRollTable, usedRange);
        int eligibleType0 = RemainingEligible(def.damageTypeRollTable, usedType);

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
        maxByGroup[3] =
            (eligibleKind0 > 0 && def.maxDamageKindRolls <= 0)
                ? totalRolls
                : def.maxDamageKindRolls;
        maxByGroup[4] =
            (eligibleRange0 > 0 && def.maxDamageRangeRolls <= 0)
                ? totalRolls
                : def.maxDamageRangeRolls;
        maxByGroup[5] =
            (eligibleType0 > 0 && def.maxDamageTypeRolls <= 0)
                ? totalRolls
                : def.maxDamageTypeRolls;

        // Clamp max to what's actually possible if duplicates are NOT allowed.
        if (!def.allowDuplicateBaseRolls)
            maxByGroup[0] = Mathf.Min(maxByGroup[0], eligibleBase0);
        if (!def.allowDuplicateDerivedRolls)
            maxByGroup[1] = Mathf.Min(maxByGroup[1], eligibleDerived0);
        if (!def.allowDuplicateCombatModRolls)
            maxByGroup[2] = Mathf.Min(maxByGroup[2], eligibleCombat0);
        if (!def.allowDuplicateDamageKindRolls)
            maxByGroup[3] = Mathf.Min(maxByGroup[3], eligibleKind0);
        if (!def.allowDuplicateDamageRangeRolls)
            maxByGroup[4] = Mathf.Min(maxByGroup[4], eligibleRange0);
        if (!def.allowDuplicateDamageTypeRolls)
            maxByGroup[5] = Mathf.Min(maxByGroup[5], eligibleType0);

        // Precompute per-table remainder weights (weight % 100) used for the normal weighted stage.
        // These weights are not written back to the SO; they are used only during rolling.
        var baseRemainder = BuildRemainderWeights(def.baseRollTable);
        var derivedRemainder = BuildRemainderWeights(def.derivedRollTable);
        var combatRemainder = BuildRemainderWeights(def.combatModRollTable);
        var kindRemainder = BuildRemainderWeights(def.damageKindRollTable);
        var rangeRemainder = BuildRemainderWeights(def.damageRangeRollTable);
        var typeRemainder = BuildRemainderWeights(def.damageTypeRollTable);

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
            + CountGuaranteedRolls(
                def.damageKindRollTable,
                def.allowDuplicateDamageKindRolls,
                maxByGroup[3]
            )
            + CountGuaranteedRolls(
                def.damageRangeRollTable,
                def.allowDuplicateDamageRangeRolls,
                maxByGroup[4]
            )
            + CountGuaranteedRolls(
                def.damageTypeRollTable,
                def.allowDuplicateDamageTypeRolls,
                maxByGroup[5]
            );

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
            usedKind,
            usedRange,
            usedType,
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
            int wKind = ComputeGroupWeightByRemainder(
                group: 3,
                rolledByGroup,
                maxByGroup,
                def.damageKindRollTable,
                usedKind,
                kindRemainder
            );
            int wRange = ComputeGroupWeightByRemainder(
                group: 4,
                rolledByGroup,
                maxByGroup,
                def.damageRangeRollTable,
                usedRange,
                rangeRemainder
            );
            int wType = ComputeGroupWeightByRemainder(
                group: 5,
                rolledByGroup,
                maxByGroup,
                def.damageTypeRollTable,
                usedType,
                typeRemainder
            );

            int totalW = wBase + wDerived + wCombat + wKind + wRange + wType;
            if (totalW <= 0)
                break;

            int pick = rng.Next(0, totalW);

            // Try the chosen group first, but fall back to others if it fails.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int group = PickGroupIndex(pick, wBase, wDerived, wCombat, wKind, wRange, wType);
                if (
                    TryRollOneFromGroupWithWeights(
                        def,
                        group,
                        rng,
                        usedBase,
                        usedDerived,
                        usedCombat,
                        usedKind,
                        usedRange,
                        usedType,
                        outBaseMods,
                        outDerivedMods,
                        outCombatMods,
                        outOverrides,
                        baseRemainder,
                        derivedRemainder,
                        combatRemainder,
                        kindRemainder,
                        rangeRemainder,
                        typeRemainder
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
                    case 3:
                        wKind = 0;
                        break;
                    case 4:
                        wRange = 0;
                        break;
                    case 5:
                        wType = 0;
                        break;
                }

                totalW = wBase + wDerived + wCombat + wKind + wRange + wType;
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

    private static int[] BuildRemainderWeights(IReadOnlyList<DamageKindRollRule> table)
    {
        if (table == null || table.Count == 0)
            return null;

        var w = new int[table.Count];
        for (int i = 0; i < table.Count; i++)
            w[i] = GetRemainderWeight(table[i].weight);
        return w;
    }

    private static int[] BuildRemainderWeights(IReadOnlyList<DamageRangeRollRule> table)
    {
        if (table == null || table.Count == 0)
            return null;

        var w = new int[table.Count];
        for (int i = 0; i < table.Count; i++)
            w[i] = GetRemainderWeight(table[i].weight);
        return w;
    }

    private static int[] BuildRemainderWeights(IReadOnlyList<DamageTypeRollRule> table)
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

    private static int CountGuaranteedRolls(
        IReadOnlyList<DamageKindRollRule> table,
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
        IReadOnlyList<DamageRangeRollRule> table,
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
        IReadOnlyList<DamageTypeRollRule> table,
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
        HashSet<int> usedKind,
        HashSet<int> usedRange,
        HashSet<int> usedType,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.SpellCombatModifier> outCombatMods,
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
        // Kind
        ApplyGuaranteedRollsDamageKind(
            def.damageKindRollTable,
            def.allowDuplicateDamageKindRolls,
            rng,
            maxByGroup[3],
            rolledByGroup,
            usedKind,
            outOverrides,
            ref rollsRemaining
        );
        // Range
        ApplyGuaranteedRollsDamageRange(
            def.damageRangeRollTable,
            def.allowDuplicateDamageRangeRolls,
            rng,
            maxByGroup[4],
            rolledByGroup,
            usedRange,
            outOverrides,
            ref rollsRemaining
        );
        // Type
        ApplyGuaranteedRollsDamageType(
            def.damageTypeRollTable,
            def.allowDuplicateDamageTypeRolls,
            rng,
            maxByGroup[5],
            rolledByGroup,
            usedType,
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
        List<MyGame.Combat.SpellCombatModifier> outMods,
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

    private static void ApplyGuaranteedRollsDamageKind(
        IReadOnlyList<DamageKindRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outOverrides == null)
            return;

        int group = 3;
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

                if (!TryRollOneDamageKindExact(table, i, rng, used, outOverrides))
                    break;

                rolledByGroup[group]++;
                rollsRemaining--;
            }
        }
    }

    private static void ApplyGuaranteedRollsDamageRange(
        IReadOnlyList<DamageRangeRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outOverrides == null)
            return;

        int group = 4;
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

                if (!TryRollOneDamageRangeExact(table, i, rng, used, outOverrides))
                    break;

                rolledByGroup[group]++;
                rollsRemaining--;
            }
        }
    }

    private static void ApplyGuaranteedRollsDamageType(
        IReadOnlyList<DamageTypeRollRule> table,
        bool allowDuplicates,
        System.Random rng,
        int groupCap,
        int[] rolledByGroup,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        ref int rollsRemaining
    )
    {
        if (groupCap <= 0 || rollsRemaining <= 0)
            return;
        if (table == null || table.Count == 0)
            return;
        if (outOverrides == null)
            return;

        int group = 5;
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

                if (!TryRollOneDamageTypeExact(table, i, rng, used, outOverrides))
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
            float v = RollValue(r.min, r.max, rng);

            if (r.op == ModOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);

            outMods.Add(
                new BaseStatModifier
                {
                    stat = r.stat,
                    op = r.op,
                    value = v,
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

            if (r.op == ModOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);

            outMods.Add(
                new DerivedStatModifier
                {
                    stat = r.stat,
                    op = r.op,
                    value = v,
                }
            );
        }
    }

    public static void RollSpellModifiers(
        IReadOnlyList<SpellCombatModifierRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<SpellCombatModifier> outMods
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
            var m = r.modifier;

            // Spell rolls should be positive-only.
            float min = Mathf.Max(0f, r.min);
            float max = Mathf.Max(0f, r.max);
            if (m.op == ModOp.Flat && r.roundFlatToInt)
                min = Mathf.Max(1f, min);
            else if (m.op == ModOp.Percent)
                min = Mathf.Max(0.01f, min);

            float v = RollValue(min, max, rng);
            if (m.op == ModOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);

            m.value = v;
            outMods.Add(m);
        }
    }

    private static bool IsAllowedPositiveSpellRollRule(SpellCombatModifierRollRule r)
    {
        // Must have some chance to roll a positive value.
        if (r.weight <= 0)
            return false;

        float max = Mathf.Max(r.max, r.min);
        if (max <= 0f)
            return false;

        return IsAllowedPositiveSpellModifier(r.modifier);
    }

    private static bool IsAllowedPositiveSpellModifier(SpellCombatModifier m)
    {
        // Legacy scope rules are always "more" style bonuses.
        if (m.target == SpellCombatModifierTarget.None)
            return true;

        // Don't roll explicit debuffs.
        switch (m.target)
        {
            case SpellCombatModifierTarget.AttackerWeakenFlatByType:
            case SpellCombatModifierTarget.AttackerWeakenLessPercentByType:
                return false;
        }

        // For Percent rules: block generic *LessPercent targets (except Resistance-by-type which is beneficial).
        if (m.op == ModOp.Percent)
        {
            bool isLess = m.target.ToString().EndsWith("LessPercent", StringComparison.Ordinal);
            if (isLess && m.target != SpellCombatModifierTarget.DefenderResistanceLessPercentByType)
                return false;
        }

        return true;
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

    public static void RollDamageTypes(
        IReadOnlyList<DamageTypeRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return;

        // NOTE: We append into SpellVariableOverride list so multiple roll categories can coexist.

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
            outOverrides.Add(
                new MyGame.Combat.SpellVariableOverride
                {
                    type = MyGame.Combat.SpellVariableOverrideType.DamageType,
                    damageType = r.damageType,
                }
            );
        }
    }

    public static void RollDamageKinds(
        IReadOnlyList<DamageKindRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return;
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
            outOverrides.Add(
                new MyGame.Combat.SpellVariableOverride
                {
                    type = MyGame.Combat.SpellVariableOverrideType.DamageKind,
                    damageKind = r.damageKind,
                }
            );
        }
    }

    public static void RollDamageRanges(
        IReadOnlyList<DamageRangeRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return;
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
            outOverrides.Add(
                new MyGame.Combat.SpellVariableOverride
                {
                    type = MyGame.Combat.SpellVariableOverrideType.DamageRangeType,
                    damageRangeType = r.damageRangeType,
                }
            );
        }
    }

    public static void RollCombatMods(
        IReadOnlyList<CombatModRollRule> table,
        int minRolls,
        int maxRolls,
        bool allowDuplicates,
        System.Random rng,
        List<MyGame.Combat.SpellCombatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null)
            return;

        outMods.Clear();

        if (outOverrides == null)
            return;

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

            if (r.op == ModOp.Flat && r.roundFlatToInt)
                min = Mathf.Max(1f, min);
            else if (r.op == ModOp.Percent)
                min = Mathf.Max(0.01f, min);

            float v = RollValue(min, max, rng);
            if (r.op == ModOp.Flat && r.roundFlatToInt)
                v = Mathf.RoundToInt(v);

            if (r.target == CombatModRollTarget.IgnoreDefence)
            {
                // Ignore defence is a spell override, not a StatModifiers combat mod.
                if (r.op == ModOp.Flat)
                {
                    int flat = Mathf.Max(0, Mathf.RoundToInt(v));
                    if (flat > 0)
                        outOverrides.Add(
                            new MyGame.Combat.SpellVariableOverride
                            {
                                type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefenseFlat,
                                ignoreDefenseFlat = flat,
                            }
                        );
                }
                else if (r.op == ModOp.Percent)
                {
                    int pct = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100);
                    if (pct > 0)
                        outOverrides.Add(
                            new MyGame.Combat.SpellVariableOverride
                            {
                                type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefensePercent,
                                ignoreDefensePercent = pct,
                            }
                        );
                }

                continue;
            }

            var mapped = MapCombatModTarget(r.target, r.op);
            if (mapped == MyGame.Combat.SpellCombatModifierTarget.None)
                continue;

            outMods.Add(
                new MyGame.Combat.SpellCombatModifier
                {
                    target = mapped,
                    scope = MyGame.Combat.SpellModifierScope.Any,
                    op = r.op,
                    value = v,
                }
            );
        }
    }

    private static MyGame.Combat.SpellCombatModifierTarget MapCombatModTarget(
        CombatModRollTarget t,
        ModOp op
    )
    {
        // No more/less selection exposed here; Percent always maps to *MorePercent.
        switch (t)
        {
            case CombatModRollTarget.Damage:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.DamageFlat
                    : MyGame.Combat.SpellCombatModifierTarget.DamageMorePercent;

            case CombatModRollTarget.PhysicalDamage:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.AttackDamageFlat
                    : MyGame.Combat.SpellCombatModifierTarget.PhysicalDamageMorePercent;

            case CombatModRollTarget.MagicDamage:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.MagicDamageFlat
                    : MyGame.Combat.SpellCombatModifierTarget.MagicDamageMorePercent;

            case CombatModRollTarget.SpellBase:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.SpellBaseFlat
                    : MyGame.Combat.SpellCombatModifierTarget.SpellBaseMorePercent;

            case CombatModRollTarget.PhysicalSpellBase:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.PhysicalSpellBaseFlat
                    : MyGame.Combat.SpellCombatModifierTarget.PhysicalSpellBaseMorePercent;

            case CombatModRollTarget.MagicSpellBase:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.MagicSpellBaseFlat
                    : MyGame.Combat.SpellCombatModifierTarget.MagicSpellBaseMorePercent;

            case CombatModRollTarget.PowerScaling:
                // Scaling "Flat" means additive scaling (stored as float in StatModifiers).
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.PowerScalingFlat
                    : MyGame.Combat.SpellCombatModifierTarget.PowerScalingMorePercent;

            case CombatModRollTarget.AttackPowerScaling:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.AttackPowerScalingFlat
                    : MyGame.Combat.SpellCombatModifierTarget.AttackPowerScalingMorePercent;

            case CombatModRollTarget.MagicPowerScaling:
                return op == ModOp.Flat
                    ? MyGame.Combat.SpellCombatModifierTarget.MagicPowerScalingFlat
                    : MyGame.Combat.SpellCombatModifierTarget.MagicPowerScalingMorePercent;

            default:
                return MyGame.Combat.SpellCombatModifierTarget.None;
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
        IReadOnlyList<SpellCombatModifierRollRule> rules,
        HashSet<int> used,
        System.Random rng
    )
    {
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            if (!IsAllowedPositiveSpellRollRule(rules[i]))
                continue;

            total += rules[i].weight;
        }

        if (total <= 0)
            return -1;

        int r = rng.Next(0, total);

        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            if (used != null && used.Contains(i))
                continue;

            if (!IsAllowedPositiveSpellRollRule(rules[i]))
                continue;

            acc += rules[i].weight;
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
        IReadOnlyList<DamageKindRollRule> rules,
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
        IReadOnlyList<DamageRangeRollRule> rules,
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
        IReadOnlyList<DamageTypeRollRule> rules,
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
        IReadOnlyList<DamageTypeRollRule> rules,
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
        IReadOnlyList<DamageKindRollRule> rules,
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
        IReadOnlyList<DamageRangeRollRule> rules,
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

            float max = Mathf.Max(rules[i].min, rules[i].max);
            if (max <= 0f)
                continue;

            // Disallow invalid mappings.
            if (
                rules[i].target != CombatModRollTarget.IgnoreDefence
                && MapCombatModTarget(rules[i].target, rules[i].op)
                    == MyGame.Combat.SpellCombatModifierTarget.None
            )
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

            float max = Mathf.Max(rules[i].min, rules[i].max);
            if (max <= 0f)
                continue;

            if (
                rules[i].target != CombatModRollTarget.IgnoreDefence
                && MapCombatModTarget(rules[i].target, rules[i].op)
                    == MyGame.Combat.SpellCombatModifierTarget.None
            )
                continue;

            acc += w;
            if (r < acc)
                return i;
        }

        return -1;
    }

    private static int PickGroupIndex(
        int pick,
        int wBase,
        int wDerived,
        int wCombat,
        int wKind,
        int wRange,
        int wType
    )
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
        acc += wKind;
        if (pick < acc)
            return 3;
        acc += wRange;
        if (pick < acc)
            return 4;
        return 5;
    }

    private static bool TryRollOneFromGroup(
        EquipmentDefinitionSO def,
        int group,
        System.Random rng,
        HashSet<int> usedBase,
        HashSet<int> usedDerived,
        HashSet<int> usedCombat,
        HashSet<int> usedKind,
        HashSet<int> usedRange,
        HashSet<int> usedType,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.SpellCombatModifier> outCombatMods,
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
            case 3:
                return TryRollOneDamageKind(def.damageKindRollTable, rng, usedKind, outOverrides);
            case 4:
                return TryRollOneDamageRange(
                    def.damageRangeRollTable,
                    rng,
                    usedRange,
                    outOverrides
                );
            case 5:
                return TryRollOneDamageType(def.damageTypeRollTable, rng, usedType, outOverrides);
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
        HashSet<int> usedKind,
        HashSet<int> usedRange,
        HashSet<int> usedType,
        List<BaseStatModifier> outBaseMods,
        List<DerivedStatModifier> outDerivedMods,
        List<MyGame.Combat.SpellCombatModifier> outCombatMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides,
        int[] baseWeights,
        int[] derivedWeights,
        int[] combatWeights,
        int[] kindWeights,
        int[] rangeWeights,
        int[] typeWeights
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
            case 3:
                return TryRollOneDamageKind(
                    def.damageKindRollTable,
                    kindWeights,
                    rng,
                    usedKind,
                    outOverrides
                );
            case 4:
                return TryRollOneDamageRange(
                    def.damageRangeRollTable,
                    rangeWeights,
                    rng,
                    usedRange,
                    outOverrides
                );
            case 5:
                return TryRollOneDamageType(
                    def.damageTypeRollTable,
                    typeWeights,
                    rng,
                    usedType,
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

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
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

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
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

        outMods.Add(
            new BaseStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
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

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
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

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
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

        outMods.Add(
            new DerivedStatModifier
            {
                stat = r.stat,
                op = r.op,
                value = v,
            }
        );

        return true;
    }

    private static bool TryRollOneCombat(
        IReadOnlyList<CombatModRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellCombatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null || outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];

        // Positive-only.
        float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
        float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

        if (r.op == ModOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == ModOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        if (r.target == CombatModRollTarget.IgnoreDefence)
        {
            if (r.op == ModOp.Flat)
            {
                int flat = Mathf.Max(0, Mathf.RoundToInt(v));
                if (flat > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefenseFlat,
                            ignoreDefenseFlat = flat,
                        }
                    );
            }
            else if (r.op == ModOp.Percent)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100);
                if (pct > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefensePercent,
                            ignoreDefensePercent = pct,
                        }
                    );
            }

            return true;
        }

        var mapped = MapCombatModTarget(r.target, r.op);
        if (mapped == MyGame.Combat.SpellCombatModifierTarget.None)
            return false;

        outMods.Add(
            new MyGame.Combat.SpellCombatModifier
            {
                target = mapped,
                scope = MyGame.Combat.SpellModifierScope.Any,
                op = r.op,
                value = v,
            }
        );
        return true;
    }

    private static bool TryRollOneCombat(
        IReadOnlyList<CombatModRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellCombatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null || outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];

        // Positive-only.
        float min = Mathf.Max(0f, Mathf.Min(r.min, r.max));
        float max = Mathf.Max(0f, Mathf.Max(r.min, r.max));

        if (r.op == ModOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == ModOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        if (r.target == CombatModRollTarget.IgnoreDefence)
        {
            if (r.op == ModOp.Flat)
            {
                int flat = Mathf.Max(0, Mathf.RoundToInt(v));
                if (flat > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefenseFlat,
                            ignoreDefenseFlat = flat,
                        }
                    );
            }
            else if (r.op == ModOp.Percent)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100);
                if (pct > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefensePercent,
                            ignoreDefensePercent = pct,
                        }
                    );
            }

            return true;
        }

        var mapped = MapCombatModTarget(r.target, r.op);
        if (mapped == MyGame.Combat.SpellCombatModifierTarget.None)
            return false;

        outMods.Add(
            new MyGame.Combat.SpellCombatModifier
            {
                target = mapped,
                scope = MyGame.Combat.SpellModifierScope.Any,
                op = r.op,
                value = v,
            }
        );
        return true;
    }

    private static bool TryRollOneCombatExact(
        IReadOnlyList<CombatModRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellCombatModifier> outMods,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outMods == null || outOverrides == null)
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

        if (r.op == ModOp.Flat && r.roundFlatToInt)
            min = Mathf.Max(1f, min);
        else if (r.op == ModOp.Percent)
            min = Mathf.Max(0.01f, min);

        float v = RollValue(min, max, rng);
        if (r.op == ModOp.Flat && r.roundFlatToInt)
            v = Mathf.RoundToInt(v);

        if (r.target == CombatModRollTarget.IgnoreDefence)
        {
            if (r.op == ModOp.Flat)
            {
                int flat = Mathf.Max(0, Mathf.RoundToInt(v));
                if (flat > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefenseFlat,
                            ignoreDefenseFlat = flat,
                        }
                    );
            }
            else if (r.op == ModOp.Percent)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100);
                if (pct > 0)
                    outOverrides.Add(
                        new MyGame.Combat.SpellVariableOverride
                        {
                            type = MyGame.Combat.SpellVariableOverrideType.IgnoreDefensePercent,
                            ignoreDefensePercent = pct,
                        }
                    );
            }

            return true;
        }

        var mapped = MapCombatModTarget(r.target, r.op);
        if (mapped == MyGame.Combat.SpellCombatModifierTarget.None)
            return false;

        outMods.Add(
            new MyGame.Combat.SpellCombatModifier
            {
                target = mapped,
                scope = MyGame.Combat.SpellModifierScope.Any,
                op = r.op,
                value = v,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageKind(
        IReadOnlyList<DamageKindRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageKind,
                damageKind = r.damageKind,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageKind(
        IReadOnlyList<DamageKindRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageKind,
                damageKind = r.damageKind,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageKindExact(
        IReadOnlyList<DamageKindRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
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
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageKind,
                damageKind = r.damageKind,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageRange(
        IReadOnlyList<DamageRangeRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageRangeType,
                damageRangeType = r.damageRangeType,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageRange(
        IReadOnlyList<DamageRangeRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageRangeType,
                damageRangeType = r.damageRangeType,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageRangeExact(
        IReadOnlyList<DamageRangeRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
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
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageRangeType,
                damageRangeType = r.damageRangeType,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageType(
        IReadOnlyList<DamageTypeRollRule> table,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageType,
                damageType = r.damageType,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageType(
        IReadOnlyList<DamageTypeRollRule> table,
        int[] weights,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
            return false;
        if (table == null || table.Count == 0)
            return false;

        int idx = PickWeightedIndex(table, weights, used, rng);
        if (idx < 0)
            return false;

        used?.Add(idx);
        var r = table[idx];
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageType,
                damageType = r.damageType,
            }
        );
        return true;
    }

    private static bool TryRollOneDamageTypeExact(
        IReadOnlyList<DamageTypeRollRule> table,
        int idx,
        System.Random rng,
        HashSet<int> used,
        List<MyGame.Combat.SpellVariableOverride> outOverrides
    )
    {
        if (outOverrides == null)
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
        outOverrides.Add(
            new MyGame.Combat.SpellVariableOverride
            {
                type = MyGame.Combat.SpellVariableOverrideType.DamageType,
                damageType = r.damageType,
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
        if (
            r.target != CombatModRollTarget.IgnoreDefence
            && MapCombatModTarget(r.target, r.op) == MyGame.Combat.SpellCombatModifierTarget.None
        )
            return false;
        return true;
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
            if (r.weight <= 0)
                continue;
            float max = Mathf.Max(r.min, r.max);
            if (max <= 0f)
                continue;
            if (
                r.target != CombatModRollTarget.IgnoreDefence
                && MapCombatModTarget(r.target, r.op)
                    == MyGame.Combat.SpellCombatModifierTarget.None
            )
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
            case DamageKindRollRule r:
                return r.weight > 0;
            case DamageRangeRollRule r:
                return r.weight > 0;
            case DamageTypeRollRule r:
                return r.weight > 0;
            default:
                return true;
        }
    }
}
