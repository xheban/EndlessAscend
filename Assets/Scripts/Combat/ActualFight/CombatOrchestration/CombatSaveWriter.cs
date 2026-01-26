using MyGame.Combat;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;

public sealed class CombatSaveWriter
{
    /// <summary>
    /// Writes remaining HP/Mana (and any persistent item cooldowns) from combat state back to SaveSession.Current.
    /// Safe: does nothing if save/engine/state missing.
    /// </summary>
    public void CommitCombatVitalsToSave(CombatEngine engine)
    {
        if (engine == null || engine.State == null)
            return;

        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        var save = SaveSession.Current;
        var state = engine.State;

        save.currentHp = state.player.hp;
        save.currentMana = state.player.mana;

        // Safety clamp
        int maxHp = state.player.derived.maxHp;
        int maxMana = state.player.derived.maxMana;

        save.currentHp = Mathf.Clamp(save.currentHp, 0, maxHp);
        save.currentMana = Mathf.Clamp(save.currentMana, 0, maxMana);

        CommitPersistentItemCooldownsToSave(engine, save);
    }

    private static void CommitPersistentItemCooldownsToSave(CombatEngine engine, SaveData save)
    {
        if (engine?.State == null || save == null)
            return;

        var runtime = engine.State.playerItemCooldowns;
        if (runtime == null)
            return;

        save.persistentItemCooldowns ??=
            new System.Collections.Generic.List<SavedItemCooldownEntry>();
        save.persistentItemCooldowns.Clear();

        var itemDb = GameConfigProvider.Instance?.ItemDatabase;
        var items = RunSession.Items;

        foreach (var kv in runtime.GetAllCooldowns())
        {
            string itemId = kv.Key;
            int remaining = kv.Value;

            if (string.IsNullOrWhiteSpace(itemId) || remaining <= 0)
                continue;

            var def = itemDb != null ? itemDb.GetById(itemId) : null;
            if (def == null || !def.carryCooldownBetweenFights)
                continue;

            // If the player no longer has any stack, don't persist cooldown.
            if (items == null || items.GetCount(itemId) <= 0)
                continue;

            save.persistentItemCooldowns.Add(
                new SavedItemCooldownEntry { itemId = itemId, remainingTurns = remaining }
            );
        }
    }
}
