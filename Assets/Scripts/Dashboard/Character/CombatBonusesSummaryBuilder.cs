using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Inventory;
using UnityEngine;

public static class CombatBonusesSummaryBuilder
{
    public static List<string> Build(PlayerEquipment equipment, DerivedCombatStats derived)
    {
        var lines = new List<string>(32);

        Aggregate(equipment, out StatModifiers m, out int ignoreDefFlat, out int ignoreDefPct);

        // Derived stats (already include: base stats + class/spec + rolled derived mods)
        AddDerivedWithMods(
            lines,
            name: "Attack Power",
            baseValue: derived.attackPower,
            flatBonus: m.attackPowerFlat + m.powerFlat,
            pctBonus: MultToPct(m.AttackPowerMultFinal * m.PowerMultFinal)
        );

        AddDerivedWithMods(
            lines,
            name: "Magic Power",
            baseValue: derived.magicPower,
            flatBonus: m.magicPowerFlat + m.powerFlat,
            pctBonus: MultToPct(m.MagicPowerMultFinal * m.PowerMultFinal)
        );

        AddDerivedWithMods(
            lines,
            name: "Attack Speed",
            baseValue: derived.attackSpeed,
            flatBonus: m.attackSpeedFlat,
            pctBonus: MultToPct(m.AttackSpeedMultFinal)
        );

        AddDerivedWithMods(
            lines,
            name: "Casting Speed",
            baseValue: derived.castSpeed,
            flatBonus: m.castingSpeedFlat,
            pctBonus: MultToPct(m.CastingSpeedMultFinal)
        );

        AddDerivedWithMods(
            lines,
            name: "Physical Defence",
            baseValue: derived.physicalDefense,
            flatBonus: m.physicalDefenseFlat + m.defenceFlat,
            pctBonus: MultToPct(m.PhysicalDefenceMultFinal * m.DefenceMultFinal)
        );

        AddDerivedWithMods(
            lines,
            name: "Magic Defence",
            baseValue: derived.magicalDefense,
            flatBonus: m.magicDefenseFlat + m.defenceFlat,
            pctBonus: MultToPct(m.MagicDefenceMultFinal * m.DefenceMultFinal)
        );

        // Other combat modifiers (not part of derived stats)
        AddModsOnly(
            lines,
            "Damage",
            flatBonus: m.damageFlat,
            pctBonus: MultToPct(m.DamageMultFinal)
        );

        AddModsOnly(
            lines,
            "Physical Damage",
            flatBonus: m.attackDamageFlat,
            pctBonus: MultToPct(m.PhysicalDamageMultFinal)
        );

        AddModsOnly(
            lines,
            "Magic Damage",
            flatBonus: m.magicDamageFlat,
            pctBonus: MultToPct(m.MagicDamageMultFinal)
        );

        AddModsOnly(
            lines,
            "Spell Base",
            flatBonus: m.spellBaseFlat,
            pctBonus: MultToPct(m.SpellBaseMultFinal)
        );

        AddModsOnly(
            lines,
            "Physical Spell Base",
            flatBonus: m.physicalSpellBaseFlat,
            pctBonus: MultToPct(m.PhysicalSpellBaseMultFinal)
        );

        AddModsOnly(
            lines,
            "Magic Spell Base",
            flatBonus: m.magicSpellBaseFlat,
            pctBonus: MultToPct(m.MagicSpellBaseMultFinal)
        );

        AddModsOnly(
            lines,
            "Power Scaling",
            flatBonus: Mathf.RoundToInt(m.powerScalingFlat * 100f),
            pctBonus: MultToPct(m.PowerScalingMultFinal),
            flatIsPercentPoints: true
        );

        AddModsOnly(
            lines,
            "Attack Power Scaling",
            flatBonus: Mathf.RoundToInt(m.attackPowerScalingFlat * 100f),
            pctBonus: MultToPct(m.AttackPowerScalingMultFinal),
            flatIsPercentPoints: true
        );

        AddModsOnly(
            lines,
            "Magic Power Scaling",
            flatBonus: Mathf.RoundToInt(m.magicPowerScalingFlat * 100f),
            pctBonus: MultToPct(m.MagicPowerScalingMultFinal),
            flatIsPercentPoints: true
        );

        if (ignoreDefFlat != 0 || ignoreDefPct != 0)
        {
            lines.Add(FormatLine("Ignore Defence", ignoreDefFlat, ignoreDefPct));
        }

        // Remove lines that have no meaningful bonuses (keeps list tight).
        lines.RemoveAll(string.IsNullOrWhiteSpace);
        return lines;
    }

    private static void Aggregate(
        PlayerEquipment equipment,
        out StatModifiers modifiers,
        out int ignoreDefFlat,
        out int ignoreDefPct
    )
    {
        modifiers = new StatModifiers();
        ignoreDefFlat = 0;
        ignoreDefPct = 0;

        if (equipment == null)
            return;

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

        for (int s = 0; s < slots.Length; s++)
        {
            if (!equipment.TryGetEquippedInstance(slots[s], out var inst))
                continue;

            if (inst?.rolledSpellMods != null && inst.rolledSpellMods.Count > 0)
                SpellCombatModifierApplier.ApplyAll(modifiers, inst.rolledSpellMods);

            if (inst?.rolledSpellOverrides == null || inst.rolledSpellOverrides.Count == 0)
                continue;

            for (int i = 0; i < inst.rolledSpellOverrides.Count; i++)
            {
                var o = inst.rolledSpellOverrides[i];
                switch (o.type)
                {
                    case SpellVariableOverrideType.IgnoreDefenseFlat:
                        ignoreDefFlat += o.ignoreDefenseFlat;
                        break;
                    case SpellVariableOverrideType.IgnoreDefensePercent:
                        ignoreDefPct += o.ignoreDefensePercent;
                        break;
                }
            }
        }
    }

    private static void AddDerivedWithMods(
        List<string> lines,
        string name,
        int baseValue,
        int flatBonus,
        float pctBonus
    )
    {
        // Always show derived stat line (even if bonuses are 0) because user asked for "final merge".
        lines.Add($"{name}: {baseValue} | {FormatBonus(flatBonus, pctBonus)}");
    }

    private static void AddModsOnly(
        List<string> lines,
        string name,
        int flatBonus,
        float pctBonus,
        bool flatIsPercentPoints = false
    )
    {
        if (flatBonus == 0 && Mathf.Abs(pctBonus) < 0.0001f)
            return;

        if (flatIsPercentPoints)
        {
            // For scaling flats we store as percent points *100 (ex: 0.15 -> 15)
            float flatPctPoints = flatBonus / 100f;
            lines.Add($"{name}: {FormatBonusFloat(flatPctPoints, pctBonus)}");
            return;
        }

        lines.Add(FormatLine(name, flatBonus, pctBonus));
    }

    private static string FormatLine(string name, int flatBonus, float pctBonus) =>
        $"{name}: {FormatBonus(flatBonus, pctBonus)}";

    private static string FormatBonus(int flatBonus, float pctBonus)
    {
        string flat = flatBonus != 0 ? $"{(flatBonus > 0 ? "+" : "")}{flatBonus}" : null;
        string pct = Mathf.Abs(pctBonus) >= 0.0001f ? $"{pctBonus:+0.##;-0.##}%" : null;

        if (flat != null && pct != null)
            return $"{flat} & {pct}";
        return flat ?? pct ?? "0";
    }

    private static string FormatBonusFloat(float flatBonus, float pctBonus)
    {
        string flat = Mathf.Abs(flatBonus) >= 0.0001f ? $"{flatBonus:+0.##;-0.##}%" : null;
        string pct = Mathf.Abs(pctBonus) >= 0.0001f ? $"{pctBonus:+0.##;-0.##}%" : null;

        if (flat != null && pct != null)
            return $"{flat} & {pct}";
        return flat ?? pct ?? "0";
    }

    private static float MultToPct(float mult)
    {
        if (Mathf.Abs(mult - 1f) < 0.0001f)
            return 0f;
        return (mult - 1f) * 100f;
    }
}
