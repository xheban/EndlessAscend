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
            if (ctx == null || _effects == null)
                return;

            if (ctx.effectInstancesToApply == null || ctx.effectInstancesToApply.Length == 0)
                return;

            _effects.ApplyEffects(
                attacker: ctx.attacker,
                defender: ctx.defender,
                spell: ctx.spell,
                spellLevel: ctx.spellLevel,
                instances: ctx.effectInstancesToApply,
                rng: ctx.rng,
                lastDamageDealt: ctx.lastDamageDealt
            );
        }
    }
}
