using MyGame.Combat;
using MyGame.Common;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;

public sealed class OutOfCombatRegenSystem : MonoBehaviour
{
    [SerializeField]
    private float _tickSeconds = 10f;

    private float _timer;

    private void Update()
    {
        if (!SaveSession.HasSave)
            return;

        // ✅ Pause regen while encounter is active
        if (IsInEncounterState.IsInEncounter)
            return;

        _timer += Time.deltaTime;
        if (_timer < _tickSeconds)
            return;

        _timer -= _tickSeconds;

        ApplyTick(_tickSeconds);
    }

    private static void ApplyTick(float tickSeconds)
    {
        var save = SaveSession.Current;

        var equipment = RunSession.Equipment ?? InventorySaveMapper.LoadEquipmentFromSave(save);
        var effectiveBaseStats = PlayerBaseStatsResolver.BuildEffectiveBaseStats(save, equipment);

        var derived = PlayerDerivedStatsResolver.BuildDerivedFromEffectiveBaseStats(
            save,
            effectiveBaseStats,
            equipment
        );

        // Max values from current save
        int maxHp = Mathf.Max(1, derived.maxHp);
        int maxMana = Mathf.Max(0, derived.maxMana);

        // Regen values per 10 seconds
        int hpPer10 = CombatStatCalculator.CalculateOutOfCombatHpRegenPer10s(
            maxHp,
            effectiveBaseStats,
            save.level,
            save.tier
        );

        int manaPer10 = CombatStatCalculator.CalculateOutOfCombatManaRegenPer10s(
            maxMana,
            effectiveBaseStats,
            save.level,
            save.tier
        );

        // Scale to tick length (if you ever change tickSeconds)
        float scale = tickSeconds / 10f;

        int hpGain = Mathf.FloorToInt(hpPer10 * scale);
        int manaGain = Mathf.FloorToInt(manaPer10 * scale);

        int beforeHp = save.currentHp;
        int beforeMana = save.currentMana;
        // Apply
        save.currentHp = Mathf.Clamp(save.currentHp + hpGain, 0, maxHp);
        save.currentMana = Mathf.Clamp(save.currentMana + manaGain, 0, maxMana);

        if (save.currentHp != beforeHp || save.currentMana != beforeMana)
        {
            MyGame.Combat.VitalsChangedBus.Raise(save.currentHp, save.currentMana);
        }
    }
}
