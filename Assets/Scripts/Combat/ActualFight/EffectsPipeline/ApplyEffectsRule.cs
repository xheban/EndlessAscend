using System;
using System.Diagnostics;
using MyGame.Common;

namespace MyGame.Combat
{
    public sealed class ApplyEffectsRule : IEffectRule
    {
        private readonly CombatEffectSystem _effects;

        public ApplyEffectsRule(CombatEffectSystem effects)
        {
            _effects = effects;
        }

        public void Apply(ActionContext ctx)
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
            _effects.ApplyEffects(
                attacker: ctx.attacker,
                defender: ctx.defender,
                spell: ctx.spell,
                spellLevel: spellLevel,
                instances: instances,
                rng: ctx.rng,
                lastDamageDealt: lastDamageDealt
            );
        }
    }
}
