using System.Collections.Generic;
using MyGame.Combat;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Array/Array Node Definition", fileName = "ArrayNodeDefinition")]
public sealed class ArrayNodeDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Tags")]
    public DamageType[] tags;

    [Header("Visuals")]
    public Sprite imageX;
    public Sprite imageY;

    [Header("Bonuses")]
    public List<ArrayBonusEntry> bonuses = new();

    [Header("Costs")]
    [Min(1)]
    public int manaStoneCost = 1;
}
