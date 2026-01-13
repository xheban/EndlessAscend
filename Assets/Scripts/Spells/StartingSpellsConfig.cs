using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Spells
{
    [CreateAssetMenu(menuName = "MyGame/Spells/Starting Spell Config")]
    public sealed class StartingSpellConfig : ScriptableObject
    {
        [Header("Mage")]
        public List<SpellDefinition> mageStartingSpells = new List<SpellDefinition>();

        [Header("Warrior")]
        public List<SpellDefinition> warriorStartingSpells = new List<SpellDefinition>();

        [Header("Ranger")]
        public List<SpellDefinition> rangerStartingSpells = new List<SpellDefinition>();

        public IReadOnlyList<SpellDefinition> GetForClassId(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId))
                return mageStartingSpells;

            classId = classId.Trim().ToLowerInvariant();

            return classId switch
            {
                "mage" => mageStartingSpells,
                "warrior" => warriorStartingSpells,
                "ranger" => rangerStartingSpells,
                _ => mageStartingSpells,
            };
        }
    }
}
