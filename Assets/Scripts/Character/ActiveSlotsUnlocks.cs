using MyGame.Common;
using MyGame.Save;
using UnityEngine;

public static class ActiveSlotUnlocks
{
    private const int MaxSlots = MyGame.Spells.PlayerSpellbook.ActiveSlotCount;

    public static int Calculate(SaveData save)
    {
        if (save == null)
            return 1;

        int slots = 1;

        // -------------------------
        // Level milestones
        // -------------------------
        if (save.level >= 2)
            slots++;
        if (save.level >= 10)
            slots++;
        if (save.level >= 50)
            slots++;

        // -------------------------
        // Tier unlocks
        // -------------------------
        slots += CountTierUnlocks(save.tier);

        // -------------------------
        // Stats requirement
        // -------------------------
        if (AllStatsAtLeast(save.finalStats, 100))
            slots++;

        // -------------------------
        // One-off unlocks (from save progress)
        // -------------------------
        var progress = save.spellActiveSlotProgress;
        if (progress != null)
        {
            if (progress.specialQuestChainCompleted)
                slots++;

            if (progress.alchemyTinctureDrank)
                slots++;
        }

        return Mathf.Clamp(slots, 1, MaxSlots);
    }

    private static int CountTierUnlocks(Tier tier)
    {
        return tier switch
        {
            Tier.Tier1 => 0,
            Tier.Tier2 => 1,
            Tier.Tier3 => 2,
            Tier.Tier4 => 3,
            Tier.Tier5 => 4,
            Tier.Tier6 => 5,
            _ => 0,
        };
    }

    private static bool AllStatsAtLeast(Stats stats, int min)
    {
        return stats.strength >= min
            && stats.agility >= min
            && stats.intelligence >= min
            && stats.endurance >= min
            && stats.spirit >= min;
    }
}
