using System;
using System.Collections.Generic;
using MyGame.Rewards;
using UnityEngine;

public static class LootTableRoller
{
    public static CombatRewardResult.LootItem[] RollLoot(LootTableDefinition table)
    {
        if (table == null)
            return Array.Empty<CombatRewardResult.LootItem>();

        int dropCount = ResolveDropCountFromCdf(table.DropCountCdf);

        var results = new List<CombatRewardResult.LootItem>(16);

        int guaranteedTotal = 0;
        var guaranteed = table.GuaranteedDrops;
        if (guaranteed != null)
        {
            for (int i = 0; i < guaranteed.Count; i++)
            {
                var g = guaranteed[i];
                if (g?.drop == null)
                    continue;
                if (g.guaranteedCount <= 0)
                    continue;

                for (int k = 0; k < g.guaranteedCount; k++)
                {
                    if (TryAddDrop(g.drop, results))
                        guaranteedTotal++;
                }
            }
        }

        int totalDrops = Mathf.Max(dropCount, guaranteedTotal);
        int remaining = Mathf.Max(0, totalDrops - guaranteedTotal);

        if (remaining <= 0)
            return results.ToArray();

        var pool = table.WeightedPool;
        if (pool == null || pool.Count == 0)
            return results.ToArray();

        // Build a working list for without-replacement.
        List<LootWeightedDrop> working = null;
        if (table.PickMode == LootPickMode.WithoutReplacement)
            working = new List<LootWeightedDrop>(pool);

        for (int i = 0; i < remaining; i++)
        {
            IReadOnlyList<LootWeightedDrop> sourcePool =
                table.PickMode == LootPickMode.WithReplacement ? pool : working;

            var pick = PickWeighted(sourcePool);
            if (pick?.drop == null)
                break;

            TryAddDrop(pick.drop, results);

            if (table.PickMode == LootPickMode.WithoutReplacement)
                working.Remove(pick);
        }

        return results.ToArray();
    }

    private static int ResolveDropCountFromCdf(IReadOnlyList<LootCountCdfEntry> cdf)
    {
        if (cdf == null || cdf.Count == 0)
            return 0;

        int roll = UnityEngine.Random.Range(1, 101); // 1..100

        int best = 0;
        for (int i = 0; i < cdf.Count; i++)
        {
            var e = cdf[i];
            if (e.count < 0)
                continue;

            int chance = Mathf.Clamp(e.chanceAtLeastPercent, 0, 100);
            if (chance >= roll)
                best = Mathf.Max(best, e.count);
        }

        return best;
    }

    private static LootWeightedDrop PickWeighted(IReadOnlyList<LootWeightedDrop> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e == null)
                continue;
            if (e.weight <= 0)
                continue;
            if (e.drop == null)
                continue;

            total += e.weight;
        }

        if (total <= 0)
            return null;

        int r = UnityEngine.Random.Range(1, total + 1);
        int run = 0;

        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e == null)
                continue;
            if (e.weight <= 0)
                continue;
            if (e.drop == null)
                continue;

            run += e.weight;
            if (r <= run)
                return e;
        }

        return null;
    }

    private static bool TryAddDrop(
        LootDropDefinition def,
        List<CombatRewardResult.LootItem> outList
    )
    {
        if (def == null || outList == null)
            return false;

        switch (def.kind)
        {
            case LootDropKind.Item:
            {
                if (string.IsNullOrWhiteSpace(def.itemId))
                    return false;

                int min = Mathf.Max(1, def.itemMinAmount);
                int max = Mathf.Max(1, def.itemMaxAmount);
                if (max < min)
                    max = min;

                int qty = UnityEngine.Random.Range(min, max + 1);
                if (qty <= 0)
                    return false;

                outList.Add(
                    new CombatRewardResult.LootItem
                    {
                        kind = LootDropKind.Item,
                        lootId = def.itemId,
                        stackCount = qty,
                        equipmentInstanceId = null,
                    }
                );
                return true;
            }

            case LootDropKind.Equipment:
            {
                if (string.IsNullOrWhiteSpace(def.equipmentId))
                    return false;

                outList.Add(
                    new CombatRewardResult.LootItem
                    {
                        kind = LootDropKind.Equipment,
                        lootId = def.equipmentId,
                        stackCount = 1,
                        equipmentInstanceId = null,
                    }
                );
                return true;
            }

            default:
                return false;
        }
    }
}
