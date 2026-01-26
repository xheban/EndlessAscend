using System;
using System.Collections.Generic;
using System.Text;
using MyGame.Common;
using MyGame.Run;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.Helpers
{
    public static class SpellDetailTooltipBuilder
    {
        public static bool TryPopulateForSpellId(
            VisualElement tooltip,
            string spellId,
            bool learned,
            ScreenSwapper swapper = null
        )
        {
            if (tooltip == null || string.IsNullOrWhiteSpace(spellId))
                return false;

            var db =
                GameConfigProvider.Instance != null
                    ? GameConfigProvider.Instance.SpellDatabase
                    : null;
            var playerSpellbook = RunSession.Spellbook;
            var playerSpell = playerSpellbook != null ? playerSpellbook.Get(spellId) : null;
            var def = db != null ? db.GetById(spellId) : null;

            return TryPopulateForSpellDefinition(tooltip, def, playerSpell, learned, swapper);
        }

        public static bool TryPopulateForSpellDefinition(
            VisualElement tooltip,
            SpellDefinition def,
            PlayerSpellEntry playerSpell,
            bool learned,
            ScreenSwapper swapper = null
        )
        {
            if (tooltip == null || def == null)
                return false;

            // Root elements
            var icon = tooltip.Q<VisualElement>("Icon");
            var name = tooltip.Q<Label>("Name");
            var tags = tooltip.Q<Label>("Tags");
            var mastery = tooltip.Q<Label>("MasteryLevel");

            var damageValue = tooltip.Q<Label>("DamageValue");
            var baseDamageValue = tooltip.Q<Label>("BaseDamageValue");
            var bonusDamageValue = tooltip.Q<Label>("BonusDamageValue");
            var scalingTypeValue = tooltip.Q<Label>("ScalingTypeValue");

            var manaCostValue = tooltip.Q<Label>("ManaCostValue");
            var cooldownValue = tooltip.Q<Label>("CooldownValue");
            var castTimeValue = tooltip.Q<Label>("CastTimeValue");

            var damageTypeLabel = tooltip.Q<Label>("DamageType");
            var rangeTypeLabel = tooltip.Q<Label>("RangeType");

            var ignoreDefenceValue = tooltip.Q<Label>("IgnoreDefenceValue");

            var effectsSection = tooltip.Q<VisualElement>("Effects");
            var onHitEffectsSection = tooltip.Q<VisualElement>("OnHitEffects");
            var onHitEffectsList = onHitEffectsSection?.Q<VisualElement>("OnHitEffectsList");
            var onCastEffectsSection = tooltip.Q<VisualElement>("OnCastEffects");
            var onCastEffectsList = onCastEffectsSection?.Q<VisualElement>("OnCastEffectsList");
            if (effectsSection != null)
                effectsSection.pickingMode = PickingMode.Position;
            if (onHitEffectsSection != null)
                onHitEffectsSection.pickingMode = PickingMode.Position;
            if (onCastEffectsSection != null)
                onCastEffectsSection.pickingMode = PickingMode.Position;

            var reqSection = tooltip.Q<VisualElement>("Requirements");
            var reqList = tooltip.Q<VisualElement>("RequirmentsList");

            var detailText = tooltip.Q<Label>("DetailText");

            // Populate basic fields
            ApplyRarityNameStyle(name, def.rarity);

            if (name != null)
                name.text = string.IsNullOrWhiteSpace(def.displayName)
                    ? def.spellId
                    : def.displayName;

            if (icon != null)
                icon.style.backgroundImage =
                    def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;

            // Tags: show intent and damage kind
            if (tags != null)
            {
                var tagParts = new List<string>();
                if (def.damageTag != null && def.damageTag.Length > 0)
                    tagParts.Add(
                        string.Join(
                            ", ",
                            Array.ConvertAll(def.damageTag, d => NiceEnum(d.ToString()))
                        )
                    );

                tags.text = string.Join(", ", tagParts);
                tags.style.display = string.IsNullOrWhiteSpace(tags.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (mastery != null)
            {
                mastery.text =
                    $"Mastery Level {playerSpell?.level ?? 1}  {HelperFunctions.ToTierRoman(def.tier)}";
                mastery.style.display = string.IsNullOrWhiteSpace(mastery.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            int actualDamage = def.GetDamageAtLevel(playerSpell?.level ?? 1);
            if (damageValue != null)
                damageValue.text = actualDamage.ToString();
            if (baseDamageValue != null)
                baseDamageValue.text = def.baseDamage.ToString();
            if (bonusDamageValue != null)
                bonusDamageValue.text = def.damageScalingValue.ToString();
            if (scalingTypeValue != null)
                scalingTypeValue.text = NiceEnum(def.damageScalingType.ToString());

            if (manaCostValue != null)
                manaCostValue.text = def.manaCost.ToString();
            if (cooldownValue != null)
                cooldownValue.text = def.cooldownTurns.ToString();
            if (castTimeValue != null)
                castTimeValue.text = def.castTimeValue.ToString();

            if (damageTypeLabel != null)
                damageTypeLabel.text = NiceEnum(def.damageKind.ToString());
            if (rangeTypeLabel != null)
                rangeTypeLabel.text = NiceEnum(def.damageRangeType.ToString());

            if (ignoreDefenceValue != null)
            {
                string s = string.Empty;
                if (def.ignoreDefenseFlat > 0)
                    s += def.ignoreDefenseFlat.ToString();
                if (def.ignoreDefensePercent > 0)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        s += " + ";
                    s += def.ignoreDefensePercent + "%";
                }
                ignoreDefenceValue.text = string.IsNullOrWhiteSpace(s) ? "0" : s;
            }

            // Effects
            int spellLevel = playerSpell?.level ?? 1;
            bool hasOnHit = PopulateEffectSection(
                onHitEffectsSection,
                onHitEffectsList,
                def.onHitEffects,
                spellLevel,
                swapper
            );
            bool hasOnCast = PopulateEffectSection(
                onCastEffectsSection,
                onCastEffectsList,
                def.onCastEffects,
                spellLevel,
                swapper
            );
            if (effectsSection != null)
                effectsSection.style.display =
                    hasOnHit || hasOnCast ? DisplayStyle.Flex : DisplayStyle.None;

            // Requirements: hide when learned == true
            if (reqSection != null)
                reqSection.style.display = learned ? DisplayStyle.None : DisplayStyle.Flex;

            if (reqList != null)
            {
                reqList.Clear();

                if (!learned)
                {
                    // Level: none on definition, but show max level as guidance
                    var lvl = new Label($"Max Level {def.maxLevel}");
                    lvl.AddToClassList("label-sm");
                    reqList.Add(lvl);

                    var cls = new Label(NiceEnum(def.allowedClasses.ToString()));
                    cls.AddToClassList("label-sm");
                    reqList.Add(cls);

                    var spec = new Label(NiceEnum(def.allowedSpecs.ToString()));
                    spec.AddToClassList("label-sm");
                    reqList.Add(spec);
                }
            }

            if (detailText != null)
            {
                detailText.text = def.description;
                detailText.style.display = string.IsNullOrWhiteSpace(detailText.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            return true;
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

        private static bool PopulateEffectSection(
            VisualElement section,
            VisualElement list,
            EffectInstance[] effects,
            int spellLevel,
            ScreenSwapper swapper
        )
        {
            if (section == null)
                return false;

            if (list != null)
                list.Clear();

            if (effects == null || effects.Length == 0)
            {
                section.style.display = DisplayStyle.None;
                return false;
            }

            bool hasAny = false;

            if (list != null)
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    var inst = effects[i];
                    var def = inst?.effect;
                    if (def == null)
                        continue;

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 2;
                    row.style.marginRight = 24;
                    row.pickingMode = PickingMode.Position;
                    row.AddToClassList("tooltip-effect-row");
                    row.AddToClassList("clickable");

                    var icon = new VisualElement();
                    icon.style.width = 16;
                    icon.style.height = 16;
                    icon.style.marginRight = 8;

                    icon.style.backgroundImage =
                        def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;
                    if (def.icon == null)
                        icon.style.display = DisplayStyle.None;

                    var name = string.IsNullOrWhiteSpace(def.displayName)
                        ? def.effectId
                        : def.displayName;
                    var label = new Label(name);
                    label.AddToClassList("label-sm");

                    row.Add(icon);
                    row.Add(label);
                    list.Add(row);

                    ConfigureEffectRowTooltip(row, inst, spellLevel, swapper);
                    hasAny = true;
                }
            }

            section.style.display = hasAny ? DisplayStyle.Flex : DisplayStyle.None;
            return hasAny;
        }

        private static void ConfigureEffectRowTooltip(
            VisualElement row,
            EffectInstance inst,
            int spellLevel,
            ScreenSwapper swapper
        )
        {
            if (row == null || inst == null || swapper == null)
                return;

            var tooltip = swapper.GetCustomTooltipElement("EffectDetailTooltip");
            if (tooltip == null)
                return;

            row.RegisterCallback<PointerEnterEvent>(
                _ =>
                {
                    if (
                        !EffectDetailTooltipBuilder.TryPopulateForEffectInstance(
                            tooltip,
                            inst,
                            spellLevel
                        )
                    )
                        return;

                    swapper.ShowSecondaryCustomTooltipAboveWorldPosition(
                        tooltip,
                        row.worldBound.center,
                        offsetPx: 8f,
                        edgePaddingPx: 8f,
                        fallbackWidthPx: 396f,
                        fallbackHeightPx: 260f
                    );
                },
                TrickleDown.TrickleDown
            );

            row.RegisterCallback<PointerLeaveEvent>(
                evt =>
                {
                    swapper.HideSecondaryCustomTooltip(tooltip);
                },
                TrickleDown.TrickleDown
            );
            row.RegisterCallback<PointerOutEvent>(
                evt =>
                {
                    swapper.HideSecondaryCustomTooltip(tooltip);
                },
                TrickleDown.TrickleDown
            );
        }

        private static bool IsPointerOverTooltip(VisualElement tooltip, Vector2 pointerPosition)
        {
            if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
                return false;

            return tooltip.worldBound.Contains(pointerPosition);
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
