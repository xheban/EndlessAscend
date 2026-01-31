using System.Collections.Generic;
using MyGame.Combat;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Array/Array Core Definition", fileName = "ArrayCoreDefinition")]
public sealed class ArrayCoreDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Tags")]
    public DamageType[] tags;

    [Header("Visuals")]
    public Sprite image;

    [Header("Bonuses")]
    public List<ArrayBonusEntry> bonuses = new();

    [Header("Costs")]
    [Min(1)]
    public int manaStoneCost = 1;

}
