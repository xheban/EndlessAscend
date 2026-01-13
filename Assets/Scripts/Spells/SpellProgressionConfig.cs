using MyGame.Common;
using UnityEngine;

namespace MyGame.Spells
{
    [CreateAssetMenu(menuName = "MyGame/Spells/Spell Progression Config")]
    public sealed class SpellProgressionConfig : ScriptableObject
    {
        [Header("Base XP for level 1 → 2")]
        public int commonBase = 10;
        public int uncommonBase = 14;
        public int rareBase = 20;
        public int epicBase = 28;
        public int legendaryBase = 40;
        public int mythicalBase = 55;
        public int forbiddenBase = 75;

        [Header("Growth")]
        [Tooltip("Multiplier applied per level. Example: 1.25 = +25% per level.")]
        public float growth = 1.25f;

        public int GetXpToNextLevel(Rarity rarity, int currentLevel)
        {
            int baseXp = rarity switch
            {
                Rarity.Common => commonBase,
                Rarity.Uncommon => uncommonBase,
                Rarity.Rare => rareBase,
                Rarity.Epic => epicBase,
                Rarity.Legendary => legendaryBase,
                Rarity.Mythical => mythicalBase,
                Rarity.Forbidden => forbiddenBase,
                _ => commonBase,
            };

            int lvl = Mathf.Max(1, currentLevel);
            float scaled = baseXp * Mathf.Pow(growth, lvl - 1);
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (commonBase < 1)
                commonBase = 1;
            if (uncommonBase < 1)
                uncommonBase = 1;
            if (rareBase < 1)
                rareBase = 1;
            if (epicBase < 1)
                epicBase = 1;
            if (legendaryBase < 1)
                legendaryBase = 1;
            if (mythicalBase < 1)
                mythicalBase = 1;
            if (forbiddenBase < 1)
                forbiddenBase = 1;

            if (growth < 1f)
                growth = 1f;
        }
#endif
    }
}
