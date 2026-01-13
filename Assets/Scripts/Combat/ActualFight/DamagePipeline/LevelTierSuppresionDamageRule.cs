using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Damage suppression based on level & tier difference.
    /// If attacker stronger => bonus damage.
    /// If attacker weaker  => reduced damage.
    /// </summary>
    public sealed class LevelTierSuppressionDamageRule : IDamageRule
    {
        private readonly float _levelFactor; // 0.03 => 3% per level delta
        private readonly float _tierFactor; // 0.20 => 20% per tier delta
        private readonly float _minMult; // prevents extreme reductions to 0

        public LevelTierSuppressionDamageRule(
            float levelFactor = 0.03f,
            float tierFactor = 0.20f,
            float minMult = 0.05f
        )
        {
            _levelFactor = levelFactor;
            _tierFactor = tierFactor;
            _minMult = minMult;
        }

        public void Apply(ActionContext ctx)
        {
            int levelDelta = ctx.attacker.level - ctx.defender.level;
            int tierDelta = (int)ctx.attacker.tier - (int)ctx.defender.tier;

            float effect = levelDelta * _levelFactor + tierDelta * _tierFactor;
            // effect > 0 => attacker stronger => bonus
            // effect < 0 => attacker weaker  => penalty

            float mult = 1f + effect;

            // If attacker is weaker, mult can drop below 0; clamp
            mult = Mathf.Max(_minMult, mult);

            ctx.damageMult *= mult;
        }
    }
}
