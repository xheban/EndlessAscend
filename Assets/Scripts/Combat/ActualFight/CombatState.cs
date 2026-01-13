using System;
using MyGame.Spells;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class CombatState
    {
        public CombatActorState player;
        public CombatActorState enemy;

        public bool waitingForPlayerInput;
        public bool waitingForEnemyDecision;
        public bool isFinished;

        // ✅ Cooldowns persist, so combat reads/writes the persistent runtime spellbook.
        public PlayerSpellbook playerSpellbook;
        public EnemySpellbookRuntime enemySpellbook;

        public CombatActorState Get(CombatActorType type)
        {
            return type == CombatActorType.Player ? player : enemy;
        }

        public CombatActorState GetOpponent(CombatActorType type)
        {
            return type == CombatActorType.Player ? enemy : player;
        }
    }
}
