using System;
using MyGame.Common;
using UnityEngine;

[Serializable]
public struct EffectInstanceScaledIntValues
{
    [Range(0, 100)]
    public int chancePercent;

    public int durationTurns;

    public int magnitudeFlat;

    [Range(0, 100)]
    public int magnitudePercent;

    public EffectMagnitudeBasis magnitudeBasis;

    public bool stackable;
    public int maxStacks;
}
