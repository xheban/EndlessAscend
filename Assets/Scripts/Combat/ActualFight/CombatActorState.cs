using System;
using MyGame.Common;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class CombatActorState
    {
        // Who is this actor? Player or Enemy.
        public CombatActorType actorType;

        // For UI and logs
        public string displayName;

        // Base stats (STR/AGI/INT/END/SPIRIT)
        public Stats baseStats;

        // Derived stats snapshot (maxHp, attackSpeed, etc.)
        public DerivedCombatStats derived;

        public StatModifiers modifiers = new StatModifiers();

        // Runtime resources
        public int hp;
        public int mana;

        // Speed-based turn order (fills by attackSpeed, spends 1.0 per turn)
        public float turnMeter;
        public string queuedSpellId;

        public bool IsAlive => hp > 0;

        public int level;
        public Tier tier;
        public bool HasQueuedSpell => !string.IsNullOrWhiteSpace(queuedSpellId);

        public CombatActorState(
            CombatActorType type,
            string name,
            int level,
            Tier tier,
            Stats baseStats,
            DerivedCombatStats derived,
            int startHp,
            int startMana
        )
        {
            actorType = type;
            displayName = string.IsNullOrWhiteSpace(name) ? type.ToString() : name;

            this.level = level;
            this.tier = tier;

            this.baseStats = baseStats;
            this.derived = derived;

            hp = Clamp(startHp, 0, derived.maxHp);
            mana = Clamp(startMana, 0, derived.maxMana);

            turnMeter = 0f;
            queuedSpellId = null;
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
