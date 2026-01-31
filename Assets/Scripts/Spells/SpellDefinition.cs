using MyGame.Combat;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Spells/Spell Definition")]
public class SpellDefinition : ScriptableObject
{
    [Header("Identity")]
    public string spellId;
    public string displayName;
    public string description;
    public Sprite icon;

    [Header("Restrictions")]
    public CharacterClass allowedClasses = CharacterClass.All;
    public CharacterSpec allowedSpecs = CharacterSpec.All;

    [Header("Meta")]
    public Rarity rarity = Rarity.Common;

    [Header("Costs")]
    public int manaCost = 5;
    public int cooldownTurns = 2;

    [Header("Leveling")]
    public int maxLevel = 10;

    [Header("Damage")]
    public int baseDamage = 5;

    [Header("Damage Kind")]
    public DamageKind damageKind = DamageKind.Magical;

    [Header("Damage Range Type")]
    public DamageRangeType damageRangeType = DamageRangeType.Melee;

    [Header("Types")]
    public SpellType[] spellTypes = { SpellType.Damage };

    [Header("Damage Type")]
    public DamageType[] damageTag;

    [Header("Tier")]
    public Tier tier = Tier.Tier1;

    [Header("Defense Ignore")]
    [Min(0)]
    public int ignoreDefenseFlat = 0;

    [Range(0, 100)]
    public int ignoreDefensePercent = 0;

    [Range(0, 100)]
    public int hitChance = 100;

    [Header("Cast time")]
    public int castTimeValue = 100;

    public int baseUseSpeed = 100;

    public int value = 100;

    // ✅ This is the field your code must reference
    public DamageScalingType damageScalingType = DamageScalingType.Additive;

    [Tooltip(
        "Additive: +value per level\nMultiplicative: ×value per level\nExponential: exp(value * levels)"
    )]
    public float damageScalingValue = 5f;

    [Header("Effects")]
    public EffectInstance[] onHitEffects;
    public EffectInstance[] onCastEffects;

    public int GetDamageAtLevel(int level)
    {
        int clamped = Mathf.Clamp(level, 1, maxLevel);
        int l = clamped - 1;

        // Treat damageScalingValue as percentage (20 => 0.2)
        float percent = damageScalingValue / 100f;

        return damageScalingType switch
        {
            // +X flat damage per level (still raw value)
            DamageScalingType.Additive => Mathf.RoundToInt(baseDamage + l * damageScalingValue),

            // +X% per level (20 => +20%)
            DamageScalingType.Multiplicative => Mathf.RoundToInt(
                baseDamage * Mathf.Pow(1f + percent, l)
            ),

            // Exponential, but controlled:
            // percent = 20 => exp(0.2) ≈ 1.22 per level
            DamageScalingType.Exponential => Mathf.RoundToInt(baseDamage * Mathf.Exp(percent * l)),

            _ => baseDamage,
        };
    }

    public bool CanBeUsedBy(CharacterClass playerClass)
    {
        return (allowedClasses & playerClass) != 0;
    }

    public bool CanBeUsedBy(CharacterClass playerClass, CharacterSpec playerSpec)
    {
        bool classOk = (allowedClasses & playerClass) != 0;
        bool specOk = (allowedSpecs & playerSpec) != 0;
        return classOk && specOk;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (manaCost < 0)
            manaCost = 0;
        if (cooldownTurns < 0)
            cooldownTurns = 0;
        if (maxLevel < 1)
            maxLevel = 1;
        if (baseDamage < 0)
            baseDamage = 0;

        // Helpful constraints:
        if (damageScalingType == DamageScalingType.Multiplicative && damageScalingValue < 1f)
            damageScalingValue = 1f; // multiplier < 1 would shrink damage each level
        if (damageScalingType == DamageScalingType.Exponential && damageScalingValue < 0f)
            damageScalingValue = 0f; // negative would shrink with exp
    }
#endif
}
