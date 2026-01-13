using UnityEngine;

[System.Serializable]
public class MonsterLootEntry
{
    [SerializeField]
    private string itemId;

    [SerializeField]
    private float chance; // 0–1

    [SerializeField]
    private int minAmount = 1;

    [SerializeField]
    private int maxAmount = 1;

    public string ItemId => itemId;
    public float Chance => chance;
    public int MinAmount => minAmount;
    public int MaxAmount => maxAmount;
}
