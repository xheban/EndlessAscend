using System;
using MyGame.Rewards;

public sealed class AfterCombatOverlayContext
{
    // Outcome
    public bool playerWon;

    // Rewards
    public CombatRewardResult rewards;

    // Buttons
    public Action OnRestartCombat;
    public Action OnReturnToTower;
    public Action OnNextFloor;
}
