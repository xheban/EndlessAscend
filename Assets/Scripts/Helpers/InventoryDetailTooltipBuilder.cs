using System;
using System.Collections.Generic;
using System.Text;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.Helpers
{
    public static class InventoryDetailTooltipBuilder
    {
        public static bool TryPopulateForItemId(VisualElement tooltip, string itemId)
        {
            if (tooltip == null || string.IsNullOrWhiteSpace(itemId))
                return false;

            var cfg = GameConfigProvider.Instance;
            var itemDb = cfg != null ? cfg.ItemDatabase : null;
            var def = itemDb != null ? itemDb.GetById(itemId) : null;

            var rarity = def != null ? def.rarity : Rarity.Common;
            var name =
                def != null && !string.IsNullOrWhiteSpace(def.displayName)
                    ? def.displayName
                    : itemId;
            var icon = def != null ? def.icon : null;
            var desc = def != null ? def.description : null;
            int goldValue = def != null ? Mathf.Max(0, def.goldValue) : 0;

            List<RequirementEntry> reqs = null;
            if (
                def != null
                && def.itemType == ItemDefinitionType.SpellScroll
                && !def.usableInCombat
            )
                reqs = BuildLearningScrollRequirements(def);

            var data = new TooltipData
            {
                displayName = name,
                icon = icon,
                rarity = rarity,
                raritySlotLine = NiceEnum(rarity.ToString()),
                levelTierLine = null, // items: not shown (match inventory)
                description = desc,
                rolledStatLines = null,
                requirements = reqs,
                goldValue = goldValue,
            };

            ApplyToTooltipRoot(tooltip, data);
            return true;
        }

        public static bool TryPopulateForEquipmentInstance(
            VisualElement tooltip,
            PlayerEquipment.EquipmentInstance inst
        )
        {
            if (tooltip == null || inst == null || string.IsNullOrWhiteSpace(inst.equipmentId))
                return false;

            if (!TryBuildEquipmentTooltipData(inst, out var data) || data == null)
                return false;

            ApplyToTooltipRoot(tooltip, data);
            return true;
        }

        public static bool TryPopulateForEquipmentId(VisualElement tooltip, string equipmentId)
        {
            if (tooltip == null || string.IsNullOrWhiteSpace(equipmentId))
                return false;

            var cfg = GameConfigProvider.Instance;
            var db = cfg != null ? cfg.EquipmentDatabase : null;
            var def = db != null ? db.GetById(equipmentId) : null;

            var rarity = def != null ? def.rarity : Rarity.Common;
            var name =
                def != null && !string.IsNullOrWhiteSpace(def.displayName)
                    ? def.displayName
                    : equipmentId;
            var icon = def != null ? def.icon : null;
            if (icon == null && db != null)
                icon = db.GetIcon(equipmentId);
            var desc = def != null ? def.description : null;
            int goldValue = def != null ? Mathf.Max(0, def.goldValue) : 0;

            var slotText = def != null ? NiceEnum(def.slot.ToString()) : string.Empty;
            var rarityText = NiceEnum(rarity.ToString());

            var levelTierLine = string.Empty;
            if (def != null)
                levelTierLine =
                    $"Level {Mathf.Max(1, def.level)}   {HelperFunctions.ToTierRoman(def.tier)}";

            var reqs = BuildEquipmentRequirements(def);

            var data = new TooltipData
            {
                displayName = name,
                icon = icon,
                rarity = rarity,
                raritySlotLine = string.IsNullOrWhiteSpace(slotText)
                    ? rarityText
                    : $"{rarityText} {slotText}",
                levelTierLine = levelTierLine,
                description = desc,
                rolledStatLines = null,
                requirements = reqs,
                goldValue = goldValue,
            };

            ApplyToTooltipRoot(tooltip, data);
            return true;
        }

        private static bool TryBuildEquipmentTooltipData(
            PlayerEquipment.EquipmentInstance inst,
            out TooltipData data
        )
        {
            data = null;

            var cfg = GameConfigProvider.Instance;
            var db = cfg != null ? cfg.EquipmentDatabase : null;
            var def = db != null ? db.GetById(inst.equipmentId) : null;

            var rarity = def != null ? def.rarity : Rarity.Common;
            var name =
                def != null && !string.IsNullOrWhiteSpace(def.displayName)
                    ? def.displayName
                    : inst.equipmentId;
            var icon = def != null ? def.icon : null;
            if (icon == null && db != null)
                icon = db.GetIcon(inst.equipmentId);
            var desc = def != null ? def.description : null;
            int goldValue = def != null ? Mathf.Max(0, def.goldValue) : 0;

            var slotText = def != null ? NiceEnum(def.slot.ToString()) : string.Empty;
            var rarityText = NiceEnum(rarity.ToString());

            var levelTierLine = string.Empty;
            if (def != null)
                levelTierLine =
                    $"Level {Mathf.Max(1, def.level)}   {HelperFunctions.ToTierRoman(def.tier)}";

            var rolledLines = BuildRolledStatLines(inst);
            var reqs = BuildEquipmentRequirements(def);

            data = new TooltipData
            {
                displayName = name,
                icon = icon,
                rarity = rarity,
                raritySlotLine = string.IsNullOrWhiteSpace(slotText)
                    ? rarityText
                    : $"{rarityText} {slotText}",
                levelTierLine = levelTierLine,
                description = desc,
                rolledStatLines = rolledLines,
                requirements = reqs,
                goldValue = goldValue,
            };
            return true;
        }

        private static void ApplyToTooltipRoot(VisualElement tooltipRoot, TooltipData data)
        {
            if (tooltipRoot == null || data == null)
                return;

            var icon = tooltipRoot.Q<VisualElement>("Icon");
            var name = tooltipRoot.Q<Label>("Name");
            var raritySlot = tooltipRoot.Q<Label>("RaritySlot");
            var levelTier = tooltipRoot.Q<Label>("LevelTier");
            var detailText = tooltipRoot.Q<Label>("DetailText");
            var rolledStatsSection = tooltipRoot.Q<VisualElement>("RolledStats");
            var rolledStatsList = tooltipRoot.Q<VisualElement>("RolledStatsList");
            var requirementsSection = tooltipRoot.Q<VisualElement>("Requirements");
            var requirementsList = tooltipRoot.Q<VisualElement>("RequirmentsList");
            var valueSection = tooltipRoot.Q<VisualElement>("Value");
            var goldValueLabel = valueSection != null ? valueSection.Q<Label>("GoldValue") : null;

            ApplyTooltipDataToTooltipElements(
                icon,
                name,
                raritySlot,
                levelTier,
                detailText,
                rolledStatsSection,
                rolledStatsList,
                requirementsSection,
                requirementsList,
                valueSection,
                goldValueLabel,
                data
            );
        }

        private static void ApplyTooltipDataToTooltipElements(
            VisualElement icon,
            Label name,
            Label raritySlot,
            Label levelTier,
            Label detailText,
            VisualElement rolledStatsSection,
            VisualElement rolledStatsList,
            VisualElement requirementsSection,
            VisualElement requirementsList,
            VisualElement valueSection,
            Label goldValueLabel,
            TooltipData data
        )
        {
            ApplyRarityNameStyle(name, data.rarity);

            if (name != null)
                name.text = data.displayName;
            if (icon != null)
                icon.style.backgroundImage =
                    data.icon != null ? new StyleBackground(data.icon) : StyleKeyword.None;

            if (raritySlot != null)
            {
                raritySlot.text = data.raritySlotLine;
                raritySlot.style.display = string.IsNullOrWhiteSpace(raritySlot.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (levelTier != null)
            {
                levelTier.text = data.levelTierLine;
                levelTier.style.display = string.IsNullOrWhiteSpace(levelTier.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (detailText != null)
            {
                detailText.text = data.description;
                detailText.style.display = string.IsNullOrWhiteSpace(detailText.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (rolledStatsSection != null)
                rolledStatsSection.style.display =
                    data.rolledStatLines != null && data.rolledStatLines.Count > 0
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

            if (rolledStatsList != null)
            {
                rolledStatsList.Clear();
                if (data.rolledStatLines != null)
                {
                    for (int i = 0; i < data.rolledStatLines.Count; i++)
                    {
                        var t = data.rolledStatLines[i];
                        if (string.IsNullOrWhiteSpace(t))
                            continue;

                        var row = new Label($"- {t}");
                        row.AddToClassList("label-sm");
                        row.style.unityTextAlign = TextAnchor.UpperLeft;
                        row.style.marginBottom = 2;
                        rolledStatsList.Add(row);
                    }
                }
            }

            if (requirementsSection != null)
                requirementsSection.style.display =
                    data.requirements != null && data.requirements.Count > 0
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

            if (requirementsList != null)
            {
                requirementsList.Clear();

                if (data.requirements != null)
                {
                    for (int i = 0; i < data.requirements.Count; i++)
                    {
                        var r = data.requirements[i];
                        if (r == null || string.IsNullOrWhiteSpace(r.name))
                            continue;

                        var line = new Label($"{r.name} {r.required}");
                        line.AddToClassList("label-sm");
                        line.style.unityTextAlign = TextAnchor.UpperLeft;
                        line.style.marginBottom = 2;

                        if (r.current < r.required)
                            line.AddToClassList("text-danger");
                        else
                            line.AddToClassList("text-success");

                        requirementsList.Add(line);
                    }
                }
            }

            if (goldValueLabel != null)
                goldValueLabel.text = Mathf.Max(0, data.goldValue).ToString();

            if (valueSection != null)
                valueSection.style.display =
                    goldValueLabel != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private sealed class TooltipData
        {
            public string displayName;
            public Sprite icon;
            public Rarity rarity;
            public string raritySlotLine;
            public string levelTierLine;
            public string description;
            public List<string> rolledStatLines;
            public List<RequirementEntry> requirements;
            public int goldValue;
        }

        private sealed class RequirementEntry
        {
            public string name;
            public int required;
            public int current;
        }

        private static List<RequirementEntry> BuildEquipmentRequirements(EquipmentDefinitionSO def)
        {
            if (def == null)
                return null;

            var save = SaveSession.Current;
            int playerLevel = save != null ? Mathf.Max(1, save.level) : 1;
            var stats = save != null ? save.finalStats : default;

            var reqs = new List<RequirementEntry>(8);

            reqs.Add(
                new RequirementEntry
                {
                    name = "Level",
                    required = Mathf.Max(1, def.requiredLevel),
                    current = playerLevel,
                }
            );

            if (def.flatStatRequirements != null)
            {
                for (int i = 0; i < def.flatStatRequirements.Count; i++)
                {
                    var r = def.flatStatRequirements[i];
                    if (r.minValue <= 0)
                        continue;

                    int current = GetBaseStatValue(stats, r.stat);
                    reqs.Add(
                        new RequirementEntry
                        {
                            name = NiceEnum(r.stat.ToString()),
                            required = Mathf.Max(0, r.minValue),
                            current = current,
                        }
                    );
                }
            }

            return reqs.Count > 0 ? reqs : null;
        }

        private static List<RequirementEntry> BuildLearningScrollRequirements(ItemDefinitionSO def)
        {
            if (def == null)
                return null;

            var save = SaveSession.Current;
            int playerLevel = save != null ? Mathf.Max(1, save.level) : 1;

            return new List<RequirementEntry>(1)
            {
                new RequirementEntry
                {
                    name = "Level",
                    required = Mathf.Max(1, def.requiredLevel),
                    current = playerLevel,
                },
            };
        }

        private static int GetBaseStatValue(Stats stats, BaseStatType stat)
        {
            return stat switch
            {
                BaseStatType.Strength => stats.strength,
                BaseStatType.Agility => stats.agility,
                BaseStatType.Intelligence => stats.intelligence,
                BaseStatType.Spirit => stats.spirit,
                BaseStatType.Endurance => stats.endurance,
                _ => 0,
            };
        }

        private static List<string> BuildRolledStatLines(PlayerEquipment.EquipmentInstance inst)
        {
            var lines = new List<string>(32);

            if (inst?.rolledBaseStatMods != null)
            {
                for (int i = 0; i < inst.rolledBaseStatMods.Count; i++)
                {
                    var m = inst.rolledBaseStatMods[i];
                    lines.Add(
                        $"{HumanizeStatName(NiceEnum(m.stat.ToString()))} {FormatModValue(m.op, m.value)}"
                    );
                }
            }

            if (inst?.rolledDerivedStatMods != null)
            {
                for (int i = 0; i < inst.rolledDerivedStatMods.Count; i++)
                {
                    var m = inst.rolledDerivedStatMods[i];
                    lines.Add(
                        $"{HumanizeStatName(NiceEnum(m.stat.ToString()))} {FormatModValue(m.op, m.value)}"
                    );
                }
            }

            if (inst?.rolledCombatStatMods != null)
            {
                for (int i = 0; i < inst.rolledCombatStatMods.Count; i++)
                {
                    var m = inst.rolledCombatStatMods[i];
                    var line = FormatCombatStatModifierLine(m);
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add(line);
                }
            }

            if (inst?.rolledSpellOverrides != null)
            {
                for (int i = 0; i < inst.rolledSpellOverrides.Count; i++)
                {
                    var o = inst.rolledSpellOverrides[i];
                    var line = FormatSpellVariableOverrideLine(o);
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add(line);
                }
            }

            return lines;
        }

        private static string HumanizeStatName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var s = raw;
            s = s.Replace("Max Hp", "Max HP");
            s = s.Replace("Defense", "Defence");
            s = s.Replace("Casting Speed", "Cast Speed");
            return s;
        }

        private static string FormatCombatStatModifierLine(CombatStatModifier m)
        {
            string label = HumanizeStatName(NiceEnum(m.stat.ToString()));
            if (IsTypeBasedStat(m.stat) && m.damageType != DamageType.None)
                label += $" ({NiceEnum(m.damageType.ToString())})";

            return $"{label} {FormatCombatStatModValue(m)}";
        }

        private static string FormatCombatStatModValue(CombatStatModifier m)
        {
            if (m.op == EffectOp.Flat && IsPowerScalingStat(m.stat))
                return FormatModValue(ModOp.Percent, m.value);

            return m.op switch
            {
                EffectOp.Flat => FormatModValue(ModOp.Flat, m.value),
                EffectOp.Percent => FormatModValue(ModOp.Percent, m.value),
                _ => m.value.ToString(),
            };
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

        private static bool IsTypeBasedStat(EffectStat stat)
        {
            return stat == EffectStat.AttackerBonusByType
                || stat == EffectStat.AttackerWeakenByType
                || stat == EffectStat.DefenderResistByType
                || stat == EffectStat.DefenderVulnerabilityByType;
        }

        private static string FormatSpellVariableOverrideLine(SpellVariableOverride o)
        {
            switch (o.type)
            {
                case SpellVariableOverrideType.DamageKind:
                    return $"Damage Kind: {NiceEnum(o.damageKind.ToString())}";
                case SpellVariableOverrideType.DamageType:
                    return $"Damage Type: {NiceEnum(o.damageType.ToString())}";
                case SpellVariableOverrideType.IgnoreDefenseFlat:
                    return $"Ignore Defence {FormatModValue(ModOp.Flat, o.ignoreDefenseFlat)}";
                case SpellVariableOverrideType.IgnoreDefensePercent:
                    return $"Ignore Defence {FormatModValue(ModOp.Percent, o.ignoreDefensePercent)}";
                default:
                    return NiceEnum(o.type.ToString());
            }
        }

        private static string FormatModValue(ModOp op, int value)
        {
            return op switch
            {
                ModOp.Flat => value > 0 ? $"+ {value}" : value.ToString(),
                ModOp.Percent => value > 0 ? $"+ {value}%" : $"{value}%",
                _ => value.ToString(),
            };
        }

        private static void ApplyRarityNameStyle(Label nameLabel, Rarity rarity)
        {
            if (nameLabel == null)
                return;

            nameLabel.RemoveFromClassList("rarity-common");
            nameLabel.RemoveFromClassList("rarity-uncommon");
            nameLabel.RemoveFromClassList("rarity-rare");
            nameLabel.RemoveFromClassList("rarity-epic");
            nameLabel.RemoveFromClassList("rarity-legendary");
            nameLabel.RemoveFromClassList("rarity-mythical");
            nameLabel.RemoveFromClassList("rarity-forbidden");

            switch (rarity)
            {
                case Rarity.Uncommon:
                    nameLabel.AddToClassList("rarity-uncommon");
                    break;
                case Rarity.Rare:
                    nameLabel.AddToClassList("rarity-rare");
                    break;
                case Rarity.Epic:
                    nameLabel.AddToClassList("rarity-epic");
                    break;
                case Rarity.Legendary:
                    nameLabel.AddToClassList("rarity-legendary");
                    break;
                case Rarity.Mythical:
                    nameLabel.AddToClassList("rarity-mythical");
                    break;
                case Rarity.Forbidden:
                    nameLabel.AddToClassList("rarity-forbidden");
                    break;
                default:
                    nameLabel.AddToClassList("rarity-common");
                    break;
            }
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
