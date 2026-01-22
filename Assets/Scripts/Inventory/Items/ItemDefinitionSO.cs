using MyGame.Common;
using UnityEngine;

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

    [Header("Visuals")]
    public Sprite icon;
}
