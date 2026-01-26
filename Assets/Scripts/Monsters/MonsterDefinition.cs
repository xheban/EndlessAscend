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

    [SerializeField]
    private Sprite avatar;

    [SerializeField]
    private float size;

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

    [SerializeField]
    private int goldMin;

    [SerializeField]
    private int goldMax;

    [Header("Spells")]
    [SerializeField]
    private List<MonsterSpellEntry> spells = new();
    public IReadOnlyList<MonsterSpellEntry> Spells => spells;

    [Header("Loot (Items + Equipment)")]
    [SerializeField]
    private LootTableDefinition baseLoot;

    [Header("Legacy Loot (deprecated)")]
    [SerializeField, HideInInspector]
    private List<MonsterLootEntry> lootTable = new();

    [Header("Tags")]
    [SerializeField]
    private MonsterTag tags;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Sprite Avatar => avatar;

    public Tier Tier => tier;
    public MonsterRarity Rarity => rarity;

    public Stats BaseStats => baseStats;

    public int BaseExp => baseExp;
    public int BaseGold => baseGold;

    public int GoldMin => (goldMin == 0 && goldMax == 0) ? baseGold : Mathf.Min(goldMin, goldMax);
    public int GoldMax => (goldMin == 0 && goldMax == 0) ? baseGold : Mathf.Max(goldMin, goldMax);
    public float Size => size;

    public IReadOnlyList<MonsterLootEntry> LootTable => lootTable;

    public LootTableDefinition BaseLoot => baseLoot;
    public MonsterTag Tags => tags;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name.Trim().Replace(" ", "_").ToLowerInvariant();

        // Keep gold range sane in the inspector.
        if (goldMin < 0)
            goldMin = 0;
        if (goldMax < 0)
            goldMax = 0;

        // One-time-ish convenience: if this monster was authored with legacy baseGold,
        // and the new range is still unset, mirror it into the range.
        if (goldMin == 0 && goldMax == 0 && baseGold > 0)
        {
            goldMin = baseGold;
            goldMax = baseGold;
        }

        // If a designer sets a fixed range (min==max), keep legacy baseGold aligned
        // so older reward code paths remain consistent until we migrate them.
        if (goldMin > 0 && goldMin == goldMax)
            baseGold = goldMin;
    }
#endif
}
