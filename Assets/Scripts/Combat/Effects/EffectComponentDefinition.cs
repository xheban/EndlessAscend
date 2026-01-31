using System;
using MyGame.Combat;
using MyGame.Common;
using UnityEngine;

[Serializable]
public class EffectComponentDefinition
{
    [Header("Kind")]
    public EffectKind kind = EffectKind.StatModifier;

    // -------------------------
    // Combat stat modifiers (only if Kind = StatModifier)
    // -------------------------
    [Header("Combat Stat Modifier (Kind = StatModifier)")]
    public EffectStat stat = EffectStat.DamageAll;
    public EffectOp op = EffectOp.Percent;

    [Tooltip("Used only for *ByType stats.")]
    public DamageType[] damageType;

    // -------------------------
    // Base / Derived stat modifiers (only if Kind = Base/Derived Stat Modifier)
    // -------------------------
    [Header("Base / Derived Stat Modifier (Kind = BaseStatModifier or DerivedStatModifier)")]
    public ModOp statOp = ModOp.Flat;
    public BaseStatType baseStat = BaseStatType.Strength;
    public DerivedStatType derivedStat = DerivedStatType.AttackPower;

    // -------------------------
    // Periodic tick config (only if Kind = DOT/HOT)
    // -------------------------
    [Header("Periodic (Kind = DOT/HOT)")]
    public EffectTickTiming tickTiming = EffectTickTiming.OnOwnerAction;
}
