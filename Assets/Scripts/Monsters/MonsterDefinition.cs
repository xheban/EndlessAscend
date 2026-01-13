using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Monsters/Monster Definition", fileName = "Monster_")]
public class MonsterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string id;

    [SerializeField]
    private string displayName;

    [SerializeField]
    private Sprite icon;

    [Header("Classification")]
    [SerializeField]
    private Tier tier;

    [SerializeField]
    private MonsterRarity rarity;

    [Header("Base Stats")]
    [SerializeField]
    private Stats baseStats;

    [Header("Rewards")]
    [SerializeField]
    private int baseExp;

    [SerializeField]
    private int baseGold;

    [Header("Spells")]
    [SerializeField]
    private List<MonsterSpellEntry> spells = new();
    public IReadOnlyList<MonsterSpellEntry> Spells => spells;

    [Header("Loot Table (empty for now)")]
    [SerializeField]
    private List<MonsterLootEntry> lootTable = new();

    [Header("Tags")]
    [SerializeField]
    private MonsterTag tags;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;

    public Tier Tier => tier;
    public MonsterRarity Rarity => rarity;

    public Stats BaseStats => baseStats;

    public int BaseExp => baseExp;
    public int BaseGold => baseGold;

    public IReadOnlyList<MonsterLootEntry> LootTable => lootTable;
    public MonsterTag Tags => tags;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name.Trim().Replace(" ", "_").ToLowerInvariant();
    }
#endif
}
