using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Resolves the final damage amount (assuming the action hit, if required).
    /// Applies IDamageRule rules, then computes finalDamage.
    /// </summary>
    public sealed class EffectPhase
    {
        private readonly List<IEffectRule> _rules;

        public EffectPhase(IEnumerable<IEffectRule> rules)
        {
            _rules = new List<IEffectRule>(rules);
        }

        public void Resolve(ActionContext ctx, StatModifiers attackerModifiers)
        {
            // 1) Safety checks
            if (ctx == null || ctx.attacker == null || ctx.defender == null || ctx.spell == null)
            {
                if (ctx != null)
                    ctx.finalDamage = 0;
                return;
            }

            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i]?.Apply(ctx, attackerModifiers);
            }
        }
    }
}
