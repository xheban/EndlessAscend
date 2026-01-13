using UnityEngine;

[System.Serializable]
public class MonsterSkillEntry
{
    [SerializeField]
    private string skillId;

    [SerializeField]
    private float chance; // 0–1

    public string SkillId => skillId;
    public float Chance => chance;
}
