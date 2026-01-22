using MyGame.Combat;
using MyGame.Rewards;
using MyGame.Run;
using MyGame.Save;

public sealed class CombatEndProcessor
{
    private readonly CombatRewardsPipeline _rewards;
    private readonly CombatSaveWriter _saveWriter;

    public CombatEndProcessor(CombatRewardsPipeline rewards, CombatSaveWriter saveWriter)
    {
        _rewards = rewards;
        _saveWriter = saveWriter;
    }

    /// <summary>
    /// Applies the "end of combat" effects to save/progression and returns the reward result.
    /// Does NOT touch UI or navigation.
    /// </summary>
    public CombatRewardResult ProcessEnd(
        bool playerWon,
        CombatEngine engine,
        MonsterDefinition monsterDef,
        TowerRunProgress towers,
        string towerId,
        TowerFloorEntry floorEntry
    )
    {
        // Commit vitals no matter what (if engine exists)
        if (
            engine != null
            && engine.State != null
            && SaveSession.HasSave
            && SaveSession.Current != null
        )
        {
            _saveWriter?.CommitCombatVitalsToSave(engine);
        }

        CombatRewardResult reward = CombatRewardResult.None();

        if (!playerWon)
        {
            SaveSessionRuntimeSave.SaveNowWithRuntime();
            return reward;
        }

        // Victory branch
        if (SaveSession.Current != null && monsterDef != null && floorEntry != null)
        {
            reward = _rewards.GrantVictoryRewards(
                SaveSession.Current,
                monsterDef,
                floorEntry.level
            );

            if (towers != null)
                towers.CompleteFloor(towerId, floorEntry);
        }

        SaveSessionRuntimeSave.SaveNowWithRuntime();
        return reward;
    }
}
