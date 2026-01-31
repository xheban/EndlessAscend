using UnityEngine;

namespace MyGame.Combat
{
    public interface ICombatEffectCallbacks
    {
        void ApplyDirectDamage(
            CombatActorType source,
            CombatActorType target,
            int amount,
            string effectName,
            Sprite icon
        );

        void ApplyDirectHeal(
            CombatActorType source,
            CombatActorType target,
            int amount,
            string effectName,
            Sprite icon
        );

        void NotifyDerivedStatsChanged(
            CombatActorState actor,
            int oldHp,
            int oldMaxHp,
            int oldMana,
            int oldMaxMana
        );
    }
}
