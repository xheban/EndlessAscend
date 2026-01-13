namespace MyGame.Combat
{
    /// <summary>
    /// Base type for all combat events emitted by the engine.
    /// </summary>
    public abstract class CombatEvent { }

    /// <summary>
    /// A human-readable line for the combat log.
    /// </summary>
    public sealed class CombatLogEvent : CombatEvent
    {
        public string Text { get; }

        public CombatLogEvent(string text)
        {
            Text = text;
        }
    }

    public sealed class CombatAdvancedLogEvent : CombatEvent
    {
        public readonly string Prefix;
        public readonly int Value;
        public readonly string Suffix;
        public readonly CombatLogType Type;

        public CombatAdvancedLogEvent(string prefix, int value, string suffix, CombatLogType type)
        {
            Prefix = prefix;
            Value = value;
            Suffix = suffix;
            Type = type;
        }
    }

    /// <summary>
    /// Turn meter changed for an actor.
    /// NewValue is 0..100
    /// </summary>
    public sealed class TurnMeterChangedEvent : CombatEvent
    {
        public CombatActorType Actor { get; }
        public int NewValue { get; } // 0..100
        public int MaxValue { get; } // should be 100

        public TurnMeterChangedEvent(CombatActorType actor, int newValue, int maxValue)
        {
            Actor = actor;
            NewValue = newValue;
            MaxValue = maxValue;
        }
    }

    /// <summary>
    /// HP changed for an actor.
    /// Delta = NewHp - OldHp
    /// </summary>
    public sealed class HpChangedEvent : CombatEvent
    {
        public CombatActorType Actor { get; }
        public int NewHp { get; }
        public int MaxHp { get; }
        public int Delta { get; }

        public HpChangedEvent(CombatActorType actor, int newHp, int maxHp, int delta)
        {
            Actor = actor;
            NewHp = newHp;
            MaxHp = maxHp;
            Delta = delta;
        }
    }

    /// <summary>
    /// Mana changed for an actor.
    /// Delta = NewMana - OldMana
    /// </summary>
    public sealed class ManaChangedEvent : CombatEvent
    {
        public CombatActorType Actor { get; }
        public int NewMana { get; }
        public int MaxMana { get; }
        public int Delta { get; }

        public ManaChangedEvent(CombatActorType actor, int newMana, int maxMana, int delta)
        {
            Actor = actor;
            NewMana = newMana;
            MaxMana = maxMana;
            Delta = delta;
        }
    }

    /// <summary>
    /// Combat ended and we have a winner.
    /// </summary>
    public sealed class CombatEndedEvent : CombatEvent
    {
        public CombatActorType Winner { get; }

        public CombatEndedEvent(CombatActorType winner)
        {
            Winner = winner;
        }
    }

    public sealed class EnemyDecisionRequestedEvent : CombatEvent
    {
        public string EnemyName { get; }

        public EnemyDecisionRequestedEvent(string enemyName)
        {
            EnemyName = enemyName;
        }
    }

    public sealed class SpellFiredEvent : CombatEvent
    {
        public CombatActorType Actor { get; }

        public SpellFiredEvent(CombatActorType actor)
        {
            Actor = actor;
        }
    }

    public sealed class SpellQueuedEvent : CombatEvent
    {
        public CombatActorType Actor { get; }
        public string CasterName { get; }
        public string SpellId { get; }

        public SpellQueuedEvent(CombatActorType actor, string casterName, string spellId)
        {
            Actor = actor;
            CasterName = casterName;
            SpellId = spellId;
        }
    }
}
