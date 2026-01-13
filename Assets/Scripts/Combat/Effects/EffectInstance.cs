using System;
using MyGame.Common;
using UnityEngine;

[Serializable]
public class EffectInstance
{
    [Header("Effect")]
    public EffectDefinition effect;

    [Header("Base (ALL INT)")]
    [Range(0, 100)]
    public int chancePercent = 20; // 0..100

    [Min(1)]
    public int durationTurns = 2;

    public int magnitudeFlat = 0; // e.g. 20 flat burn

    [Range(0, 100)]
    public int magnitudePercent = 0; // e.g. 50 = 50%

    public EffectMagnitudeBasis magnitudeBasis = EffectMagnitudeBasis.None;

    public bool stackable = true;

    [Min(1)]
    public int maxStacks = 5;

    [Header("Scaling (choose one per field)")]
    public IntScalingChoice chanceScaling;
    public IntScalingChoice durationScaling;
    public IntScalingChoice magnitudeFlatScaling;
    public IntScalingChoice magnitudePercentScaling;
    public IntScalingChoice maxStacksScaling;

    public EffectInstanceScaledIntValues GetScaled(int spellLevel)
    {
        int chance =
            chanceScaling != null
                ? chanceScaling.Evaluate(spellLevel, chancePercent)
                : chancePercent;

        int duration =
            durationScaling != null
                ? durationScaling.Evaluate(spellLevel, durationTurns)
                : durationTurns;

        int flat =
            magnitudeFlatScaling != null
                ? magnitudeFlatScaling.Evaluate(spellLevel, magnitudeFlat)
                : magnitudeFlat;

        int percent =
            magnitudePercentScaling != null
                ? magnitudePercentScaling.Evaluate(spellLevel, magnitudePercent)
                : magnitudePercent;

        int stacks =
            maxStacksScaling != null ? maxStacksScaling.Evaluate(spellLevel, maxStacks) : maxStacks;

        // Safety clamps
        chance = Mathf.Clamp(chance, 0, 100);
        duration = Mathf.Max(1, duration);
        flat = Mathf.Max(0, flat);
        percent = Mathf.Clamp(percent, 0, 100);

        if (!stackable)
            stacks = 1;
        else
            stacks = Mathf.Max(1, stacks);

        return new EffectInstanceScaledIntValues
        {
            chancePercent = chance,
            durationTurns = duration,
            magnitudeFlat = flat,
            magnitudePercent = percent,
            maxStacks = stacks,
            stackable = stackable,
        };
    }
}

[Serializable]
public struct EffectInstanceScaledIntValues
{
    [Range(0, 100)]
    public int chancePercent;
    public int durationTurns;
    public int magnitudeFlat;

    [Range(0, 100)]
    public int magnitudePercent;
    public bool stackable;
    public int maxStacks;
}
