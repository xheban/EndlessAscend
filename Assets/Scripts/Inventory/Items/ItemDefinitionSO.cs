using System;
using MyGame.Common;
using UnityEngine;

[Serializable]
public sealed class SpellScrollData
{
    public string spellId;

    [Min(1)]
    public int spellLevel = 1;

    public bool usesPlayerMana = true;
}

[Serializable]
public sealed class ConsumableData
{
    [Min(0f)]
    public float value = 0f;

    public ConsumableValueFrom valueFrom = ConsumableValueFrom.Flat;

    public ConsumableRefillType refillType = ConsumableRefillType.Health;
}

public enum ItemDefinitionType
{
    Material = 0,
    SpellScroll = 1,
    Consumable = 2,
}

public enum ConsumableValueFrom
{
    Flat = 0,
    PercentMax = 1,
    PercentPower = 2,
}

public enum ConsumableRefillType
{
    Health = 0,
    Mana = 1,
    Both = 2,
}

[CreateAssetMenu(fileName = "Item", menuName = "MyGame/Inventory/Item")]
public sealed class ItemDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Progression")]
    public Rarity rarity = Rarity.Common;

    [Min(1)]
    public int requiredLevel = 1;

    public Tier tier = Tier.Tier1;

    [Header("Value")]
    [Min(0)]
    public int goldValue = 0;

    [Header("Type")]
    public ItemDefinitionType itemType = ItemDefinitionType.Material;

    [Header("Use")]
    [Min(0f)]
    public float usageTime = 0f;

    [Min(0)]
    public int cooldownTurns = 0;

    public bool usableInCombat = true;

    public bool carryCooldownBetweenFights = false;

    [Header("Spell Scroll")]
    public SpellScrollData scrollData;

    [Header("Consumable")]
    public ConsumableData consumableData;

    [Header("Visuals")]
    public Sprite icon;
}
