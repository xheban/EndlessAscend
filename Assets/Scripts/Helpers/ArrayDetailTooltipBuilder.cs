using System.Collections.Generic;
using System.Text;
using MyGame.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.Helpers
{
    public static class ArrayDetailTooltipBuilder
    {
        public static bool TryPopulateForCore(VisualElement tooltip, ArrayCoreDefinitionSO core)
        {
            if (tooltip == null || core == null)
                return false;

            return TryPopulate(
                tooltip,
                core.displayName,
                core.id,
                core.image,
                core.tags,
                core.bonuses,
                core.description,
                core.manaStoneCost
            );
        }

        public static bool TryPopulateForNode(VisualElement tooltip, ArrayNodeDefinitionSO node)
        {
            if (tooltip == null || node == null)
                return false;

            return TryPopulate(
                tooltip,
                node.displayName,
                node.id,
                node.imageX,
                node.tags,
                node.bonuses,
                node.description,
                node.manaStoneCost
            );
        }

        private static bool TryPopulate(
            VisualElement tooltip,
            string displayName,
            string id,
            Sprite image,
            DamageType[] tags,
            List<ArrayBonusEntry> bonuses,
            string description,
            int manaStoneCost
        )
        {
            var icon = tooltip.Q<VisualElement>("Icon");
            var nameLabel = tooltip.Q<Label>("Name");
            var tagsLabel = tooltip.Q<Label>("Tags");
            var effectsList = tooltip.Q<VisualElement>("EffectsList");
            var detailText = tooltip.Q<Label>("DetailText");
            var costValue = tooltip.Q<Label>("CostValueLabel") ?? tooltip.Q<Label>("CostValue");

            if (
                icon == null
                || nameLabel == null
                || tagsLabel == null
                || effectsList == null
                || detailText == null
            )
                return false;

            nameLabel.text = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : id ?? string.Empty;
            tagsLabel.text = BuildTags(tags);
            detailText.text = description ?? string.Empty;
            if (costValue != null)
                costValue.text = manaStoneCost.ToString();

            icon.style.backgroundImage =
                image != null ? new StyleBackground(image) : StyleKeyword.None;

            effectsList.Clear();
            if (bonuses != null && bonuses.Count > 0)
            {
                for (int i = 0; i < bonuses.Count; i++)
                {
                    var b = bonuses[i];
                    if (b == null)
                        continue;

                    var line = new Label(FormatBonusLine(b));
                    line.AddToClassList("label-sm");
                    effectsList.Add(line);
                }
            }
            else
            {
                var line = new Label("None");
                line.AddToClassList("label-sm");
                effectsList.Add(line);
            }

            return true;
        }

        private static string BuildTags(DamageType[] tags)
        {
            if (tags == null || tags.Length == 0)
                return "None";

            var list = new List<string>();
            for (int i = 0; i < tags.Length; i++)
            {
                var t = tags[i];
                if (t == DamageType.None)
                    continue;
                list.Add(FormatEnumName(t.ToString()));
            }

            return list.Count > 0 ? string.Join(", ", list) : "None";
        }

        private static string FormatBonusLine(ArrayBonusEntry entry)
        {
            if (entry == null)
                return string.Empty;

            var typeName = entry.bonusType switch
            {
                ArrayBonusType.PerMasteryLevel => "Per Mastery Level sacrificied",
                ArrayBonusType.General => "General",
                ArrayBonusType.MatchingTags => "Matching attributes",
                _ => entry.bonusType.ToString(),
            };

            var value = entry.value;
            var valueText = value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
            var suffix = entry.bonusType switch
            {
                ArrayBonusType.General => "% chance",
                ArrayBonusType.MatchingTags => "% chance",
                ArrayBonusType.PerMasteryLevel => "% chance",
                _ => string.Empty,
            };

            return string.IsNullOrWhiteSpace(suffix)
                ? $"{typeName}: {valueText}"
                : $"{typeName}: {valueText} {suffix}";
        }

        private static string FormatEnumName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var sb = new StringBuilder(raw.Length + 4);
            sb.Append(raw[0]);
            for (int i = 1; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsUpper(c) && raw[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
