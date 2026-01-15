using UnityEngine;

namespace MyGame.Combat
{
    public sealed class DamageBonusRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            // Attack and magic power after bonuses
            int flatBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? attackerModifiers.magicDamageFlat
                    : attackerModifiers.attackDamageFlat + attackerModifiers.damageFlat;

            float multBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? attackerModifiers.MagicDamageMultFinal
                    : attackerModifiers.PhysicalDamageMultFinal + attackerModifiers.DamageMultFinal;

            // final result of damage added is multiplication of finalPower times finalPowerScaling

            ctx.flatDamageBonus += flatBonus;
            ctx.damageMult *= multBonus;
        }
    }
}
