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
}
