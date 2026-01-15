using System;
using MyGame.Common;
using UnityEngine;

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

    public int magnitudeFlat = 0; // e.g. +20 flat

    [Range(0, 10000)]
    public int magnitudePercent = 0; // e.g. 50 = 50%

    public EffectMagnitudeBasis magnitudeBasis = EffectMagnitudeBasis.None;

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
    public IntScalingChoice magnitudeFlatScaling;
    public IntScalingChoice magnitudePercentScaling;
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

        // -------------------------
        // Safety clamps
        // -------------------------
        chance = Mathf.Clamp(chance, 0, 100);
        duration = Mathf.Max(1, duration);
        flat = Mathf.Max(0, flat);
        percent = Mathf.Clamp(percent, 0, 10000);

        if (!stackable)
            stacks = 1;
        else
            stacks = Mathf.Max(1, stacks);

        return new EffectInstanceScaledIntValues
        {
            // numeric result
            chancePercent = chance,
            durationTurns = duration,
            magnitudeFlat = flat,
            magnitudePercent = percent,

            // basis / flags
            magnitudeBasis = magnitudeBasis,
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
}
