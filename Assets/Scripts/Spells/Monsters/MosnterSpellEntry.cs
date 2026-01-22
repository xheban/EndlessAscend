using UnityEngine;

[System.Serializable]
public class MonsterSpellEntry
{
    [SerializeField]
    private string spellId;

    [SerializeField]
    private int level = 1;

    [SerializeField]
    private float weight = 1f; // selection weight

    public string SpellId => spellId;
    public float Weight => weight;
    public int Level => level;
}
