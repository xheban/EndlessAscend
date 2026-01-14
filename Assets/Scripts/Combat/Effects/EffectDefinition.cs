using MyGame.Combat;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Effects/Effect Definition")]
public class EffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public string displayName;
    public Sprite icon;

    [Header("Kind")]
    public EffectKind kind = EffectKind.StatModifier;

    [Tooltip("Used for UI/cleanse rules. Math is determined by stat/op or DOT/HOT kind.")]
    public EffectPolarity polarity = EffectPolarity.Debuff;

    [Header("Reapply Behavior")]
    public EffectReapplyRule reapplyRule = EffectReapplyRule.AddOnTop;

    [Header("Duration Stacking")]
    public DurationStackMode durationStackMode = DurationStackMode.Refresh;

    [Tooltip(
        "Used only when durationStackMode = Refresh. If true, set remaining = newDuration; if false, remaining = max(remaining, newDuration)."
    )]
    public bool refreshOverridesRemaining = false;

    [Header("Strength Compare (OverwriteIfStronger only)")]
    public EffectStrengthCompareMode compareMode = EffectStrengthCompareMode.ByStrengthRating;

    [Tooltip("Only used when compareMode = ByStrengthRating. Higher wins when overwriting.")]
    public int strengthRating = 0;

    // -------------------------
    // Stat modifier config
    // Used only when kind == StatModifier
    // -------------------------
    [Header("Stat Modifier (only if Kind = StatModifier)")]
    public EffectStat stat = EffectStat.DamageAll;
    public EffectOp op = EffectOp.MorePercent;

    [Tooltip("Used only for *ByType stats.")]
    public DamageType[] damageType;

    // -------------------------
    // Periodic config
    // Used only when kind == DOT/HOT
    // -------------------------
    [Header("Periodic (only if Kind = DOT/HOT)")]
    public EffectTickTiming tickTiming = EffectTickTiming.OnOwnerAction;

    [TextArea(2, 4)]
    public string description;
}
