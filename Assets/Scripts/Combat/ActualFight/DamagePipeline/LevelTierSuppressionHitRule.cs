using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// If attacker is weaker, reduce hit chance.
    /// This is the "hit" part of suppression.
    /// </summary>
    public sealed class LevelTierSuppressionHitRule : IHitRule
    {
        private readonly float _levelPenaltyPerLevel; // 3 => 3% per level
        private readonly float _tierPenaltyPerTier; // 20 => 20% per tier

        public LevelTierSuppressionHitRule(
            float levelPenaltyPerLevel = 3,
            float tierPenaltyPerTier = 3
        )
        {
            _levelPenaltyPerLevel = levelPenaltyPerLevel;
            _tierPenaltyPerTier = tierPenaltyPerTier;
        }

        public void Apply(ActionContext ctx)
        {
            int levelDiff = ctx.defender.level - ctx.attacker.level;
            int tierDiff = (int)ctx.defender.tier - (int)ctx.attacker.tier;

            float penaltyPct =
                (levelDiff > 0 ? levelDiff * _levelPenaltyPerLevel : 0f)
                + (tierDiff > 0 ? tierDiff * _tierPenaltyPerTier : 0f);

            int penalty = Mathf.RoundToInt(penaltyPct);

            ctx.hitChance -= penalty;
        }
    }
}
