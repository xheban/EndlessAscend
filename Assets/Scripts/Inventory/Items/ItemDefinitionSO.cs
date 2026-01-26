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

    [Header("Use")]
    [Min(0f)]
    public float usageTime = 0f;

    [Min(0)]
    public int cooldownTurns = 0;

    public bool usableInCombat = true;

    public bool carryCooldownBetweenFights = false;

    [Header("Spell Scroll")]
    public bool isSpellScroll = false;

    public SpellScrollData scrollData;

    [Header("Visuals")]
    public Sprite icon;
}
