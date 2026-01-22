using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Helpers;
using MyGame.Rewards;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public class CombatTowerController : MonoBehaviour, IScreenController
{
    [Header("Databases")]
    [SerializeField]
    private TowerDatabase towerDatabase;

    private CombatTowerView _view;
    private ScreenSwapper _swapper;
    private CombatTowerContext _context;
    private TowerDefinition _towerDef;
    private TowerFloorEntry _floorEntry;
    private MonsterDefinition _monsterDef;
    private CombatInputBinder _input;

    [SerializeField]
    private float enemyDecisionDelay = 15f;
    private EnemyDecisionScheduler _enemyScheduler;
    private CombatSessionCoordinator _combat;
    private CombatEndProcessor _endProcessor;
    private CombatSaveWriter _saveWriter;
    private CombatTowerNavigator _nav;
    private CombatRewardsPipeline _rewards;

    [SerializeField]
    private CombatKeybindInputController _keybindInput;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _swapper = swapper;
        _context = context as CombatTowerContext;

        if (_context == null)
        {
            Debug.LogError("[CombatTowerController] Missing CombatTowerContext.");
            return;
        }

        _view = new CombatTowerView();
        _view.Bind(screenHost);
        _enemyScheduler ??= new EnemyDecisionScheduler(this);
        _rewards = new CombatRewardsPipeline(new DefaultCombatRewardCalculator());
        _saveWriter = new CombatSaveWriter();
        _endProcessor = new CombatEndProcessor(_rewards, _saveWriter);
        _nav = new CombatTowerNavigator(_swapper);

        if (!ResolveEncounter())
            return;

        PopulatePlayerUI();
        PopulateEnemyUI();
        InitializeCombatEngine();
        IsInEncounterState.IsInEncounter = true;

        if (_keybindInput == null)
        {
            // Optional: auto-find on same GameObject (safe fallback)
            _keybindInput = GetComponent<CombatKeybindInputController>();
        }

        if (_keybindInput != null)
        {
            _keybindInput.Engine = _combat?.Engine;
            _keybindInput.SpellSlots = _view?.SpellSlots;
        }
        else
        {
            Debug.LogWarning(
                "[CombatTowerController] CombatKeybindInputController is not assigned."
            );
        }

        _input ??= new CombatInputBinder();
        _input.BindRunButton(_view.RunButton, OnRunClicked);
        _input.BindSpellSlots(_view.SpellSlots, OnSpellSlotClicked);
        _view?.RefreshCooldownUI(_combat?.Engine, _view.SpellSlots, _view.SlotViews);
        _view?.SetActionText(CombatActorType.Player, "Choose action...");
    }

    public void Unbind()
    {
        _view?.Unbind();
        _view = null;
        CleanupCombat();
        // unbind Run button
        _input?.UnbindAll();
        // your existing cleanup...
        _swapper = null;
        _context = null;

        _towerDef = null;
        _floorEntry = null;
        _monsterDef = null;

        _nav = null;
        _enemyScheduler?.Cancel();
        _enemyScheduler = null;
        IsInEncounterState.IsInEncounter = false;
    }

    // -------------------------
    // Encounter resolution
    // -------------------------

    private bool ResolveEncounter()
    {
        if (towerDatabase == null)
        {
            Debug.LogError("[CombatTowerController] TowerDatabase not assigned.");
            return false;
        }

        _towerDef = towerDatabase.GetById(_context.towerId);
        if (_towerDef == null)
        {
            Debug.LogError($"[CombatTowerController] Tower '{_context.towerId}' not found.");
            return false;
        }

        _floorEntry = _towerDef.GetFloor(_context.floor);
        if (_floorEntry == null || _floorEntry.monster == null)
        {
            Debug.LogError(
                $"[CombatTowerController] No monster defined for tower '{_context.towerId}', floor {_context.floor}."
            );
            return false;
        }

        _monsterDef = _floorEntry.monster;
        return true;
    }

    // -------------------------
    // UI population
    // -------------------------

    private void PopulatePlayerUI()
    {
        if (_view == null || _view.PlayerPanel == null)
        {
            Debug.LogWarning("[CombatTowerController] Player panel not found (name=\"Player\").");
            return;
        }

        if (!SaveSession.HasSave || SaveSession.Current == null)
        {
            Debug.LogError("[CombatTowerController] No active SaveSession.");
            return;
        }

        var save = SaveSession.Current;
        var cfg = GameConfigProvider.Instance;

        string name = string.IsNullOrWhiteSpace(save.characterName) ? "Player" : save.characterName;

        string className = save.classId ?? string.Empty;
        string specName = save.specId ?? string.Empty;

        var classDb = cfg?.PlayerClassDatabase;
        if (classDb != null)
        {
            var classDef = classDb.GetClass(save.classId);
            if (classDef != null)
                className = classDef.displayName;

            var specDef = classDb.GetSpec(save.specId);
            if (specDef != null)
                specName = specDef.displayName;
        }

        Sprite playerAvatar = cfg?.PlayerAvatarDatabase?.GetSpriteOrNull(save.avatarId);
        Sprite playerIcon = cfg?.PlayerIconDatabase?.GetSpriteOrNull(save.avatarId);

        var data = new PlayerPanelRenderData
        {
            Name = name,
            LevelText = save.level.ToString(),
            TierText = HelperFunctions.ToTierRoman(save.tier),
            ClassText = className,
            SpecText = specName,
            SmallIcon = playerIcon,
            BigIcon = playerAvatar,
        };

        _view.RenderPlayerPanel(data);

        // Keep spell slots population here for now (next step we move it)
        PopulateActiveSpellSlots();
    }

    private void PopulateEnemyUI()
    {
        if (_view == null || _view.EnemyPanel == null)
        {
            Debug.LogWarning("[CombatTowerController] Enemy panel not found (name=\"Enemy\").");
            return;
        }

        // Missing data branch
        if (_monsterDef == null || _floorEntry == null)
        {
            _view.RenderEnemyPanel(
                new EnemyPanelRenderData
                {
                    Name = "MISSING ENEMY",
                    LevelText = "",
                    TierText = "",
                    TagsText = "",
                    SmallIcon = null,
                    BigIcon = null,
                }
            );
            return;
        }

        _view.RenderEnemyPanel(
            new EnemyPanelRenderData
            {
                Name = _monsterDef.DisplayName,
                LevelText = _floorEntry.level.ToString(),
                TierText = HelperFunctions.ToTierRoman(_monsterDef.Tier),
                TagsText = CombatTowerView.FormatMonsterTags(_monsterDef.Tags),
                SmallIcon = _monsterDef.Icon,
                BigIcon = _monsterDef.Avatar,
                Size = _monsterDef.Size,
            }
        );
    }

    private void PopulateActiveSpellSlots()
    {
        if (_view == null)
            return;

        var spellbook = RunSession.Spellbook;
        if (spellbook == null)
        {
            _view.RenderActiveSpellSlots(null);
            return;
        }

        var active = spellbook.GetActiveSpellsInOrder();
        if (active == null || active.Count == 0)
        {
            _view.RenderActiveSpellSlots(null);
            return;
        }

        var spellDb = GameConfigProvider.Instance?.SpellDatabase;

        // Convert your runtime spell entries into render-friendly data
        var list = new List<ActiveSpellSlotData>(active.Count);

        for (int i = 0; i < active.Count; i++)
        {
            var entry = active[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.spellId))
                continue;

            SpellDefinition def = spellDb != null ? spellDb.GetById(entry.spellId) : null;

            list.Add(
                new ActiveSpellSlotData
                {
                    SpellId = entry.spellId,
                    Level = entry.level,
                    DisplayName = def != null ? def.displayName : entry.spellId,
                    Icon = def != null ? def.icon : null,
                    ActiveSlotIndex = entry.activeSlotIndex,
                }
            );
        }

        _view.RenderActiveSpellSlots(list);
    }

    private void InitializeCombatEngine()
    {
        // Safety checks
        if (!SaveSession.HasSave || SaveSession.Current == null)
        {
            Debug.LogError("[CombatTowerController] No active SaveSession.");
            return;
        }

        if (_monsterDef == null || _floorEntry == null)
        {
            Debug.LogError("[CombatTowerController] Missing monster/floor encounter data.");
            return;
        }

        var cfg = GameConfigProvider.Instance;
        if (cfg == null || cfg.SpellDatabase == null)
        {
            Debug.LogError("[CombatTowerController] Missing GameConfigProvider or SpellDatabase.");
            return;
        }

        var spellbook = RunSession.Spellbook;
        if (spellbook == null)
        {
            Debug.LogError("[CombatTowerController] Missing RunSession.Spellbook.");
            return;
        }

        _combat ??= new CombatSessionCoordinator();
        _combat.SetLogSink(_view);
        _combat.SetUiSink(_view);

        _combat.OnEnemyDecisionRequested -= HandleEnemyDecisionRequested;
        _combat.OnCombatEnded -= HandleCombatEnded;
        _combat.OnPlayerSpellFired -= HandlePlayerSpellFired;
        _combat.OnPlayerSpellQueued -= HandlePlayerSpellQueued;
        _combat.OnEnemySpellQueued -= HandleEnemySpellQueued;

        // Hook
        _combat.OnEnemyDecisionRequested += HandleEnemyDecisionRequested;
        _combat.OnCombatEnded += HandleCombatEnded;
        _combat.OnPlayerSpellFired += HandlePlayerSpellFired;
        _combat.OnPlayerSpellQueued += HandlePlayerSpellQueued;
        _combat.OnEnemySpellQueued += HandleEnemySpellQueued;

        bool started = _combat.TryStartEncounter(
            save: SaveSession.Current,
            monsterDef: _monsterDef,
            monsterLevel: _floorEntry.level,
            spellbook: spellbook,
            spellDatabase: cfg.SpellDatabase,
            effectDatabase: cfg.EffectDatabase
        );

        if (!started)
            return;

        // Use coordinator engine as the source of truth
        var engine = _combat?.Engine;

        if (engine != null && engine.State != null)
        {
            var s = engine.State;
            _view?.SetBar(_view.PlayerHpBar, s.player.hp, s.player.derived.maxHp);
            _view?.SetBar(_view.PlayerManaBar, s.player.mana, s.player.derived.maxMana);

            _view?.SetBar(_view.EnemyHpBar, s.enemy.hp, s.enemy.derived.maxHp);
            _view?.SetBar(_view.EnemyManaBar, s.enemy.mana, s.enemy.derived.maxMana);

            _view.RenderEffects(CombatActorType.Player, s.player.activeEffects);
            _view.RenderEffects(CombatActorType.Enemy, s.enemy.activeEffects);
        }
    }

    private void OnRunClicked()
    {
        _saveWriter?.CommitCombatVitalsToSave(_combat?.Engine);
        // 1) save runtime (spells XP + towers) + also saves currentHp/currentMana now
        SaveSessionRuntimeSave.SaveNowWithRuntime();

        // 2) cleanup combat state + UI bindings
        CleanupCombat();

        _nav?.GoToInsideTower(_context.towerId);
    }

    private void CleanupCombat()
    {
        // Unsubscribe from engine events
        if (_combat != null)
        {
            _combat.OnEnemyDecisionRequested -= HandleEnemyDecisionRequested;
            _combat.OnCombatEnded -= HandleCombatEnded;
            _combat.OnPlayerSpellFired -= HandlePlayerSpellFired;
            _combat.Stop();
        }

        // Unregister spell slot callbacks (if we registered them with stored delegates)
        _input?.UnbindAll();
        _enemyScheduler?.Cancel();
        _view?.ClearLog();
    }

    private void ShowAfterCombatOverlay(bool playerWon, CombatRewardResult rewards)
    {
        _nav?.ShowAfterCombatOverlay(
            context: _context,
            playerWon: playerWon,
            rewards: rewards,
            onRestart: () =>
            {
                CleanupCombat();
                _nav.RestartCombat(_context);
            },
            onReturnToTower: () =>
            {
                CleanupCombat();
                _nav.GoToInsideTower(_context.towerId);
            },
            onNextFloor: () =>
            {
                CleanupCombat();
                _nav.GoToNextFloor(_context);
            }
        );
    }

    private void ScheduleEnemyDecision()
    {
        if (_enemyScheduler == null)
            _enemyScheduler = new EnemyDecisionScheduler(this);

        _enemyScheduler.Schedule(
            enemyDecisionDelay,
            () =>
            {
                var engine = _combat?.Engine;
                if (engine == null || engine.State == null || engine.State.isFinished)
                    return;
                // Tell engine: enemy decided, queue spell now
                engine.QueueEnemySpellAfterDecision();
                // Continue simulation until player needs to act again (or fight ends)
                engine.AdvanceUntilPlayerInputOrEnd();
            }
        );
    }

    private void OnSpellSlotClicked(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return;

        var engine = _combat?.Engine;
        if (engine == null)
            return;

        engine.TryUseSpell(spellId);
    }

    private void HandleEnemyDecisionRequested()
    {
        // Still controller-owned because it schedules a coroutine/action.
        ScheduleEnemyDecision();
    }

    private void HandleCombatEnded(CombatEndedEvent end)
    {
        // For now, just forward to your existing logic by calling the old switch case method.
        // Next step we will delete the old HandleCombatEvent completely.
        bool playerWon = end.Winner == CombatActorType.Player;

        _view?.AppendLog(playerWon ? "Victory!" : "Defeat!");

        if (_view?.PlayerAction != null)
            _view.PlayerAction.text = playerWon ? "Winner" : "Defeated";
        if (_view?.EnemyAction != null)
            _view.EnemyAction.text = playerWon ? "Defeated" : "Winner";

        CombatRewardResult reward = CombatRewardResult.None();
        if (_endProcessor != null)
        {
            reward = _endProcessor.ProcessEnd(
                playerWon: playerWon,
                engine: _combat?.Engine,
                monsterDef: _monsterDef,
                towers: RunSession.Towers,
                towerId: _context.towerId,
                floorEntry: _floorEntry
            );
        }

        if (playerWon)
            _view?.AppendLog($"Gained {reward.exp} EXP and {reward.gold} gold.");

        ShowAfterCombatOverlay(playerWon, reward);
    }

    private void HandlePlayerSpellFired()
    {
        if (_view == null || _combat == null)
            return;

        _view.RefreshCooldownUI(_combat.Engine, _view.SpellSlots, _view.SlotViews);
        var s = _combat.Engine?.State;
        if (s != null)
        {
            _view.RenderEffects(CombatActorType.Player, s.player.activeEffects);
            _view.RenderEffects(CombatActorType.Enemy, s.enemy.activeEffects);
        }
    }

    private void HandlePlayerSpellQueued()
    {
        var s = _combat.Engine?.State;
        if (s != null)
        {
            _view.RenderEffects(CombatActorType.Player, s.player.activeEffects);
            _view.RenderEffects(CombatActorType.Enemy, s.enemy.activeEffects);
        }
    }

    private void HandleEnemySpellQueued()
    {
        var s = _combat.Engine?.State;
        if (s != null)
        {
            _view.RenderEffects(CombatActorType.Player, s.player.activeEffects);
            _view.RenderEffects(CombatActorType.Enemy, s.enemy.activeEffects);
        }
    }
}
