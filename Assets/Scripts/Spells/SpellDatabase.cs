using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Spells
{
    [CreateAssetMenu(menuName = "MyGame/Spells/Spell Database")]
    public sealed class SpellDatabase : ScriptableObject
    {
        [SerializeField]
        private List<SpellDefinition> spells = new List<SpellDefinition>();

        private Dictionary<string, SpellDefinition> _byId;

        private void OnEnable()
        {
            BuildLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildLookup();
        }
#endif

        private void BuildLookup()
        {
            _byId = new Dictionary<string, SpellDefinition>();

            foreach (var s in spells)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.spellId))
                    continue;

                _byId[s.spellId] = s;
            }
        }

        public SpellDefinition GetById(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
                return null;

            if (_byId == null)
                BuildLookup();

            return _byId.TryGetValue(spellId, out var def) ? def : null;
        }

        public string GetDisplayName(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
                return string.Empty;

            var def = GetById(spellId);
            return def != null ? def.displayName : spellId;
        }

        public IReadOnlyList<SpellDefinition> All => spells;

        // ✅ This must be INSIDE the class
        public List<SpellDefinition> GetSpellsUsableBy(CharacterClass playerClass)
        {
            var result = new List<SpellDefinition>();

            foreach (var s in spells)
            {
                if (s == null)
                    continue;

                if (s.CanBeUsedBy(playerClass))
                    result.Add(s);
            }

            return result;
        }
    }
}
