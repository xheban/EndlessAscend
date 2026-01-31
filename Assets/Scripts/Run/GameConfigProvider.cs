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

        [Header("Arrays")]
        [SerializeField]
        private ArrayCoreDatabaseSO arrayCoreDatabase;

        [SerializeField]
        private ArrayNodeDatabaseSO arrayNodeDatabase;

        [Header("Player")]
        [SerializeField]
        private PlayerAvatarDatabase playerAvatarDatabase;

        [SerializeField]
        private PlayerIconDatabase playerIconDatabase;

        [SerializeField]
        private PlayerClassDatabase playerClassDatabase;

        [Header("Inventory")]
        [SerializeField]
        private ItemDatabase itemDatabase;

        [SerializeField]
        private EquipmentDatabase equipmentDatabase;

        [Header("Unlocks")]
        [SerializeField]
        private UnlockDatabase unlockDatabase;

        public StartingSpellConfig StartingSpellConfig => startingSpellConfig;

        public SpellDatabase SpellDatabase => spellDatabase;
        public EffectDatabase EffectDatabase => effectDatabase;
        public SpellProgressionConfig SpellProgression => spellProgression;

        public ArrayCoreDatabaseSO ArrayCoreDatabase => arrayCoreDatabase;
        public ArrayNodeDatabaseSO ArrayNodeDatabase => arrayNodeDatabase;

        public PlayerAvatarDatabase PlayerAvatarDatabase => playerAvatarDatabase;
        public PlayerIconDatabase PlayerIconDatabase => playerIconDatabase;
        public PlayerClassDatabase PlayerClassDatabase => playerClassDatabase;

        public ItemDatabase ItemDatabase => itemDatabase;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;
        public UnlockDatabase UnlockDatabase => unlockDatabase;

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
