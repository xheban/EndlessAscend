using MyGame.Combat;
using MyGame.Save;
using UnityEngine;

public sealed class CombatSaveWriter
{
    /// <summary>
    /// Writes remaining HP/Mana from combat state back to SaveSession.Current.
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
    }
}
