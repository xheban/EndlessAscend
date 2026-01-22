using MyGame.Combat;
using UnityEngine;

public interface ICombatUiSink
{
    void UpdateHp(CombatActorType actor, int newHp, int maxHp);
    void UpdateMana(CombatActorType actor, int newMana, int maxMana);
    void UpdateTurn(CombatActorType actor, int newValue, int maxValue);
    void SetActionText(CombatActorType actor, string text);
    void ShowFloatingNumber(
        CombatActorType source,
        CombatActorType target,
        int amount,
        FloatingNumberKind kind,
        Sprite icon
    );

    // Optional: use this instead of string hacks
    void OnPlayerSpellFired();
    void OnEnemySpellFired();
}
