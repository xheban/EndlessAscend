using System;
using MyGame.Combat;
using MyGame.Run;
using MyGame.Save;
using MyGame.Spells;
using UnityEngine;

public sealed class CombatSessionCoordinator
{
    private CombatEngine _engine;

    /// <summary>Expose the current engine instance (read-only).</summary>
    public CombatEngine Engine => _engine;

    /// <summary>Raised whenever the engine emits an event (passthrough).</summary>
    public event Action<CombatEvent> OnEvent;

    public event Action OnEnemyDecisionRequested;
    public event Action<CombatEndedEvent> OnCombatEnded;

    public event Action OnPlayerSpellFired;

    private ICombatLogSink _logSink;
    private ICombatUiSink _uiSink;

    /// <summary>
    /// Builds and starts the combat engine for this encounter.
    /// </summary>
    public bool TryStartEncounter(
        SaveData save,
        MonsterDefinition monsterDef,
        int monsterLevel,
        PlayerSpellbook spellbook,
        SpellDatabase spellDatabase,
        EffectDatabase effectDatabase
    )
    {
        if (save == null)
        {
            Debug.LogError("[CombatSessionCoordinator] save is null.");
            return false;
        }

        if (monsterDef == null)
        {
            Debug.LogError("[CombatSessionCoordinator] monsterDef is null.");
            return false;
        }

        if (spellbook == null)
        {
            Debug.LogError("[CombatSessionCoordinator] spellbook is null.");
            return false;
        }

        if (spellDatabase == null)
        {
            Debug.LogError("[CombatSessionCoordinator] spellDatabase is null.");
            return false;
        }

        Stop(); // safety: ensure no previous engine is still running

        var resolver = new SpellDatabaseCombatResolver(spellDatabase);
        _engine = new CombatEngine(resolver, effectDatabase);
        _engine.OnEvent += HandleEngineEvent;

        _engine.StartEncounter(
            save: save,
            monsterDef: monsterDef,
            monsterLevel: monsterLevel,
            spellbook: spellbook
        );

        return true;
    }

    public void Stop()
    {
        if (_engine != null)
        {
            _engine.OnEvent -= HandleEngineEvent;
            _engine = null;
        }
    }

    public void SetLogSink(ICombatLogSink sink)
    {
        _logSink = sink;
    }

    public void SetUiSink(ICombatUiSink sink)
    {
        _uiSink = sink;
    }

    private void HandleEngineEvent(CombatEvent e)
    {
        // Keep the raw passthrough available if you still want it
        OnEvent?.Invoke(e);

        switch (e)
        {
            case CombatLogEvent log:
                _logSink?.LogLine(log.Text);
                break;

            case CombatAdvancedLogEvent adv:
                _logSink?.LogAdvanced(adv.Prefix, adv.Value, adv.Suffix, adv.Type);
                break;

            case HpChangedEvent hp:
                _uiSink?.UpdateHp(hp.Actor, hp.NewHp, hp.MaxHp);
                break;

            case ManaChangedEvent mana:
                _uiSink?.UpdateMana(mana.Actor, mana.NewMana, mana.MaxMana);
                break;

            case TurnMeterChangedEvent tm:
                _uiSink?.UpdateTurn(tm.Actor, tm.NewValue, tm.MaxValue);
                break;

            case SpellQueuedEvent q:
            {
                // Resolve display name here or in controller? We'll keep it lightweight and do it here
                string spellName = GameConfigProvider.Instance?.SpellDatabase.GetDisplayName(
                    q.SpellId
                );
                string text = $"{q.CasterName} casting {spellName}…";
                _uiSink?.SetActionText(q.Actor, text);
                break;
            }

            case SpellFiredEvent fired:
            {
                if (fired.Actor == CombatActorType.Player)
                {
                    _uiSink?.SetActionText(CombatActorType.Player, "Choose action...");
                    Debug.Log("spell firing");
                    OnPlayerSpellFired?.Invoke();
                }
                else
                {
                    _uiSink?.SetActionText(CombatActorType.Enemy, "Enemy deciding...");
                }
                break;
            }

            case EnemyDecisionRequestedEvent:
                OnEnemyDecisionRequested?.Invoke();
                break;

            case CombatEndedEvent end:
                OnCombatEnded?.Invoke(end);
                break;
        }
    }
}
