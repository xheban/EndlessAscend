using MyGame.Progression;
using MyGame.Save;

namespace MyGame.Rewards
{
    public sealed class CombatRewardsPipeline
    {
        private readonly ICombatRewardCalculator _calculator;

        public CombatRewardsPipeline(ICombatRewardCalculator calculator)
        {
            _calculator = calculator;
        }

        public CombatRewardResult GrantVictoryRewards(
            SaveData save,
            MonsterDefinition monster,
            int monsterLevel
        )
        {
            var result = _calculator.Calculate(monster, monsterLevel);

            // Apply to save (adjust field names if yours differ)
            save.exp += result.exp;
            save.gold.Add(result.gold);
            PlayerLevelUp.ApplyLevelUps(save);
            return result;
        }
    }
}
