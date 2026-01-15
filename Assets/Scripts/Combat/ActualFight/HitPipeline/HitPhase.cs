using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class HitPhase
    {
        private readonly List<IHitRule> _rules;

        public HitPhase(IEnumerable<IHitRule> rules)
        {
            _rules = new List<IHitRule>(rules);
        }

        public void Resolve(ActionContext ctx, StatModifiers attackerModifiers)
        {
            if (ctx == null || ctx.attacker == null || ctx.defender == null || ctx.spell == null)
            {
                if (ctx != null)
                    ctx.hit = false;
                return;
            }

            // Clamp before rules
            ctx.hitChance = Mathf.Clamp(ctx.hitChance, 0, 100);

            // Apply hit rules
            for (int i = 0; i < _rules.Count; i++)
                _rules[i]?.Apply(ctx, attackerModifiers);

            // Clamp after rules
            ctx.hitChance = Mathf.Clamp(ctx.hitChance, 0, 100);

            // Roll: [0..99] < hitChance
            int roll = (ctx.rng != null) ? ctx.rng.RangeInt(0, 100) : Random.Range(0, 100);

            ctx.hit = roll < ctx.hitChance;
        }
    }
}
