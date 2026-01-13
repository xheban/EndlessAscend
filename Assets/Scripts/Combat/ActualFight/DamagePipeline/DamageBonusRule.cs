using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Converts attacker power into extra flat damage.
    /// Magical -> magicPower, Physical -> attackPower (you can add more kinds later).
    /// </summary>
    public sealed class DamageBonusRule : IDamageRule
    {
        public void Apply(ActionContext ctx)
        {
            // Attack and magic power after bonuses
            float flatBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.modifiers.magicDamageFlat
                    : ctx.attacker.modifiers.attackDamageFlat + ctx.attacker.modifiers.damageFlat;

            float multBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.modifiers.MagicDamageMultFinal
                    : ctx.attacker.modifiers.PhysicalDamageMultFinal
                        + ctx.attacker.modifiers.DamageMultFinal;

            // final result of damage added is multiplication of finalPower times finalPowerScaling
            int bonus = Mathf.FloorToInt(flatBonus * multBonus);

            if (bonus < 0)
                bonus = 0;

            ctx.flatDamageBonus += bonus;
        }
    }
}
