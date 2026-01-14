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

    [TextArea(2, 4)]
    public string description;

    // -------------------------
    // What the effect IS (stable meaning)
    // -------------------------

    [Header("Kind (what this effect is)")]
    public EffectKind kind = EffectKind.StatModifier;

    [Tooltip("Used for UI/cleanse rules.")]
    public EffectPolarity polarity = EffectPolarity.Debuff;

    // -------------------------
    // Stat modifier mapping (only if Kind == StatModifier)
    // This stays in definition so effectId always means the same stat/op.
    // -------------------------
    [Header("Stat Modifier Mapping (only if Kind = StatModifier)")]
    public EffectStat stat = EffectStat.DamageAll;

    public EffectOp op = EffectOp.MorePercent;

    [Tooltip("Used only for *ByType stats.")]
    public DamageType[] damageType;

    // -------------------------
    // Periodic configuration (only if Kind == DOT/HOT)
    // This stays in definition so Burn always ticks the same way.
    // -------------------------
    [Header("Periodic (only if Kind = DOT/HOT)")]
    public EffectTickTiming tickTiming = EffectTickTiming.OnOwnerAction;
}
