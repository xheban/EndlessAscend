using MyGame.Rewards;

public interface ICombatRewardCalculator
{
    CombatRewardResult Calculate(MonsterDefinition monster, int monsterLevel);
}
