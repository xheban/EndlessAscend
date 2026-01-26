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

            var icon = tooltip.Q<VisualElement>("Icon");
            var name = tooltip.Q<Label>("Name");
            var tags = tooltip.Q<Label>("Tags");
            var polarity = tooltip.Q<Label>("MasteryLevel");

            var damageRow = tooltip.Q<VisualElement>("Damage");
            var damageLabel = damageRow?.Q<Label>("Label");
            var damageValue = tooltip.Q<Label>("DamageValue");
            var damageFrom = tooltip.Q<Label>("DamageFrom");

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

            var detailText = tooltip.Q<Label>("DetailText");

            if (icon != null)
                icon.style.backgroundImage =
                    def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;

            SetLabelText(
                name,
                string.IsNullOrWhiteSpace(def.displayName) ? def.effectId : def.displayName
            );

            var tagParts = new List<string>();
            if (def.kind != default)
                tagParts.Add(NiceEnum(def.kind.ToString()));
            if (def.kind == EffectKind.StatModifier && def.stat != EffectStat.None)
                tagParts.Add(NiceEnum(def.stat.ToString()));
            if (def.damageType != null && def.damageType.Length > 0)
                tagParts.Add(
                    string.Join(
                        ", ",
                        System.Array.ConvertAll(def.damageType, d => NiceEnum(d.ToString()))
                    )
                );

            SetLabelText(tags, string.Join(", ", tagParts));
            SetLabelText(polarity, NiceEnum(def.polarity.ToString()));

            string magnitudeText = BuildMagnitudeText(def, scaled);
            if (damageLabel != null)
                damageLabel.text = def.kind switch
                {
                    EffectKind.HealOverTime => "Healing",
                    EffectKind.DamageOverTime => "Damage",
                    _ => "Magnitude",
                };

            SetLabelText(damageValue, magnitudeText);
            if (damageFrom != null)
            {
                string from = BuildMagnitudeFromText(def, scaled);
                damageFrom.text = from;
                damageFrom.style.display = string.IsNullOrWhiteSpace(from)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
            SetRowVisible(damageRow, !string.IsNullOrWhiteSpace(magnitudeText));

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

            if (detailText != null)
            {
                detailText.text = def.description;
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
            EffectDefinition def,
            EffectInstanceScaledIntValues scaled
        )
        {
            if (def == null)
                return null;

            return def.op switch
            {
                EffectOp.Flat => scaled.magnitudeFlat > 0 ? scaled.magnitudeFlat.ToString() : null,
                EffectOp.MorePercent => scaled.magnitudePercent > 0
                    ? $"+{scaled.magnitudePercent}%"
                    : null,
                EffectOp.LessPercent => scaled.magnitudePercent > 0
                    ? $"-{scaled.magnitudePercent}%"
                    : null,
                _ => null,
            };
        }

        private static string BuildMagnitudeFromText(
            EffectDefinition def,
            EffectInstanceScaledIntValues scaled
        )
        {
            if (def == null)
                return null;

            if (def.kind == EffectKind.StatModifier && def.stat != EffectStat.None)
                return NiceEnum(def.stat.ToString());

            if (scaled.magnitudeBasis != EffectMagnitudeBasis.None)
                return NiceEnum(scaled.magnitudeBasis.ToString());

            return def.op == EffectOp.Flat ? "Flat" : "Percent";
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
