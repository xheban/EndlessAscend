using MyGame.Common;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Converts attacker power into extra flat damage.
    /// Magical -> magicPower, Physical -> attackPower (you can add more kinds later).
    /// </summary>
    public sealed class PowerScalingDamageRule : IDamageRule
    {
        private readonly float _percentOfPower;

        public PowerScalingDamageRule(float percentOfPower = 0.50f)
        {
            _percentOfPower = Mathf.Max(0f, percentOfPower);
        }

        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            // Attack and magic power after bonuses
            float basePower =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.derived.magicPower
                    : ctx.attacker.derived.attackPower;

            float bonusFlatPower =
                (
                    ctx.spell.damageKind == DamageKind.Magical
                        ? ctx.attacker.modifiers.magicPowerFlat
                        : ctx.attacker.modifiers.attackPowerFlat
                ) + ctx.attacker.modifiers.powerFlat;

            float bonusMultPower =
                (
                    ctx.spell.damageKind == DamageKind.Magical
                        ? ctx.attacker.modifiers.MagicPowerMultFinal
                        : ctx.attacker.modifiers.AttackPowerMultFinal
                ) * ctx.attacker.modifiers.PowerMultFinal;

            float finalPower = (basePower + bonusFlatPower) * bonusMultPower;

            // percentage of power multipliers and flat bonuses meaning how much one AP or MP will afffect the spell damge. bose is 0.5 damage per one AP or MP
            float bonusFlatPercentageOfPower =
                (
                    ctx.spell.damageKind == DamageKind.Magical
                        ? ctx.attacker.modifiers.magicPowerScalingFlat
                        : ctx.attacker.modifiers.attackPowerScalingFlat
                ) + ctx.attacker.modifiers.powerScalingFlat;

            float bonusMultPercentageOfPower =
                (
                    ctx.spell.damageKind == DamageKind.Magical
                        ? ctx.attacker.modifiers.MagicPowerScalingMultFinal
                        : ctx.attacker.modifiers.AttackPowerScalingMultFinal
                ) * ctx.attacker.modifiers.PowerScalingMultFinal;

            float finalPercentageOfPower =
                (_percentOfPower + bonusFlatPercentageOfPower) * bonusMultPercentageOfPower;

            // final result of damage added is multiplication of finalPower times finalPowerScaling
            int bonus = Mathf.FloorToInt(finalPower * finalPercentageOfPower);
            ctx.lastDamageDealtPower = Mathf.FloorToInt(finalPower);
            ctx.flatDamageBonus = bonus;
        }
    }
}
