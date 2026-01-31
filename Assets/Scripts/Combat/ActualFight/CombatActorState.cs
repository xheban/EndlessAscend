using System;
using System.Collections.Generic;
using MyGame.Common;

namespace MyGame.Combat
{
    public enum QueuedActionType
    {
        None = 0,
        Spell = 1,
        Item = 2,
    }

    [Serializable]
    public sealed class CombatActorState
    {
        // Who is this actor? Player or Enemy.
        public CombatActorType actorType;
        public int actionIndex;

        // For UI and logs
        public string displayName;

        // Base stats (STR/AGI/INT/END/SPIRIT)
        public Stats baseStats;
        public Stats baseStatsBase; // baseline before effect buffs

        // Derived stats snapshot (maxHp, attackSpeed, etc.)
        public DerivedCombatStats derived;
        public readonly List<DerivedStatModifier> baseDerivedStatMods =
            new List<DerivedStatModifier>();

        public StatModifiers modifiers = new StatModifiers();
        public readonly List<ActiveEffect> activeEffects = new List<ActiveEffect>(16);

        // Runtime resources
        public int hp;
        public int mana;
        public int lastDamageTaken;

        // Speed-based turn order (fills by attackSpeed, spends 1.0 per turn)
        public float turnMeter;
        public string queuedActionId;
        public QueuedActionType queuedActionType;

        public bool IsAlive => hp > 0;

        public int level;
        public Tier tier;
        public bool HasQueuedAction =>
            queuedActionType != QueuedActionType.None && !string.IsNullOrWhiteSpace(queuedActionId);

        public CombatActorState(
            CombatActorType type,
            string name,
            int level,
            Tier tier,
            Stats baseStats,
            DerivedCombatStats derived,
            int startHp,
            int startMana,
            List<DerivedStatModifier> baseDerivedMods = null
        )
        {
            actorType = type;
            displayName = string.IsNullOrWhiteSpace(name) ? type.ToString() : name;

            this.level = level;
            this.tier = tier;

            this.baseStats = baseStats;
            this.baseStatsBase = baseStats;
            this.derived = derived;

            hp = Clamp(startHp, 0, derived.maxHp);
            mana = Clamp(startMana, 0, derived.maxMana);
            lastDamageTaken = 0;

            if (baseDerivedMods != null && baseDerivedMods.Count > 0)
                baseDerivedStatMods.AddRange(baseDerivedMods);

            turnMeter = 0f;
            queuedActionId = null;
            queuedActionType = QueuedActionType.None;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }
    }
}
