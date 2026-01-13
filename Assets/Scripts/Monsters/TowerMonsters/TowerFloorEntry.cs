using System;
using UnityEngine;

[Serializable]
public class TowerFloorEntry
{
    [Min(1)]
    public int floor = 1;

    public MonsterDefinition monster;

    // Instance level decided by tower, not the monster definition
    [Min(1)]
    public int level = 1;

    [Header("Optional overrides (leave 0 to use monster base rewards)")]
    [Min(0)]
    public int expOverride = 0;

    [Min(0)]
    public int goldOverride = 0;
}
