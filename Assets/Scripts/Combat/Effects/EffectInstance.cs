using System;
using MyGame.Common;
using UnityEngine;

[Serializable]
public class EffectComponentInstance
{
    [Header("Magnitude (component)")]
    public int magnitudeFlat = 0;

    [Range(0, 10000)]
    public int magnitudePercent = 0;

    public EffectMagnitudeBasis magnitudeBasis = EffectMagnitudeBasis.None;

    [Header("Scaling (component)")]
    public IntScalingChoice magnitudeFlatScaling;
    public IntScalingChoice magnitudePercentScaling;

    public EffectComponentScaledIntValues GetScaled(int spellLevel)
    {
        int flat =
            magnitudeFlatScaling != null
                ? magnitudeFlatScaling.Evaluate(spellLevel, magnitudeFlat)
                : magnitudeFlat;

        int percent =
            magnitudePercentScaling != null
                ? magnitudePercentScaling.Evaluate(spellLevel, magnitudePercent)
                : magnitudePercent;

        flat = Mathf.Max(0, flat);
        percent = Mathf.Clamp(percent, 0, 10000);

        return new EffectComponentScaledIntValues
        {
            magnitudeFlat = flat,
            magnitudePercent = percent,
            magnitudeBasis = magnitudeBasis,
        };
    }
}

[Serializable]
public struct EffectComponentScaledIntValues
{
    public int magnitudeFlat;
    public int magnitudePercent;
    public EffectMagnitudeBasis magnitudeBasis;
}

[Serializable]
public class EffectInstance
{
    [Header("Effect")]
    public EffectDefinition effect;

    // -------------------------
    // Apply chance / base numbers
    // -------------------------
    [Header("Base (ALL INT)")]
    [Range(0, 100)]
    public int chancePercent = 20; // 0..100

    [Min(1)]
    public int durationTurns = 2;

    [Header("Component Magnitudes (Composite)")]
    public EffectComponentInstance[] componentMagnitudes;

    // -------------------------
    // Stacking / merging behavior (per spell instance)
    // -------------------------
    [Header("Stacking / Merging (per spell instance)")]
    public bool stackable = true;

    [Min(1)]
    public int maxStacks = 5;

    [Tooltip(
        "If true: effects from different sources fuse into one bucket (same effectId). If false: they stay separate per source."
    )]
    public bool mergeable = true;

    [Header("Reapply Behavior (per spell instance)")]
    public EffectReapplyRule reapplyRule = EffectReapplyRule.AddOnTop;

    [Header("Duration Stacking (per spell instance)")]
    public DurationStackMode durationStackMode = DurationStackMode.Refresh;

    [Tooltip(
        "Only when durationStackMode = Refresh. If true set remaining=newDuration, else remaining=max(remaining,newDuration)."
    )]
    public bool refreshOverridesRemaining = false;

    [Header("Strength Compare (OverwriteIfStronger only)")]
    public EffectStrengthCompareMode compareMode = EffectStrengthCompareMode.ByStrengthRating;

    [Tooltip(
        "Only used when compareMode = ByStrengthRating. Higher wins. If <= 0, definition strengthRating can be used (optional)."
    )]
    public int strengthRating = 0;

    [Tooltip("Used to know when to remove the effect from the actor.")]
    public RemoveWhenType removeWhenType = RemoveWhenType.DurationEnds;

    // -------------------------
    // Target selection
    // -------------------------
    [Header("Target")]
    public EffectTarget target = EffectTarget.Enemy;

    // -------------------------
    // Scaling (choose one per field)
    // -------------------------
    [Header("Scaling (choose one per field)")]
    public IntScalingChoice chanceScaling;
    public IntScalingChoice durationScaling;
    public IntScalingChoice maxStacksScaling;

    /// <summary>
    /// Produces final integer values for runtime use,
    /// based on spell level, and snapshots instance behavior fields.
    /// </summary>
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

        int stacks =
            maxStacksScaling != null ? maxStacksScaling.Evaluate(spellLevel, maxStacks) : maxStacks;

        // -------------------------
        // Safety clamps
        // -------------------------
        chance = Mathf.Clamp(chance, 0, 100);
        duration = Mathf.Max(1, duration);

        if (!stackable)
            stacks = 1;
        else
            stacks = Mathf.Max(1, stacks);

        return new EffectInstanceScaledIntValues
        {
            // numeric result
            chancePercent = chance,
            durationTurns = duration,
            stackable = stackable,
            maxStacks = stacks,
            mergeable = mergeable,

            // per-instance behavior snapshot
            reapplyRule = reapplyRule,
            durationStackMode = durationStackMode,
            refreshOverridesRemaining = refreshOverridesRemaining,
            compareMode = compareMode,
            strengthRating = strengthRating,

            // targeting
            target = target,
        };
    }

    public EffectComponentScaledIntValues GetComponentScaled(int spellLevel, int componentIndex)
    {
        if (componentMagnitudes != null && componentIndex >= 0)
        {
            if (
                componentIndex < componentMagnitudes.Length
                && componentMagnitudes[componentIndex] != null
            )
                return componentMagnitudes[componentIndex].GetScaled(spellLevel);
        }

        return new EffectComponentScaledIntValues
        {
            magnitudeFlat = 0,
            magnitudePercent = 0,
            magnitudeBasis = EffectMagnitudeBasis.None,
        };
    }
}
