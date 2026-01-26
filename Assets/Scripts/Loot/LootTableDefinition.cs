using System;
using System.Collections.Generic;
using UnityEngine;

public enum LootPickMode
{
    WithReplacement,
    WithoutReplacement,
}

[Serializable]
public struct LootCountCdfEntry
{
    [Min(0)]
    public int count;

    [Range(0, 100)]
    public int chanceAtLeastPercent;
}

public enum LootDropKind
{
    Item,
    Equipment,
}

[Serializable]
public sealed class LootDropDefinition
{
    public LootDropKind kind;

    [Header("Item")]
    public string itemId;

    [Min(1)]
    public int itemMinAmount = 1;

    [Min(1)]
    public int itemMaxAmount = 1;

    [Header("Equipment")]
    public string equipmentId;
}

[Serializable]
public sealed class LootGuaranteedDrop
{
    public LootDropDefinition drop = new LootDropDefinition();

    [Min(1)]
    public int guaranteedCount = 1;
}

[Serializable]
public sealed class LootWeightedDrop
{
    public LootDropDefinition drop = new LootDropDefinition();

    [Min(0)]
    public int weight = 1;
}

[CreateAssetMenu(menuName = "MyGame/Loot/Loot Table", fileName = "LootTable_")]
public sealed class LootTableDefinition : ScriptableObject
{
    [Header("How many drops?")]
    [SerializeField]
    private List<LootCountCdfEntry> dropCountCdf = new List<LootCountCdfEntry>();

    [Header("Guaranteed drops (do not consume RNG pool)")]
    [SerializeField]
    private List<LootGuaranteedDrop> guaranteedDrops = new List<LootGuaranteedDrop>();

    [Header("Weighted pool (fills remaining slots)")]
    [SerializeField]
    private LootPickMode pickMode = LootPickMode.WithReplacement;

    [SerializeField]
    private List<LootWeightedDrop> weightedPool = new List<LootWeightedDrop>();

    public IReadOnlyList<LootCountCdfEntry> DropCountCdf => dropCountCdf;
    public IReadOnlyList<LootGuaranteedDrop> GuaranteedDrops => guaranteedDrops;
    public LootPickMode PickMode => pickMode;
    public IReadOnlyList<LootWeightedDrop> WeightedPool => weightedPool;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Basic safety so assets stay sane in Inspector.
        for (int i = 0; i < dropCountCdf.Count; i++)
        {
            var e = dropCountCdf[i];
            if (e.count < 0)
                e.count = 0;
            e.chanceAtLeastPercent = Mathf.Clamp(e.chanceAtLeastPercent, 0, 100);
            dropCountCdf[i] = e;
        }

        if (guaranteedDrops != null)
        {
            foreach (var g in guaranteedDrops)
            {
                if (g?.drop == null)
                    continue;

                if (g.drop.kind == LootDropKind.Item)
                {
                    if (g.drop.itemMinAmount < 1)
                        g.drop.itemMinAmount = 1;
                    if (g.drop.itemMaxAmount < 1)
                        g.drop.itemMaxAmount = 1;
                    if (g.drop.itemMaxAmount < g.drop.itemMinAmount)
                        g.drop.itemMaxAmount = g.drop.itemMinAmount;
                }
            }
        }

        if (weightedPool != null)
        {
            foreach (var w in weightedPool)
            {
                if (w?.drop == null)
                    continue;

                if (w.weight < 0)
                    w.weight = 0;

                if (w.drop.kind == LootDropKind.Item)
                {
                    if (w.drop.itemMinAmount < 1)
                        w.drop.itemMinAmount = 1;
                    if (w.drop.itemMaxAmount < 1)
                        w.drop.itemMaxAmount = 1;
                    if (w.drop.itemMaxAmount < w.drop.itemMinAmount)
                        w.drop.itemMaxAmount = w.drop.itemMinAmount;
                }
            }
        }
    }
#endif
}
