using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Unlocks/Unlock Definition", fileName = "Unlock_")]
public sealed class UnlockDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string unlockId;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Requirements")]
    [Min(0)]
    public int requiredLevel = 0;

    public Stats minStats;

    [Tooltip("All missions that must be completed for this unlock.")]
    public List<string> requiredMissionIds = new List<string>();

    [Header("Presentation")]
    public Sprite icon;
}
