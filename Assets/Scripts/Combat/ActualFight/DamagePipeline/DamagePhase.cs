using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Resolves the final damage amount (assuming the action hit, if required).
    /// Applies IDamageRule rules, then computes finalDamage.
    /// </summary>
    public sealed class DamagePhase
    {
        private readonly List<IDamageRule> _rules;

        public DamagePhase(IEnumerable<IDamageRule> rules)
        {
            _rules = new List<IDamageRule>(rules);
        }

        public void Resolve(ActionContext ctx)
        {
            // 1) Safety checks
            if (ctx == null || ctx.attacker == null || ctx.defender == null || ctx.spell == null)
            {
                if (ctx != null)
                    ctx.finalDamage = 0;
                return;
            }

            // 2) Initialize baseline damage state for this action
            //    Base comes from ResolvedSpell (already includes spell level + caster scaling if you keep that there)
            ctx.baseDamage = Mathf.Max(0, ctx.spell.damage);
            ctx.flatDamageBonus = 0;
            ctx.damageMult = 1f;

            // 3) Apply each damage rule in a deterministic order
            for (int i = 0; i < _rules.Count; i++)
                _rules[i]?.Apply(ctx);

            // 4) Compute final
            //    "Base + Flat" first, then multiply.
            int dmg = ctx.baseDamage + ctx.flatDamageBonus;
            if (dmg < 0)
                dmg = 0;

            float mult = ctx.damageMult;
            if (mult < 0f)
                mult = 0f; // safety

            dmg = Mathf.FloorToInt(dmg * mult);
            if (dmg < 0)
                dmg = 0;

            ctx.finalDamage = dmg;
        }
    }
}
