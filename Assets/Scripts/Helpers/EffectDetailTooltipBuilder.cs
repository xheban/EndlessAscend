using System.Collections.Generic;
using System.Text;
using MyGame.Common;
using UnityEngine.UIElements;

namespace MyGame.Helpers
{
    public static class EffectDetailTooltipBuilder
    {
        public static bool TryPopulateForEffectInstance(
            VisualElement tooltip,
            EffectInstance inst,
            int spellLevel = 1
        )
        {
            if (tooltip == null || inst == null || inst.effect == null)
                return false;

            var def = inst.effect;
            var scaled = inst.GetScaled(spellLevel);
            int componentCount = def.GetComponentCount();
            bool isComposite = componentCount > 1;
            var primaryComponent = def.GetComponent(0);

            var icon = tooltip.Q<VisualElement>("Icon");
            var name = tooltip.Q<Label>("Name");
            var kindAndPolarity = tooltip.Q<Label>("KindAndPolarity");
            var stat = tooltip.Q<Label>("Stat");
            var tags = tooltip.Q<Label>("Tags");

            var damageRow = tooltip.Q<VisualElement>("Value");
            var valueLabel = damageRow?.Q<Label>("Label");
            var valueValue = tooltip.Q<Label>("ValueValue");

            var chanceRow = tooltip.Q<VisualElement>("Chance");
            var chanceValue = tooltip.Q<Label>("ChanceValue");

            var stacksRow = tooltip.Q<VisualElement>("Stacks");
            var stackValue = tooltip.Q<Label>("StackValue");

            var durationRow = tooltip.Q<VisualElement>("Duration");
            var durationValue = tooltip.Q<Label>("DurationValue");

            var durationStackingRow = tooltip.Q<VisualElement>("DurationStacking");
            var durationStackingValue = tooltip.Q<Label>("DurationStackingValue");

            var stackableRow = tooltip.Q<VisualElement>("Stackable");
            var stackableValue = tooltip.Q<Label>("StackableValue");

            var mergeableRow = tooltip.Q<VisualElement>("Mergeable");
            var mergeableValue = tooltip.Q<Label>("MergeableValue");

            var reapplyRuleRow = tooltip.Q<VisualElement>("ReapplyRule");
            var reapplyRuleValue = tooltip.Q<Label>("ReapplyRuleValue");

            var removeWhenRow = tooltip.Q<VisualElement>("RemoveWhen");
            var removeWhenValue = tooltip.Q<Label>("RemoveWhenValue");

            var bonusesRow = tooltip.Q<VisualElement>("Bonuses");
            var bonusesList = bonusesRow?.Q<VisualElement>("BonusesList");

            var detailText = tooltip.Q<Label>("DetailText");

            if (icon != null)
                icon.style.backgroundImage =
                    def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;

            SetLabelText(
                name,
                string.IsNullOrWhiteSpace(def.displayName) ? def.effectId : def.displayName
            );

            var tagParts = new List<string>();

            var typeSet = new HashSet<string>();
            for (int i = 0; i < componentCount; i++)
            {
                var compDef = def.GetComponent(i);
                if (
                    compDef?.damageType == null
                    || compDef.damageType.Length == 0
                    || !IsTypeBasedStat(compDef.stat)
                )
                    continue;

                for (int j = 0; j < compDef.damageType.Length; j++)
                    typeSet.Add(NiceEnum(compDef.damageType[j].ToString()));
            }

            if (typeSet.Count > 0)
                tagParts.Add(string.Join(", ", typeSet));

            if (!isComposite && primaryComponent != null)
            {
                switch (primaryComponent.kind)
                {
                    case EffectKind.StatModifier:
                        SetLabelText(stat, NiceEnum(primaryComponent.stat.ToString()));
                        break;
                    case EffectKind.BaseStatModifier:
                        SetLabelText(stat, NiceEnum(primaryComponent.baseStat.ToString()));
                        break;
                    case EffectKind.DerivedStatModifier:
                        SetLabelText(stat, NiceEnum(primaryComponent.derivedStat.ToString()));
                        break;
                    default:
                        SetRowVisible(stat, false);
                        break;
                }
            }
            else
            {
                SetRowVisible(stat, false);
            }

            if (tagParts.Count > 0)
                SetLabelText(tags, string.Join(" ", tagParts));
            else
                SetRowVisible(tags, false);

            if (isComposite)
            {
                SetLabelText(kindAndPolarity, "Composite " + def.polarity.ToString());
                SetRowVisible(damageRow, false);
            }
            else
            {
                var kindLabel =
                    primaryComponent != null
                        ? NiceEnum(primaryComponent.kind.ToString())
                        : string.Empty;
                SetLabelText(kindAndPolarity, kindLabel + " " + def.polarity.ToString());

                var primaryScaled =
                    primaryComponent != null ? inst.GetComponentScaled(spellLevel, 0) : default;

                string magnitudeText =
                    primaryComponent != null
                        ? BuildMagnitudeText(primaryComponent, primaryScaled, def)
                        : null;

                if (valueLabel != null)
                    valueLabel.text =
                        primaryComponent != null
                            ? primaryComponent.kind switch
                            {
                                EffectKind.HealOverTime => "Healing",
                                EffectKind.DirectHeal => "Healing",
                                EffectKind.DamageOverTime => "Damage",
                                EffectKind.DirectDamage => "Damage",
                                EffectKind.StatModifier => "Modifier",
                                EffectKind.BaseStatModifier => "Modifier",
                                EffectKind.DerivedStatModifier => "Modifier",
                                _ => "Magnitude",
                            }
                            : "Magnitude";

                SetLabelText(valueValue, magnitudeText);
                SetRowVisible(damageRow, primaryComponent != null);
            }

            if (chanceValue != null)
                chanceValue.text =
                    scaled.chancePercent > 0 ? $"{scaled.chancePercent}%" : string.Empty;
            SetRowVisible(chanceRow, scaled.chancePercent > 0);

            if (stackValue != null)
                stackValue.text = scaled.maxStacks.ToString();
            SetRowVisible(stacksRow, scaled.stackable);

            if (durationValue != null)
                durationValue.text = FormatTurns(scaled.durationTurns);
            SetRowVisible(durationRow, scaled.durationTurns > 0);

            if (durationStackingValue != null)
                durationStackingValue.text = NiceEnum(scaled.durationStackMode.ToString());
            SetRowVisible(durationStackingRow, scaled.durationStackMode != DurationStackMode.None);

            if (stackableValue != null)
                stackableValue.text = scaled.stackable ? "True" : "False";
            SetRowVisible(stackableRow, stackableValue != null);

            if (mergeableValue != null)
                mergeableValue.text = scaled.mergeable ? "True" : "False";
            SetRowVisible(mergeableRow, mergeableValue != null);

            if (reapplyRuleValue != null)
                reapplyRuleValue.text = NiceEnum(scaled.reapplyRule.ToString());
            SetRowVisible(reapplyRuleRow, reapplyRuleValue != null);

            if (removeWhenValue != null)
                removeWhenValue.text = NiceEnum(inst.removeWhenType.ToString());
            SetRowVisible(removeWhenRow, removeWhenValue != null);

            // Bonuses list: only for composite buffs
            bool showBonuses = isComposite && def.polarity == EffectPolarity.Buff;
            if (bonusesList != null)
                bonusesList.Clear();
            if (showBonuses && bonusesList != null)
            {
                for (int i = 0; i < componentCount; i++)
                {
                    var compDef = def.GetComponent(i);
                    if (compDef == null)
                        continue;

                    var compScaled = inst.GetComponentScaled(spellLevel, i);
                    string line = BuildBonusLine(compDef, compScaled, def);
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var label = new Label(line);
                    label.AddToClassList("label-sm");
                    bonusesList.Add(label);
                }
            }
            SetRowVisible(
                bonusesRow,
                showBonuses && bonusesList != null && bonusesList.childCount > 0
            );

            if (detailText != null)
            {
                if (isComposite && def.polarity == EffectPolarity.Buff)
                {
                    detailText.text = def.description;
                }
                else
                {
                    if (isComposite)
                    {
                        var sb = new StringBuilder();

                        if (!string.IsNullOrWhiteSpace(def.description))
                        {
                            sb.AppendLine(def.description.Trim());
                        }

                        for (int i = 0; i < componentCount; i++)
                        {
                            var compDef = def.GetComponent(i);
                            if (compDef == null)
                                continue;

                            var compScaled = inst.GetComponentScaled(spellLevel, i);
                            string line = BuildComponentLine(compDef, compScaled, def);
                            if (!string.IsNullOrWhiteSpace(line))
                                sb.AppendLine(line);
                        }

                        detailText.text = sb.ToString().Trim();
                    }
                    else
                    {
                        detailText.text = def.description;
                    }
                }

                detailText.style.display = string.IsNullOrWhiteSpace(detailText.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            return true;
        }

        private static void SetLabelText(Label label, string text)
        {
            if (label == null)
                return;

            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrWhiteSpace(label.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private static void SetRowVisible(VisualElement row, bool visible)
        {
            if (row == null)
                return;

            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string BuildMagnitudeText(
            EffectComponentDefinition comp,
            EffectComponentScaledIntValues scaled,
            EffectDefinition def
        )
        {
            if (comp == null)
                return null;

            switch (comp.kind)
            {
                case EffectKind.StatModifier:
                {
                    int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;
                    bool powerScaling =
                        comp.stat == EffectStat.PowerScalingAll
                        || comp.stat == EffectStat.PowerScalingPhysical
                        || comp.stat == EffectStat.PowerScalingMagic;
                    return comp.op switch
                    {
                        EffectOp.Flat => FormatSigned(scaled.magnitudeFlat * sign, powerScaling),
                        EffectOp.Percent => FormatSigned(scaled.magnitudePercent * sign, true),
                        _ => null,
                    };
                }

                case EffectKind.BaseStatModifier:
                case EffectKind.DerivedStatModifier:
                {
                    int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;
                    if (comp.statOp == ModOp.Flat)
                        return FormatSigned(scaled.magnitudeFlat * sign, false);
                    if (comp.statOp == ModOp.Percent)
                        return FormatSigned(scaled.magnitudePercent * sign, true);
                    return null;
                }

                case EffectKind.DamageOverTime:
                case EffectKind.HealOverTime:
                case EffectKind.DirectDamage:
                case EffectKind.DirectHeal:
                    return BuildDamageMagnitudeText(scaled);
            }

            return null;
        }

        private static string BuildMagnitudeFromText(
            EffectComponentDefinition comp,
            EffectComponentScaledIntValues scaled
        )
        {
            if (comp == null)
                return null;

            if (comp.kind == EffectKind.StatModifier && comp.op == EffectOp.Flat)
                return "Flat";

            if (scaled.magnitudeBasis == EffectMagnitudeBasis.None)
                return string.Empty;

            return BuildBasisText(scaled.magnitudeBasis);
        }

        private static string BuildDamageMagnitudeText(EffectComponentScaledIntValues scaled)
        {
            var parts = new List<string>(2);

            if (scaled.magnitudeFlat > 0)
                parts.Add(scaled.magnitudeFlat.ToString());

            if (scaled.magnitudeBasis != EffectMagnitudeBasis.None && scaled.magnitudePercent > 0)
                parts.Add($"{scaled.magnitudePercent}% of {BuildBasisText(scaled.magnitudeBasis)}");

            return parts.Count > 0 ? string.Join(" + ", parts) : null;
        }

        private static string BuildBonusLine(
            EffectComponentDefinition comp,
            EffectComponentScaledIntValues scaled,
            EffectDefinition def
        )
        {
            if (comp == null)
                return null;

            int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;

            switch (comp.kind)
            {
                case EffectKind.StatModifier:
                {
                    if (comp.stat == EffectStat.None)
                        return null;

                    bool percent = comp.op == EffectOp.Percent || IsPowerScalingStat(comp.stat);
                    int value =
                        comp.op == EffectOp.Percent
                            ? scaled.magnitudePercent
                            : scaled.magnitudeFlat;

                    if (value == 0)
                        return null;

                    string label = NiceEnum(comp.stat.ToString());
                    if (
                        IsTypeBasedStat(comp.stat)
                        && comp.damageType != null
                        && comp.damageType.Length > 0
                    )
                    {
                        var types = new List<string>(comp.damageType.Length);
                        for (int i = 0; i < comp.damageType.Length; i++)
                            types.Add(NiceEnum(comp.damageType[i].ToString()));
                        label += $" ({string.Join(", ", types)})";
                    }

                    return $"{label} {FormatSigned(value * sign, percent)}";
                }

                case EffectKind.BaseStatModifier:
                {
                    int value =
                        comp.statOp == ModOp.Percent
                            ? scaled.magnitudePercent
                            : scaled.magnitudeFlat;
                    if (value == 0)
                        return null;
                    return $"{NiceEnum(comp.baseStat.ToString())} {FormatSigned(value * sign, comp.statOp == ModOp.Percent)}";
                }

                case EffectKind.DerivedStatModifier:
                {
                    int value =
                        comp.statOp == ModOp.Percent
                            ? scaled.magnitudePercent
                            : scaled.magnitudeFlat;
                    if (value == 0)
                        return null;
                    return $"{NiceEnum(comp.derivedStat.ToString())} {FormatSigned(value * sign, comp.statOp == ModOp.Percent)}";
                }
            }

            return null;
        }

        private static bool IsPowerScalingStat(EffectStat stat)
        {
            return stat == EffectStat.PowerScalingAll
                || stat == EffectStat.PowerScalingPhysical
                || stat == EffectStat.PowerScalingMagic;
        }

        private static bool IsTypeBasedStat(EffectStat stat)
        {
            return stat == EffectStat.AttackerBonusByType
                || stat == EffectStat.AttackerWeakenByType
                || stat == EffectStat.DefenderResistByType
                || stat == EffectStat.DefenderVulnerabilityByType;
        }

        private static string BuildComponentLine(
            EffectComponentDefinition comp,
            EffectComponentScaledIntValues scaled,
            EffectDefinition def
        )
        {
            if (comp == null)
                return null;

            string kindLabel = NiceEnum(comp.kind.ToString());

            string statLabel = comp.kind switch
            {
                EffectKind.StatModifier => NiceEnum(comp.stat.ToString()),
                EffectKind.BaseStatModifier => NiceEnum(comp.baseStat.ToString()),
                EffectKind.DerivedStatModifier => NiceEnum(comp.derivedStat.ToString()),
                _ => null,
            };

            string magnitude = BuildMagnitudeText(comp, scaled, def);
            string from = BuildMagnitudeFromText(comp, scaled);

            var sb = new StringBuilder();
            sb.Append("- ");
            sb.Append(kindLabel);

            if (!string.IsNullOrWhiteSpace(statLabel))
            {
                sb.Append(" (");
                sb.Append(statLabel);
                sb.Append(')');
            }

            if (!string.IsNullOrWhiteSpace(magnitude))
            {
                sb.Append(": ");
                sb.Append(magnitude);
            }

            if (!string.IsNullOrWhiteSpace(from))
            {
                sb.Append(" [");
                sb.Append(from);
                sb.Append(']');
            }

            return sb.ToString();
        }

        private static string BuildBasisText(EffectMagnitudeBasis basis)
        {
            return basis switch
            {
                EffectMagnitudeBasis.Power => "Current Power",
                EffectMagnitudeBasis.DamageDealt => "Last Damage Dealt",
                EffectMagnitudeBasis.LastDamageTaken => "Last Damage Taken",
                EffectMagnitudeBasis.MaxHealth => "Max HP",
                EffectMagnitudeBasis.MaxMana => "Max Mana",
                _ => string.Empty,
            };
        }

        private static string FormatSigned(int value, bool percent)
        {
            if (value == 0)
                return null;

            string sign = value > 0 ? "+" : string.Empty;
            return percent ? $"{sign}{value}%" : $"{sign}{value}";
        }

        private static string FormatTurns(int turns)
        {
            if (turns <= 0)
                return string.Empty;

            return turns == 1 ? "1 turn" : $"{turns} turns";
        }

        private static string NiceEnum(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var sb = new StringBuilder(raw.Length + 8);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (
                    i > 0
                    && char.IsUpper(c)
                    && (char.IsLower(raw[i - 1]) || char.IsDigit(raw[i - 1]))
                )
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
