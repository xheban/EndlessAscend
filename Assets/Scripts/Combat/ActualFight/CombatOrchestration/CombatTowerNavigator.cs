using MyGame.Rewards;
using UnityEngine;

public sealed class CombatTowerNavigator
{
    private readonly ScreenSwapper _swapper;

    // Centralize screen ids here
    private const string CombatScreenId = "combat_tower";
    private const string InsideTowerScreenId = "inside_tower";
    private const string AfterCombatOverlayId = "after_combat_overlay";

    public CombatTowerNavigator(ScreenSwapper swapper)
    {
        _swapper = swapper;
    }

    public void GoToInsideTower(string towerId)
    {
        if (_swapper == null)
            return;

        _swapper.ShowScreen(InsideTowerScreenId, new InsideTowerContext(towerId));
    }

    public void RestartCombat(CombatTowerContext context)
    {
        if (_swapper == null || context == null)
            return;

        _swapper.ShowScreen(CombatScreenId, context);
    }

    public void GoToNextFloor(CombatTowerContext context)
    {
        if (_swapper == null || context == null)
            return;

        _swapper.ShowScreen(
            CombatScreenId,
            new CombatTowerContext(context.towerId, context.floor + 1)
        );
    }

    public void ShowAfterCombatOverlay(
        CombatTowerContext context,
        bool playerWon,
        CombatRewardResult rewards,
        System.Action onRestart,
        System.Action onReturnToTower,
        System.Action onNextFloor
    )
    {
        if (_swapper == null)
            return;

        var ctx = new AfterCombatOverlayContext
        {
            playerWon = playerWon,
            rewards = rewards,
            OnRestartCombat = onRestart,
            OnReturnToTower = onReturnToTower,
            OnNextFloor = onNextFloor,
        };

        _swapper.ShowOverlay(AfterCombatOverlayId, ctx);
    }
}
