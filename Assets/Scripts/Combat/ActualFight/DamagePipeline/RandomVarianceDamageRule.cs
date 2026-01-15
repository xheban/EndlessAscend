using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Random variance applied to damage multiplier.
    /// pct=0.20 => roll in [0.80..1.20]
    /// </summary>
    public sealed class RandomVarianceDamageRule : IDamageRule
    {
        private readonly float _pct;

        public RandomVarianceDamageRule(float pct = 0.20f)
        {
            _pct = Mathf.Max(0f, pct);
        }

        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            // Prefer ctx.rng if present (deterministic), otherwise fallback.
            float roll =
                (ctx.rng != null)
                    ? ctx.rng.Range(1f - _pct, 1f + _pct)
                    : Random.Range(1f - _pct, 1f + _pct);

            ctx.damageMult *= roll;
        }
    }
}
