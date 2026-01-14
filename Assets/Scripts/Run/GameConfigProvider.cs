using MyGame.Spells;
using UnityEngine;

namespace MyGame.Run
{
    public sealed class GameConfigProvider : MonoBehaviour
    {
        public static GameConfigProvider Instance { get; private set; }

        [Header("Spells")]
        [SerializeField]
        private SpellDatabase spellDatabase;

        [SerializeField]
        private EffectDatabase effectDatabase;

        [SerializeField]
        private SpellProgressionConfig spellProgression;

        [SerializeField]
        private StartingSpellConfig startingSpellConfig;

        [Header("Player")]
        [SerializeField]
        private PlayerIconDatabase playerIconDatabase;

        [SerializeField]
        private PlayerClassDatabase playerClassDatabase;

        public StartingSpellConfig StartingSpellConfig => startingSpellConfig;

        public SpellDatabase SpellDatabase => spellDatabase;
        public EffectDatabase EffectDatabase => effectDatabase;
        public SpellProgressionConfig SpellProgression => spellProgression;

        public PlayerIconDatabase PlayerIconDatabase => playerIconDatabase;

        public PlayerClassDatabase PlayerClassDatabase => playerClassDatabase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // If your dashboard/gameplay spans multiple scenes, uncomment:
            // DontDestroyOnLoad(gameObject);
        }
    }
}
