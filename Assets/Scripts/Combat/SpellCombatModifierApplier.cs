using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    public static class SpellCombatModifierApplier
    {
        public static void ApplyAll(
            StatModifiers target,
            System.Collections.Generic.IEnumerable<SpellCombatModifier> mods
        )
        {
            if (target == null || mods == null)
                return;

            foreach (var m in mods)
                ApplyOne(target, m);
        }

        public static void ApplyOne(StatModifiers target, SpellCombatModifier m)
        {
            if (target == null)
                return;

            int flat = 0;
            float pct = 0f;

            if (m.op == ModOp.Flat)
                flat = Mathf.RoundToInt(m.value);
            else if (m.op == ModOp.Percent)
                pct = m.value;

            // Preferred: target-based mapping (covers most of StatModifiers)
            if (m.target != SpellCombatModifierTarget.None)
            {
                ApplyTarget(target, m, flat, pct);
                return;
            }

            // Legacy: scope-based mapping (kept for backwards compatibility)
            switch (m.scope)
            {
                case SpellModifierScope.Any:
                    if (m.op == ModOp.Flat)
                        target.AddSpellBaseFlat(flat);
                    else if (m.op == ModOp.Percent)
                        target.AddSpellBaseMorePercent(pct / 100f);
                    break;

                case SpellModifierScope.DamageKind:
                    if (m.damageKind == DamageKind.Magical)
                    {
                        if (m.op == ModOp.Flat)
                            target.AddMagicSpellBaseFlat(flat);
                        else if (m.op == ModOp.Percent)
                            target.AddMagicSpellBaseMorePercent(pct / 100f);
                    }
                    else
                    {
                        if (m.op == ModOp.Flat)
                            target.AddPhysicalSpellBaseFlat(flat);
                        else if (m.op == ModOp.Percent)
                            target.AddPhysicalSpellBaseMorePercent(pct / 100f);
                    }
                    break;

                case SpellModifierScope.DamageType:
                    if (m.op == ModOp.Flat)
                        target.AddAttackerBonusFlat(m.damageType, flat);
                    else if (m.op == ModOp.Percent)
                        target.AddAttackerBonusMorePercent(m.damageType, pct / 100f);
                    break;

                case SpellModifierScope.DamageRangeType:
                    if (m.op == ModOp.Flat)
                        target.AddAttackerRangeBonusFlat(m.damageRangeType, flat);
                    else if (m.op == ModOp.Percent)
                        target.AddAttackerRangeBonusMorePercent(m.damageRangeType, pct / 100f);
                    break;
            }
        }

        private static void ApplyTarget(
            StatModifiers target,
            SpellCombatModifier m,
            int flat,
            float pct
        )
        {
            // For Percent, SpellCombatModifier.value is in "percent" units (10 => 10%),
            // but StatModifiers expects 0.10f in AddMore/AddLess.
            float p = pct / 100f;

            switch (m.target)
            {
                // ---------- Flats ----------
                case SpellCombatModifierTarget.AttackPowerFlat:
                    if (m.op == ModOp.Flat)
                        target.attackPowerFlat += flat;
                    break;

                case SpellCombatModifierTarget.MagicPowerFlat:
                    if (m.op == ModOp.Flat)
                        target.magicPowerFlat += flat;
                    break;

                case SpellCombatModifierTarget.PowerFlat:
                    if (m.op == ModOp.Flat)
                        target.AddPowerFlat(flat);
                    break;

                case SpellCombatModifierTarget.SpellBaseFlat:
                    if (m.op == ModOp.Flat)
                        target.AddSpellBaseFlat(flat);
                    break;

                case SpellCombatModifierTarget.MagicSpellBaseFlat:
                    if (m.op == ModOp.Flat)
                        target.AddMagicSpellBaseFlat(flat);
                    break;

                case SpellCombatModifierTarget.PhysicalSpellBaseFlat:
                    if (m.op == ModOp.Flat)
                        target.AddPhysicalSpellBaseFlat(flat);
                    break;

                case SpellCombatModifierTarget.DamageFlat:
                    if (m.op == ModOp.Flat)
                        target.damageFlat += flat;
                    break;

                case SpellCombatModifierTarget.MagicDamageFlat:
                    if (m.op == ModOp.Flat)
                        target.magicDamageFlat += flat;
                    break;

                case SpellCombatModifierTarget.AttackDamageFlat:
                    if (m.op == ModOp.Flat)
                        target.attackDamageFlat += flat;
                    break;

                case SpellCombatModifierTarget.DefenceFlat:
                    if (m.op == ModOp.Flat)
                        target.defenceFlat += flat;
                    break;

                case SpellCombatModifierTarget.PhysicalDefenseFlat:
                    if (m.op == ModOp.Flat)
                        target.physicalDefenseFlat += flat;
                    break;

                case SpellCombatModifierTarget.MagicDefenseFlat:
                    if (m.op == ModOp.Flat)
                        target.magicDefenseFlat += flat;
                    break;

                case SpellCombatModifierTarget.AttackSpeedFlat:
                    if (m.op == ModOp.Flat)
                        target.attackSpeedFlat += flat;
                    break;

                case SpellCombatModifierTarget.CastingSpeedFlat:
                    if (m.op == ModOp.Flat)
                        target.castingSpeedFlat += flat;
                    break;

                case SpellCombatModifierTarget.PowerScalingFlat:
                    if (m.op == ModOp.Flat)
                        target.AddPowerScalingFlat(m.value);
                    break;

                case SpellCombatModifierTarget.AttackPowerScalingFlat:
                    if (m.op == ModOp.Flat)
                        target.AddAttackPowerScalingFlat(m.value);
                    break;

                case SpellCombatModifierTarget.MagicPowerScalingFlat:
                    if (m.op == ModOp.Flat)
                        target.AddMagicPowerScalingFlat(m.value);
                    break;

                // ---------- More/Less buckets (Percent) ----------
                case SpellCombatModifierTarget.PowerMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddPowerMorePercent(p);
                    break;
                case SpellCombatModifierTarget.PowerLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddPowerLessPercent(p);
                    break;

                case SpellCombatModifierTarget.AttackPowerMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackPowerMorePercent(p);
                    break;
                case SpellCombatModifierTarget.AttackPowerLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackPowerLessPercent(p);
                    break;

                case SpellCombatModifierTarget.MagicPowerMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicPowerMorePercent(p);
                    break;
                case SpellCombatModifierTarget.MagicPowerLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicPowerLessPercent(p);
                    break;

                case SpellCombatModifierTarget.PowerScalingMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddPowerScalingMorePercent(p);
                    break;
                case SpellCombatModifierTarget.PowerScalingLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddPowerScalingLessPercent(p);
                    break;

                case SpellCombatModifierTarget.AttackPowerScalingMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackPowerScalingMorePercent(p);
                    break;
                case SpellCombatModifierTarget.AttackPowerScalingLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackPowerScalingLessPercent(p);
                    break;

                case SpellCombatModifierTarget.MagicPowerScalingMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicPowerScalingMorePercent(p);
                    break;
                case SpellCombatModifierTarget.MagicPowerScalingLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicPowerScalingLessPercent(p);
                    break;

                case SpellCombatModifierTarget.DamageMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddDamageMorePercent(p);
                    break;
                case SpellCombatModifierTarget.DamageLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddDamageLessPercent(p);
                    break;

                case SpellCombatModifierTarget.PhysicalDamageMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalDamageMorePercent(p);
                    break;
                case SpellCombatModifierTarget.PhysicalDamageLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalDamageLessPercent(p);
                    break;

                case SpellCombatModifierTarget.MagicDamageMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicDamageMorePercent(p);
                    break;
                case SpellCombatModifierTarget.MagicDamageLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicDamageLessPercent(p);
                    break;

                case SpellCombatModifierTarget.SpellBaseMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddSpellBaseMorePercent(p);
                    break;
                case SpellCombatModifierTarget.SpellBaseLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddSpellBaseLessPercent(p);
                    break;

                case SpellCombatModifierTarget.MagicSpellBaseMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicSpellBaseMorePercent(p);
                    break;
                case SpellCombatModifierTarget.MagicSpellBaseLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicSpellBaseLessPercent(p);
                    break;

                case SpellCombatModifierTarget.PhysicalSpellBaseMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalSpellBaseMorePercent(p);
                    break;
                case SpellCombatModifierTarget.PhysicalSpellBaseLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalSpellBaseLessPercent(p);
                    break;

                case SpellCombatModifierTarget.HitChanceMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddHitChanceMorePercent(p);
                    break;
                case SpellCombatModifierTarget.HitChanceLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddHitChanceLessPercent(p);
                    break;

                case SpellCombatModifierTarget.CastingSpeedMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddCastingSpeedMorePercent(p);
                    break;
                case SpellCombatModifierTarget.CastingSpeedLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddCastingSpeedLessPercent(p);
                    break;

                case SpellCombatModifierTarget.AttackSpeedMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackSpeedMorePercent(p);
                    break;
                case SpellCombatModifierTarget.AttackSpeedLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddAttackSpeedLessPercent(p);
                    break;

                case SpellCombatModifierTarget.DefenceMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddDefenceMorePercent(p);
                    break;
                case SpellCombatModifierTarget.DefenceLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddDefenceLessPercent(p);
                    break;

                case SpellCombatModifierTarget.PhysicalDefenceMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalDefenceMorePercent(p);
                    break;
                case SpellCombatModifierTarget.PhysicalDefenceLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddPhysicalDefenceLessPercent(p);
                    break;

                case SpellCombatModifierTarget.MagicDefenceMorePercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicDefenceMorePercent(p);
                    break;
                case SpellCombatModifierTarget.MagicDefenceLessPercent:
                    if (m.op == ModOp.Percent)
                        target.AddMagicDefenceLessPercent(p);
                    break;

                // ---------- Type-based arrays ----------
                case SpellCombatModifierTarget.AttackerBonusFlatByType:
                    if (m.op == ModOp.Flat)
                        target.AddAttackerBonusFlat(m.damageType, flat);
                    break;
                case SpellCombatModifierTarget.AttackerBonusMorePercentByType:
                    if (m.op == ModOp.Percent)
                        target.AddAttackerBonusMorePercent(m.damageType, p);
                    break;

                case SpellCombatModifierTarget.DefenderVulnerabilityFlatByType:
                    if (m.op == ModOp.Flat)
                        target.AddDefenderVulnFlat(m.damageType, flat);
                    break;
                case SpellCombatModifierTarget.DefenderVulnerabilityMorePercentByType:
                    if (m.op == ModOp.Percent)
                        target.AddDefenderVulnMorePercent(m.damageType, p);
                    break;

                case SpellCombatModifierTarget.DefenderResistanceFlatByType:
                    if (m.op == ModOp.Flat)
                        target.AddDefenderResistFlat(m.damageType, flat);
                    break;
                case SpellCombatModifierTarget.DefenderResistanceLessPercentByType:
                    if (m.op == ModOp.Percent)
                        target.AddDefenderResistLessPercent(m.damageType, p);
                    break;

                case SpellCombatModifierTarget.AttackerWeakenFlatByType:
                    if (m.op == ModOp.Flat)
                        target.AddAttackerWeakenFlat(m.damageType, flat);
                    break;
                case SpellCombatModifierTarget.AttackerWeakenLessPercentByType:
                    if (m.op == ModOp.Percent)
                        target.AddAttackerWeakenLessPercent(m.damageType, p);
                    break;

                // ---------- Range-based ----------
                case SpellCombatModifierTarget.AttackerRangeBonusFlatByRange:
                    if (m.op == ModOp.Flat)
                        target.AddAttackerRangeBonusFlat(m.damageRangeType, flat);
                    break;
                case SpellCombatModifierTarget.AttackerRangeBonusMorePercentByRange:
                    if (m.op == ModOp.Percent)
                        target.AddAttackerRangeBonusMorePercent(m.damageRangeType, p);
                    break;
            }
        }
    }
}
