using MyGame.Save;
using MyGame.Spells;
using MyGame.Towers;
using UnityEngine;

namespace MyGame.Run
{
    public static class RunSession
    {
        public static PlayerSpellbook Spellbook { get; private set; }
        public static TowerRunProgress Towers { get; private set; }

        public static bool IsInitialized => Spellbook != null && Towers != null;

        public static void InitializeFromSave(
            SaveData save,
            SpellDatabase db,
            SpellProgressionConfig progression
        )
        {
            if (save == null || db == null || progression == null)
            {
                Debug.LogError("[RunSession] InitializeFromSave missing dependencies.");
                return;
            }

            Spellbook = SpellSaveMapper.LoadFromSave(save, db, progression);
            Towers = TowerSaveMapper.LoadFromSave(save);
        }

        public static void ApplyRuntimeToSave(SaveData save)
        {
            if (save == null)
            {
                Debug.LogError("[RunSession] ApplyRuntimeToSave: save is null.");
                return;
            }
            Debug.Log("i am saving");
            SpellSaveMapper.WriteToSave(Spellbook, save);
            TowerSaveMapper.WriteToSave(Towers, save);
        }

        public static void Clear()
        {
            Spellbook = null;
            Towers = null;
        }
    }
}
