using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Class", menuName = "MyGame/Classes/Class")]
public class ClassSO : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Base Stats")]
    public Stats baseStats;

    [Header("Spec")]
    public List<SpecSO> spec = new();
}
