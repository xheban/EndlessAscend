using System;
using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Converts attacker power into extra flat damage.
    /// Magical -> magicPower, Physical -> attackPower (you can add more kinds later).
    /// </summary>
    public sealed class ApplyEffectRule : IEffectRule
    {
        private readonly CombatEffectSystem _effects;

        public ApplyEffectRule(CombatEffectSystem effects)
        {
            _effects = effects;
        }

        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            if (_effects == null || ctx == null)
                return;

            if (ctx.attacker == null || ctx.defender == null || ctx.spell == null)
                return;

            var instances = ctx.effectInstancesToApply;
            if (instances == null || instances.Length == 0)
                return;

            int spellLevel = Math.Max(1, ctx.spellLevel);
            int lastDamageDealt = Math.Max(0, ctx.lastDamageDealt);

            float basePower =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.derived.magicPower
                    : ctx.attacker.derived.attackPower;

            float bonusFlatPower =
                ctx.spell.damageKind == DamageKind.Magical
                    ? attackerModifiers.magicPowerFlat
                    : attackerModifiers.attackPowerFlat;
            bonusFlatPower += attackerModifiers.powerFlat;

            float bonusMultPower =
                ctx.spell.damageKind == DamageKind.Magical
                    ? attackerModifiers.MagicPowerMultFinal
                    : attackerModifiers.AttackPowerMultFinal;

            bonusMultPower *= attackerModifiers.PowerMultFinal;

            int finalPower = Mathf.FloorToInt((basePower + bonusFlatPower) * bonusMultPower);

            _effects.ApplyEffects(
                attacker: ctx.attacker,
                defender: ctx.defender,
                spell: ctx.spell,
                spellLevel: spellLevel,
                instances: instances,
                lastDamageDealt: lastDamageDealt,
                finalPower: finalPower
            );
        }
    }
}
