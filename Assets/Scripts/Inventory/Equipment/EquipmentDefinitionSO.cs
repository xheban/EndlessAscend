using System;
using System.Collections.Generic;
using MyGame.Common;
using MyName.Equipment;
using UnityEngine;

[CreateAssetMenu(fileName = "Equipment", menuName = "MyGame/Inventory/Equipment")]
public sealed class EquipmentDefinitionSO : ScriptableObject
{
    [Serializable]
    public struct FlatStatRequirement
    {
        public BaseStatType stat;

        [Min(0)]
        public int minValue;
    }

    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Equipment")]
    public EquipmentSlot slot;

    public Rarity rarity = Rarity.Common;

    [Min(1)]
    public int requiredLevel = 1;

    public Tier tier = Tier.Tier1;

    [Min(1)]
    public int level = 1;

    public EquipmentHandedness handedness;

    [Header("Requirements - Stats (Flat)")]
    public List<FlatStatRequirement> flatStatRequirements = new();

    [Header("Visuals")]
    public Sprite icon;

    [Header("Rolls - Base Stats")]
    [Min(0)]
    public int minBaseRolls = 0;

    [Min(0)]
    public int maxBaseRolls = 0;

    public bool allowDuplicateBaseRolls = false;

    public List<BaseStatRollRule> baseRollTable = new();

    [Header("Rolls - Damage Kind")]
    [Min(0)]
    public int minDamageKindRolls = 0;

    [Min(0)]
    public int maxDamageKindRolls = 0;

    public bool allowDuplicateDamageKindRolls = false;

    public List<DamageKindRollRule> damageKindRollTable = new();

    [Header("Rolls - Damage Range")]
    [Min(0)]
    public int minDamageRangeRolls = 0;

    [Min(0)]
    public int maxDamageRangeRolls = 0;

    public bool allowDuplicateDamageRangeRolls = false;

    public List<DamageRangeRollRule> damageRangeRollTable = new();

    [Header("Rolls - Damage Type")]
    [Min(0)]
    public int minDamageTypeRolls = 0;

    [Min(0)]
    public int maxDamageTypeRolls = 0;

    public bool allowDuplicateDamageTypeRolls = false;

    public List<DamageTypeRollRule> damageTypeRollTable = new();

    [Header("Rolls - Combat Modifiers")]
    [Min(0)]
    public int minCombatModRolls = 0;

    [Min(0)]
    public int maxCombatModRolls = 0;

    public bool allowDuplicateCombatModRolls = false;

    public List<CombatModRollRule> combatModRollTable = new();

    [Header("Rolls - Derived Stats")]
    [Min(0)]
    public int minDerivedRolls = 0;

    [Min(0)]
    public int maxDerivedRolls = 0;

    public bool allowDuplicateDerivedRolls = false;

    public List<DerivedStatRollRule> derivedRollTable = new();
}
