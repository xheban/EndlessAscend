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

            if (inst?.rolledCombatStatMods != null && inst.rolledCombatStatMods.Count > 0)
            {
                for (int i = 0; i < inst.rolledCombatStatMods.Count; i++)
                {
                    var mod = inst.rolledCombatStatMods[i];
                    if (!IsPositiveCombatStatRoll(mod))
                        continue;

                    string name = GetCombatModName(mod);
                    string selector = FormatCombatSelector(mod);
                    var key = new CombatModLineKey(name, selector);

                    totalsByKey.TryGetValue(key, out var totals);

                    if (mod.op == EffectOp.Flat)
                    {
                        int add = Mathf.RoundToInt(mod.value);
                        if (add > 0)
                            totals.flat += add;
                    }
                    else if (mod.op == EffectOp.Percent)
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
                FormatCombinedFlatAndPctLine(
                    kvp.Key.name,
                    kvp.Key.selector,
                    flat,
                    pct,
                    flatIsPercentPoints: IsPowerScalingName(kvp.Key.name)
                )
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
        float pct,
        bool flatIsPercentPoints = false
    )
    {
        string left = flat > 0
            ? flatIsPercentPoints
                ? $"+{flat}%"
                : $"+{flat}"
            : null;
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

    private static string GetCombatModName(CombatStatModifier m)
    {
        switch (m.stat)
        {
            case EffectStat.DamageAll:
                return "Damage";
            case EffectStat.DamagePhysical:
                return "Physical Damage";
            case EffectStat.DamageMagic:
                return "Magic Damage";
            case EffectStat.SpellBaseAll:
                return "Spell Base";
            case EffectStat.SpellBasePhysical:
                return "Physical Spell Base";
            case EffectStat.SpellBaseMagic:
                return "Magic Spell Base";
            case EffectStat.PowerScalingAll:
                return "Power Scaling";
            case EffectStat.PowerScalingPhysical:
                return "Attack Power Scaling";
            case EffectStat.PowerScalingMagic:
                return "Magic Power Scaling";
            case EffectStat.AttackerBonusByType:
                return "Attacker Bonus";
            case EffectStat.DefenderVulnerabilityByType:
                return "Defender Vulnerability";
            case EffectStat.DefenderResistByType:
                return "Defender Resist";
            case EffectStat.AttackerWeakenByType:
                return "Attacker Weaken";
            case EffectStat.MeleeDamageBonus:
                return "Melee Damage Bonus";
            case EffectStat.RangedDamageBonus:
                return "Ranged Damage Bonus";
            case EffectStat.DefenceAll:
                return "Defence";
            case EffectStat.PowerAll:
                return "Power";
        }

        return CharacterDashboardText.NiceEnum(m.stat.ToString());
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

            if (inst?.rolledCombatStatMods != null && inst.rolledCombatStatMods.Count > 0)
            {
                for (int i = 0; i < inst.rolledCombatStatMods.Count; i++)
                {
                    var mod = inst.rolledCombatStatMods[i];

                    // 1) Don't show penalties in the "spell rolls" list.
                    if (!IsPositiveCombatStatRoll(mod))
                        continue;

                    string line = $"- {slotPrefix}{FormatCombatStatRoll(mod)}";

                    if (IsCombatStatTypeRelated(mod))
                        damageTypeLines.Add(line);
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

    private static bool IsPositiveCombatStatRoll(CombatStatModifier m)
    {
        if (m.op == EffectOp.Flat)
            return m.value > 0;
        if (m.op == EffectOp.Percent)
            return m.value > 0;
        return true;
    }

    private static bool IsCombatStatTypeRelated(CombatStatModifier m)
    {
        switch (m.stat)
        {
            case EffectStat.AttackerBonusByType:
            case EffectStat.DefenderVulnerabilityByType:
            case EffectStat.DefenderResistByType:
            case EffectStat.AttackerWeakenByType:
                return true;
            default:
                return false;
        }
    }

    private static string FormatCombatStatRoll(CombatStatModifier m)
    {
        string name = GetCombatModName(m);
        string selector = FormatCombatSelector(m);
        string value = FormatCombatStatValue(m);

        if (!string.IsNullOrWhiteSpace(selector))
            return $"{name} ({selector}): {value}";
        return $"{name}: {value}";
    }

    private static string FormatCombatSelector(CombatStatModifier m)
    {
        if (!IsCombatStatTypeRelated(m))
            return null;
        if (m.damageType == DamageType.None)
            return null;
        return CharacterDashboardText.NiceEnum(m.damageType.ToString());
    }

    private static string FormatCombatStatValue(CombatStatModifier m)
    {
        if (m.op == EffectOp.Flat)
        {
            string suffix = IsPowerScalingStat(m.stat) ? "%" : string.Empty;
            return $"{(m.value >= 0 ? "+" : "")}{m.value}{suffix}";
        }

        if (m.op == EffectOp.Percent)
            return $"{(m.value >= 0 ? "+" : "")}{m.value:0.##}%";

        return m.value.ToString();
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

    private static bool IsPowerScalingName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.IndexOf("Power Scaling", StringComparison.Ordinal) >= 0;
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
