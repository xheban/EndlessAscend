using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class EnemySpellState
    {
        public string spellId;
        public float weight;
        public int level;
        public int cooldownRemainingTurns;

        public EnemySpellState(string spellId, int level, float weight)
        {
            this.spellId = spellId;
            this.weight = Mathf.Max(0.01f, weight);
            this.level = level;
            cooldownRemainingTurns = 0;
        }
    }

    [Serializable]
    public sealed class EnemySpellbookRuntime
    {
        public List<EnemySpellState> spells = new();

        // Tick ALL cooldowns by 1 (classic "a turn passed")
        public void TickCooldowns(int amount = 1)
        {
            if (amount <= 0)
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                var s = spells[i];
                if (s.cooldownRemainingTurns > 0)
                    s.cooldownRemainingTurns = Mathf.Max(0, s.cooldownRemainingTurns - amount);
            }
        }

        // Tick ONE cooldown by spell id (for potions / cooldown-reduction spells)
        public bool TickCooldown(string spellId, int amount = 1)
        {
            if (amount <= 0)
                return false;
            if (string.IsNullOrWhiteSpace(spellId))
                return false;

            var s = spells.Find(x => x.spellId == spellId);
            if (s == null)
                return false;
            if (s.cooldownRemainingTurns <= 0)
                return false;

            s.cooldownRemainingTurns = Mathf.Max(0, s.cooldownRemainingTurns - amount);
            return true;
        }

        public void StartCooldown(string spellId, int turns)
        {
            if (turns <= 0)
                return;
            if (string.IsNullOrWhiteSpace(spellId))
                return;

            var s = spells.Find(x => x.spellId == spellId);
            if (s == null)
                return;

            s.cooldownRemainingTurns = Mathf.Max(s.cooldownRemainingTurns, turns);
        }

        public int GetCooldown(string spellId)
        {
            var s = spells.Find(x => x.spellId == spellId);
            return s != null ? s.cooldownRemainingTurns : 0;
        }
    }
}
