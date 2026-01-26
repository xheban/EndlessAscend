using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/City/Trainer Definition", fileName = "TrainerDefinition")]
public sealed class TrainerDefinitionSO : ScriptableObject
{
    [Serializable]
    public sealed class TeachEntry
    {
        [Tooltip("Spell id from your SpellDatabase/definitions.")]
        public string spellId;

        [Tooltip("Gold cost to learn.")]
        public int goldCost;

        [Tooltip("Minimum player level required.")]
        public int minLevel;

        [Tooltip("Minimum player tier required.")]
        public Tier minTier = Tier.Tier1;

        [Tooltip("Optional: require the player already knows these spells.")]
        public List<string> requiresSpellIds = new();
    }

    [Header("Identity")]
    public string trainerId;
    public string displayName;

    [Header("Presentation")]
    [TextArea(2, 6)]
    public string greetingDialogue;

    public Sprite portrait;

    [Header("Teaches")]
    public List<TeachEntry> teaches = new();
}
