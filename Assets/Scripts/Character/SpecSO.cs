using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(fileName = "Subclass", menuName = "MyGame/Classes/Subclass")]
public class SpecSO : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Stats Bonus")]
    public Stats statBonus;

    [Header("Starting Bonuses (new)")]
    public List<BaseStatModifier> baseStatMods = new();
    public List<DerivedStatModifier> derivedStatMods = new();
}
