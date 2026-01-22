using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Inventory;
using UnityEngine;

public static class EquipmentBonusLinesBuilder
{
    private readonly struct CombatModLineKey : IEquatable<CombatModLineKey>
    {
        public readonly string name;
        public readonly string selector;

        public CombatModLineKey(string name, string selector)
        {
            this.name = name;
            this.selector = selector;
        }

        public bool Equals(CombatModLineKey other) =>
            string.Equals(name, other.name, StringComparison.Ordinal)
            && string.Equals(selector, other.selector, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CombatModLineKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (name != null ? StringComparer.Ordinal.GetHashCode(name) : 0);
                hash =
                    (hash * 31)
                    + (selector != null ? StringComparer.Ordinal.GetHashCode(selector) : 0);
                return hash;
            }
        }
    }

    private struct CombatModTotals
    {
        public int flat;
        public float pct;
    }

    public static List<string> BuildCombatBonusLines(PlayerEquipment equipment)
    {
        var lines = new List<string>(48);
        if (equipment == null)
            return lines;

        var slots = new[]
        {
            MyName.Equipment.EquipmentSlot.MainHand,
            MyName.Equipment.EquipmentSlot.Offhand,
            MyName.Equipment.EquipmentSlot.Ranged,
            MyName.Equipment.EquipmentSlot.Head,
            MyName.Equipment.EquipmentSlot.Chest,
            MyName.Equipment.EquipmentSlot.Legs,
            MyName.Equipment.EquipmentSlot.Hands,
            MyName.Equipment.EquipmentSlot.Feet,
            MyName.Equipment.EquipmentSlot.Shoulders,
            MyName.Equipment.EquipmentSlot.Belt,
            MyName.Equipment.EquipmentSlot.Ring,
            MyName.Equipment.EquipmentSlot.Amulet,
            MyName.Equipment.EquipmentSlot.Trinket,
            MyName.Equipment.EquipmentSlot.Cape,
            MyName.Equipment.EquipmentSlot.Jewelry,
            MyName.Equipment.EquipmentSlot.Gloves,
        };

        var damageOverrideLines = new List<string>(24);

        // Aggregate combat modifiers across all equipped items.
        // Keyed by a normalized "display name" + selector so Flat/% combine into a single line.
        var totalsByKey = new Dictionary<CombatModLineKey, CombatModTotals>(32);
        int ignoreDefFlat = 0;
        int ignoreDefPct = 0;

        for (int s = 0; s < slots.Length; s++)
        {
            if (!equipment.TryGetEquippedInstance(slots[s], out var inst))
                continue;

            if (inst?.rolledSpellOverrides != null && inst.rolledSpellOverrides.Count > 0)
            {
                for (int i = 0; i < inst.rolledSpellOverrides.Count; i++)
                {
                    var o = inst.rolledSpellOverrides[i];
                    switch (o.type)
                    {
                        case SpellVariableOverrideType.DamageKind:
                            damageOverrideLines.Add(
                                $"- {FormatSlotPrefix(slots[s])}Damage Kind: {o.damageKind}"
                            );
                            break;

                        case SpellVariableOverrideType.DamageRangeType:
                            damageOverrideLines.Add(
                                $"- {FormatSlotPrefix(slots[s])}Range: {o.damageRangeType}"
                            );
                            break;

                        case SpellVariableOverrideType.DamageType:
                            damageOverrideLines.Add(
                                $"- {FormatSlotPrefix(slots[s])}Damage Type: {o.damageType}"
                            );
                            break;

                        case SpellVariableOverrideType.IgnoreDefenseFlat:
                            if (o.ignoreDefenseFlat > 0)
                                ignoreDefFlat += o.ignoreDefenseFlat;
                            break;

                        case SpellVariableOverrideType.IgnoreDefensePercent:
                            if (o.ignoreDefensePercent > 0)
                                ignoreDefPct += o.ignoreDefensePercent;
                            break;
                    }
                }
            }

            if (inst?.rolledSpellMods != null && inst.rolledSpellMods.Count > 0)
            {
                for (int i = 0; i < inst.rolledSpellMods.Count; i++)
                {
                    var mod = inst.rolledSpellMods[i];
                    if (!IsPositiveSpellRoll(mod))
                        continue;

                    string name = GetNormalizedCombatModName(mod);
                    string selector = FormatSelector(mod);
                    var key = new CombatModLineKey(name, selector);

                    totalsByKey.TryGetValue(key, out var totals);

                    if (mod.op == ModOp.Flat)
                    {
                        int add = Mathf.RoundToInt(mod.value);
                        if (add > 0)
                            totals.flat += add;
                    }
                    else if (mod.op == ModOp.Percent)
                    {
                        if (mod.value > 0.0001f)
                            totals.pct += mod.value;
                    }

                    totalsByKey[key] = totals;
                }
            }
        }

        if (damageOverrideLines.Count > 0)
        {
            lines.Add("Damage Overrides:");
            lines.AddRange(damageOverrideLines);
        }

        var otherCombatLines = new List<string>(48);

        if (ignoreDefFlat > 0 || ignoreDefPct > 0)
            otherCombatLines.Add(
                FormatCombinedFlatAndPctLine(
                    name: "Ignore Defence",
                    selector: null,
                    flat: ignoreDefFlat,
                    pct: ignoreDefPct
                )
            );

        foreach (var kvp in totalsByKey)
        {
            int flat = kvp.Value.flat;
            float pct = kvp.Value.pct;
            if (flat <= 0 && pct <= 0.0001f)
                continue;

            otherCombatLines.Add(
                FormatCombinedFlatAndPctLine(kvp.Key.name, kvp.Key.selector, flat, pct)
            );
        }

        otherCombatLines.Sort(StringComparer.Ordinal);

        if (otherCombatLines.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            lines.AddRange(otherCombatLines);
        }

        return lines;
    }

    private static string FormatCombinedFlatAndPctLine(
        string name,
        string selector,
        int flat,
        float pct
    )
    {
        string left = flat > 0 ? $"+{flat}" : null;
        string right = pct > 0.0001f ? $"{pct:0.##}%" : null;

        string values;
        if (left != null && right != null)
            values = $"{left} / {right}";
        else
            values = left ?? right ?? "0";

        if (!string.IsNullOrWhiteSpace(selector))
            return $"{name} ({selector}) {values}";

        return $"{name} {values}";
    }

    private static string GetNormalizedCombatModName(SpellCombatModifier m)
    {
        if (m.target == SpellCombatModifierTarget.None)
            return CharacterDashboardText.NiceEnum(m.scope.ToString());

        switch (m.target)
        {
            case SpellCombatModifierTarget.DamageFlat:
            case SpellCombatModifierTarget.DamageMorePercent:
            case SpellCombatModifierTarget.DamageLessPercent:
                return "Damage";

            // Flat physical uses AttackDamageFlat; percent uses PhysicalDamageMorePercent.
            case SpellCombatModifierTarget.AttackDamageFlat:
            case SpellCombatModifierTarget.PhysicalDamageMorePercent:
                return "Physical Damage";

            case SpellCombatModifierTarget.MagicDamageFlat:
            case SpellCombatModifierTarget.MagicDamageMorePercent:
                return "Magic Damage";

            case SpellCombatModifierTarget.SpellBaseFlat:
            case SpellCombatModifierTarget.SpellBaseMorePercent:
            case SpellCombatModifierTarget.SpellBaseLessPercent:
                return "Spell Base";

            case SpellCombatModifierTarget.PhysicalSpellBaseFlat:
            case SpellCombatModifierTarget.PhysicalSpellBaseMorePercent:
            case SpellCombatModifierTarget.PhysicalSpellBaseLessPercent:
                return "Physical Spell Base";

            case SpellCombatModifierTarget.MagicSpellBaseFlat:
            case SpellCombatModifierTarget.MagicSpellBaseMorePercent:
            case SpellCombatModifierTarget.MagicSpellBaseLessPercent:
                return "Magic Spell Base";

            case SpellCombatModifierTarget.PowerScalingFlat:
            case SpellCombatModifierTarget.PowerScalingMorePercent:
            case SpellCombatModifierTarget.PowerScalingLessPercent:
                return "Power Scaling";

            case SpellCombatModifierTarget.AttackPowerScalingFlat:
            case SpellCombatModifierTarget.AttackPowerScalingMorePercent:
            case SpellCombatModifierTarget.AttackPowerScalingLessPercent:
                return "Attack Power Scaling";

            case SpellCombatModifierTarget.MagicPowerScalingFlat:
            case SpellCombatModifierTarget.MagicPowerScalingMorePercent:
            case SpellCombatModifierTarget.MagicPowerScalingLessPercent:
                return "Magic Power Scaling";
        }

        // Fallback: strip common suffixes so Flat and Percent variants combine.
        string raw = m.target.ToString();
        if (raw.EndsWith("MorePercent", StringComparison.Ordinal))
            raw = raw.Substring(0, raw.Length - "MorePercent".Length);
        else if (raw.EndsWith("LessPercent", StringComparison.Ordinal))
            raw = raw.Substring(0, raw.Length - "LessPercent".Length);
        else if (raw.EndsWith("Flat", StringComparison.Ordinal))
            raw = raw.Substring(0, raw.Length - "Flat".Length);

        return CharacterDashboardText.NiceEnum(raw);
    }

    public static List<string> BuildEquipmentBonusLines(PlayerEquipment equipment)
    {
        var lines = new List<string>(64);
        if (equipment == null)
            return lines;

        var slots = new[]
        {
            MyName.Equipment.EquipmentSlot.MainHand,
            MyName.Equipment.EquipmentSlot.Offhand,
            MyName.Equipment.EquipmentSlot.Ranged,
            MyName.Equipment.EquipmentSlot.Head,
            MyName.Equipment.EquipmentSlot.Chest,
            MyName.Equipment.EquipmentSlot.Legs,
            MyName.Equipment.EquipmentSlot.Hands,
            MyName.Equipment.EquipmentSlot.Feet,
            MyName.Equipment.EquipmentSlot.Shoulders,
            MyName.Equipment.EquipmentSlot.Belt,
            MyName.Equipment.EquipmentSlot.Ring,
            MyName.Equipment.EquipmentSlot.Amulet,
            MyName.Equipment.EquipmentSlot.Trinket,
            MyName.Equipment.EquipmentSlot.Cape,
            MyName.Equipment.EquipmentSlot.Jewelry,
            MyName.Equipment.EquipmentSlot.Gloves,
        };

        // Base/derived rolls (shown as-is)
        var baseModLines = new List<string>(16);
        var derivedModLines = new List<string>(16);

        // Aggregate base stats into one line per stat (sum flat + sum %).
        var baseFlatByStat = new int[Enum.GetValues(typeof(BaseStatType)).Length];
        var basePctByStat = new float[Enum.GetValues(typeof(BaseStatType)).Length];

        // Spell rolls (shown per-roll, not aggregated)
        var otherCombatRollLines = new List<string>(32);
        var damageTypeLines = new List<string>(32);

        for (int s = 0; s < slots.Length; s++)
        {
            if (!equipment.TryGetEquippedInstance(slots[s], out var inst))
                continue;

            string slotPrefix = FormatSlotPrefix(slots[s]);

            if (inst?.rolledSpellMods != null && inst.rolledSpellMods.Count > 0)
            {
                for (int i = 0; i < inst.rolledSpellMods.Count; i++)
                {
                    var mod = inst.rolledSpellMods[i];

                    // 1) Don't show penalties in the "spell rolls" list.
                    if (!IsPositiveSpellRoll(mod))
                        continue;

                    // 2) Exclude spell rolls that duplicate derived-stat rolls (ex: Magic Power flat/%).
                    if (IsDuplicateWithDerivedStats(mod))
                        continue;

                    string line = $"- {slotPrefix}{FormatSpellRoll(mod)}";

                    if (IsDamageTypeRelated(mod))
                        damageTypeLines.Add(line);
                    else if (IsDamageRangeRelated(mod))
                        otherCombatRollLines.Add(line);
                    else
                        otherCombatRollLines.Add(line);
                }
            }

            if (inst?.rolledBaseStatMods != null && inst.rolledBaseStatMods.Count > 0)
                AccumulateBaseStatMods(baseFlatByStat, basePctByStat, inst.rolledBaseStatMods);

            if (inst?.rolledDerivedStatMods != null && inst.rolledDerivedStatMods.Count > 0)
                AppendDerivedStatMods(derivedModLines, inst.rolledDerivedStatMods);

            if (inst?.rolledSpellOverrides == null || inst.rolledSpellOverrides.Count == 0)
                continue;

            for (int i = 0; i < inst.rolledSpellOverrides.Count; i++)
            {
                var o = inst.rolledSpellOverrides[i];
                switch (o.type)
                {
                    case SpellVariableOverrideType.IgnoreDefenseFlat:
                        if (o.ignoreDefenseFlat > 0)
                            otherCombatRollLines.Add(
                                $"- {slotPrefix}Ignore Defence (flat): +{o.ignoreDefenseFlat}"
                            );
                        break;

                    case SpellVariableOverrideType.IgnoreDefensePercent:
                        if (o.ignoreDefensePercent > 0)
                            otherCombatRollLines.Add(
                                $"- {slotPrefix}Ignore Defence (%): +{o.ignoreDefensePercent}%"
                            );
                        break;

                    case SpellVariableOverrideType.DamageKind:
                        otherCombatRollLines.Add(
                            $"- {slotPrefix}Damage Kind override: {o.damageKind}"
                        );
                        break;

                    case SpellVariableOverrideType.DamageRangeType:
                        otherCombatRollLines.Add(
                            $"- {slotPrefix}Range override: {o.damageRangeType}"
                        );
                        break;

                    case SpellVariableOverrideType.DamageType:
                        damageTypeLines.Add($"- {slotPrefix}Damage Type override: {o.damageType}");
                        break;
                }
            }
        }

        BuildBaseStatLines(baseModLines, baseFlatByStat, basePctByStat);

        if (baseModLines.Count > 0)
        {
            lines.Add("Stats:");
            lines.AddRange(baseModLines);
        }

        if (derivedModLines.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            lines.Add("Derived Stats:");
            lines.AddRange(derivedModLines);
        }

        if (otherCombatRollLines.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            lines.Add("Other Combat Rolls:");
            lines.AddRange(otherCombatRollLines);
        }

        if (damageTypeLines.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            lines.Add("Damage Types:");
            lines.AddRange(damageTypeLines);
        }

        return lines;
    }

    private static string FormatSlotPrefix(MyName.Equipment.EquipmentSlot slot)
    {
        if (slot == MyName.Equipment.EquipmentSlot.None)
            return string.Empty;
        string nice = CharacterDashboardText.NiceEnum(slot.ToString());
        return $"[{nice}] ";
    }

    private static bool IsPositiveSpellRoll(SpellCombatModifier m)
    {
        // Only show "bonuses" in the UI.
        if (m.op == ModOp.Flat)
            return m.value > 0.0001f;

        if (m.op == ModOp.Percent)
        {
            if (m.value <= 0.0001f)
                return false;

            // By default treat Less% targets as penalties (don't show), except "Resistance" which is beneficial.
            if (IsLessPercentTarget(m.target))
                return m.target == SpellCombatModifierTarget.DefenderResistanceLessPercentByType;

            return true;
        }

        return false;
    }

    private static bool IsDuplicateWithDerivedStats(SpellCombatModifier m)
    {
        // These targets overlap with derived stats displayed above (Attack/Magic Power, Speed, Defence).
        // Showing them again under spell rolls feels like a duplicate.
        switch (m.target)
        {
            case SpellCombatModifierTarget.PowerFlat:
            case SpellCombatModifierTarget.PowerMorePercent:
            case SpellCombatModifierTarget.PowerLessPercent:

            case SpellCombatModifierTarget.AttackPowerFlat:
            case SpellCombatModifierTarget.AttackPowerMorePercent:
            case SpellCombatModifierTarget.AttackPowerLessPercent:

            case SpellCombatModifierTarget.MagicPowerFlat:
            case SpellCombatModifierTarget.MagicPowerMorePercent:
            case SpellCombatModifierTarget.MagicPowerLessPercent:

            case SpellCombatModifierTarget.AttackSpeedFlat:
            case SpellCombatModifierTarget.AttackSpeedMorePercent:
            case SpellCombatModifierTarget.AttackSpeedLessPercent:

            case SpellCombatModifierTarget.CastingSpeedFlat:
            case SpellCombatModifierTarget.CastingSpeedMorePercent:
            case SpellCombatModifierTarget.CastingSpeedLessPercent:

            case SpellCombatModifierTarget.DefenceFlat:
            case SpellCombatModifierTarget.DefenceMorePercent:
            case SpellCombatModifierTarget.DefenceLessPercent:

            case SpellCombatModifierTarget.PhysicalDefenseFlat:
            case SpellCombatModifierTarget.PhysicalDefenceMorePercent:
            case SpellCombatModifierTarget.PhysicalDefenceLessPercent:

            case SpellCombatModifierTarget.MagicDefenseFlat:
            case SpellCombatModifierTarget.MagicDefenceMorePercent:
            case SpellCombatModifierTarget.MagicDefenceLessPercent:
                return true;
        }

        return false;
    }

    private static bool IsDamageTypeRelated(SpellCombatModifier m)
    {
        if (m.scope == SpellModifierScope.DamageType)
            return true;

        switch (m.target)
        {
            case SpellCombatModifierTarget.AttackerBonusFlatByType:
            case SpellCombatModifierTarget.AttackerBonusMorePercentByType:
            case SpellCombatModifierTarget.DefenderVulnerabilityFlatByType:
            case SpellCombatModifierTarget.DefenderVulnerabilityMorePercentByType:
            case SpellCombatModifierTarget.DefenderResistanceFlatByType:
            case SpellCombatModifierTarget.DefenderResistanceLessPercentByType:
            case SpellCombatModifierTarget.AttackerWeakenFlatByType:
            case SpellCombatModifierTarget.AttackerWeakenLessPercentByType:
                return true;
        }

        return false;
    }

    private static bool IsDamageRangeRelated(SpellCombatModifier m)
    {
        if (m.scope == SpellModifierScope.DamageRangeType)
            return true;

        switch (m.target)
        {
            case SpellCombatModifierTarget.AttackerRangeBonusFlatByRange:
            case SpellCombatModifierTarget.AttackerRangeBonusMorePercentByRange:
                return true;
        }

        return false;
    }

    private static bool IsLessPercentTarget(SpellCombatModifierTarget t)
    {
        // Covers all generic *LessPercent targets plus type-based *LessPercent.
        return t.ToString().EndsWith("LessPercent", StringComparison.Ordinal);
    }

    private static string FormatSpellRoll(SpellCombatModifier m)
    {
        // Prefer new target naming; fall back to legacy scope naming.
        string name =
            m.target != SpellCombatModifierTarget.None
                ? CharacterDashboardText.NiceEnum(m.target.ToString())
                : CharacterDashboardText.NiceEnum(m.scope.ToString());

        string selector = FormatSelector(m);
        string value = FormatSpellRollValue(m);

        if (!string.IsNullOrWhiteSpace(selector))
            return $"{name} ({selector}): {value}";
        return $"{name}: {value}";
    }

    private static string FormatSelector(SpellCombatModifier m)
    {
        // New target-based rules still store the selector in damageType/damageRangeType.
        if (m.scope == SpellModifierScope.DamageKind)
            return CharacterDashboardText.NiceEnum(m.damageKind.ToString());
        if (m.scope == SpellModifierScope.DamageType)
            return CharacterDashboardText.NiceEnum(m.damageType.ToString());
        if (m.scope == SpellModifierScope.DamageRangeType)
            return CharacterDashboardText.NiceEnum(m.damageRangeType.ToString());

        if (IsDamageTypeRelated(m))
            return CharacterDashboardText.NiceEnum(m.damageType.ToString());
        if (IsDamageRangeRelated(m))
            return CharacterDashboardText.NiceEnum(m.damageRangeType.ToString());

        return null;
    }

    private static string FormatSpellRollValue(SpellCombatModifier m)
    {
        if (m.op == ModOp.Flat)
        {
            int flat = Mathf.RoundToInt(m.value);
            return $"+{flat}";
        }

        // Percent stored as "10" => 10%.
        float pct = m.value;

        if (m.target == SpellCombatModifierTarget.DefenderResistanceLessPercentByType)
            return $"+{pct:0.##}% (resist)";

        return $"+{pct:0.##}%";
    }

    private static void AccumulateBaseStatMods(
        int[] flatByStat,
        float[] pctByStat,
        List<BaseStatModifier> mods
    )
    {
        if (flatByStat == null || pctByStat == null)
            return;
        if (mods == null)
            return;
        for (int i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];

            int idx = (int)mod.stat;
            if (idx < 0 || idx >= flatByStat.Length || idx >= pctByStat.Length)
                continue;

            if (mod.op == ModOp.Flat)
                flatByStat[idx] += Mathf.RoundToInt(mod.value);
            else if (mod.op == ModOp.Percent)
                pctByStat[idx] += mod.value;
        }
    }

    private static void BuildBaseStatLines(List<string> lines, int[] flatByStat, float[] pctByStat)
    {
        if (lines == null || flatByStat == null || pctByStat == null)
            return;

        var all = (BaseStatType[])Enum.GetValues(typeof(BaseStatType));
        for (int i = 0; i < all.Length; i++)
        {
            int idx = (int)all[i];
            if (idx < 0 || idx >= flatByStat.Length || idx >= pctByStat.Length)
                continue;

            int flat = flatByStat[idx];
            float pct = pctByStat[idx];

            if (flat == 0 && Mathf.Abs(pct) < 0.0001f)
                continue;

            lines.Add(
                $"- {CharacterDashboardText.NiceEnum(all[i].ToString())}: {FormatFlatAndPct(flat, pct)}"
            );
        }
    }

    private static string FormatFlatAndPct(int flat, float pct)
    {
        string flatPart = flat != 0 ? $"{(flat > 0 ? "+" : "")}{flat}" : null;
        string pctPart = Mathf.Abs(pct) >= 0.0001f ? $"{pct:+0.##;-0.##}%" : null;

        if (flatPart != null && pctPart != null)
            return $"{flatPart} & {pctPart}";
        return flatPart ?? pctPart ?? "0";
    }

    private static void AppendDerivedStatMods(List<string> lines, List<DerivedStatModifier> mods)
    {
        if (mods == null)
            return;
        for (int i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            lines.Add(
                $"- {CharacterDashboardText.NiceEnum(mod.stat.ToString())} {CharacterDashboardText.FormatModValue(mod.op, mod.value)}"
            );
        }
    }
}
