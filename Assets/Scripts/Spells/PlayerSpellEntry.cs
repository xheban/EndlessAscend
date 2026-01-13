using System;
using UnityEngine;

[Serializable]
public class PlayerSpellEntry
{
    [Tooltip("Matches SpellDefinition.spellId")]
    public string spellId;

    [Min(1)]
    public int level = 1;

    [Min(0)]
    public int experience = 0;

    [Min(0)]
    public int cooldownRemainingTurns = 0;

    /// <summary>
    /// -1 = not active, 0..N = active slot index
    /// </summary>
    public int activeSlotIndex = -1;

    public PlayerSpellEntry(string spellId, int startLevel = 1)
    {
        this.spellId = spellId;
        this.level = Mathf.Max(1, startLevel);
        this.experience = 0;
        this.cooldownRemainingTurns = 0;
        this.activeSlotIndex = -1;
    }
}
