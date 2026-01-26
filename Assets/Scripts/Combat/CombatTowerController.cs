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

    private EventCallback<PointerEnterEvent>[] _itemTooltipEnters;
    private EventCallback<PointerLeaveEvent>[] _itemTooltipLeaves;
    private EventCallback<PointerOutEvent>[] _itemTooltipOuts;

    private EventCallback<PointerEnterEvent>[] _spellTooltipEnters;
    private EventCallback<PointerLeaveEvent>[] _spellTooltipLeaves;
    private EventCallback<PointerOutEvent>[] _spellTooltipOuts;

    private sealed class EffectTooltipBinding
    {
        public VisualElement slot;
        public EventCallback<PointerEnterEvent> enter;
        public EventCallback<PointerLeaveEvent> leave;
        public EventCallback<PointerOutEvent> outCb;
    }

    private readonly List<EffectTooltipBinding> _effectTooltipBindings = new();

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
            _keybindInput.ItemSlots = _view?.ItemSlots;
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
        _input.BindItemSlots(_view.ItemSlots, OnItemSlotClicked);
        BindCombatTowerSimpleTooltips();
        _view?.RefreshCooldownUI(_combat?.Engine, _view.SpellSlots, _view.SlotViews);
        _view?.RefreshItemCooldownUI(_combat?.Engine);
        _view?.SetActionText(CombatActorType.Player, "Choose action...");
    }

    public void Unbind()
    {
        UnbindCombatTowerSimpleTooltips();
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

    private void BindCombatTowerSimpleTooltips()
    {
        UnbindCombatTowerSimpleTooltips();

        if (_swapper == null || _view == null)
            return;

        BindItemSlotTooltips(_view.ItemSlots);
        BindSpellSlotTooltips(_view.SpellSlots);
        BindEffectSlotTooltips(_view.PlayerBuffSlots);
        BindEffectSlotTooltips(_view.PlayerDebuffSlots);
        BindEffectSlotTooltips(_view.EnemyBuffSlots);
        BindEffectSlotTooltips(_view.EnemyDebuffSlots);
    }

    private void UnbindCombatTowerSimpleTooltips()
    {
        HideSimpleTooltip();

        UnbindItemSlotTooltips();
        UnbindSpellSlotTooltips();
        UnbindEffectSlotTooltips();
    }

    private void BindItemSlotTooltips(VisualElement[] itemSlots)
    {
        UnbindItemSlotTooltips();

        if (_swapper == null)
            return;
        if (itemSlots == null || itemSlots.Length == 0)
            return;

        _itemTooltipEnters = new EventCallback<PointerEnterEvent>[itemSlots.Length];
        _itemTooltipLeaves = new EventCallback<PointerLeaveEvent>[itemSlots.Length];
        _itemTooltipOuts = new EventCallback<PointerOutEvent>[itemSlots.Length];

        for (int i = 0; i < itemSlots.Length; i++)
        {
            var slot = itemSlots[i];
            if (slot == null)
                continue;

            // Ensure the slot root receives pointer events even if children overlap.
            slot.pickingMode = PickingMode.Position;

            int slotIndex = i;
            EventCallback<PointerEnterEvent> onEnter = _ => ShowCombatItemTooltip(slotIndex, slot);
            EventCallback<PointerLeaveEvent> onLeave = _ => HideSimpleTooltip();
            EventCallback<PointerOutEvent> onOut = _ => HideSimpleTooltip();

            _itemTooltipEnters[i] = onEnter;
            _itemTooltipLeaves[i] = onLeave;
            _itemTooltipOuts[i] = onOut;

            slot.RegisterCallback(onEnter, TrickleDown.TrickleDown);
            slot.RegisterCallback(onLeave, TrickleDown.TrickleDown);
            slot.RegisterCallback(onOut, TrickleDown.TrickleDown);
        }
    }

    private void UnbindItemSlotTooltips()
    {
        HideSimpleTooltip();

        var slots = _view?.ItemSlots;
        if (slots == null || _itemTooltipEnters == null)
        {
            _itemTooltipEnters = null;
            _itemTooltipLeaves = null;
            _itemTooltipOuts = null;
            return;
        }

        int count = Mathf.Min(slots.Length, _itemTooltipEnters.Length);
        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            if (_itemTooltipEnters[i] != null)
                slot.UnregisterCallback(_itemTooltipEnters[i], TrickleDown.TrickleDown);
            if (
                _itemTooltipLeaves != null
                && i < _itemTooltipLeaves.Length
                && _itemTooltipLeaves[i] != null
            )
                slot.UnregisterCallback(_itemTooltipLeaves[i], TrickleDown.TrickleDown);
            if (
                _itemTooltipOuts != null
                && i < _itemTooltipOuts.Length
                && _itemTooltipOuts[i] != null
            )
                slot.UnregisterCallback(_itemTooltipOuts[i], TrickleDown.TrickleDown);
        }

        _itemTooltipEnters = null;
        _itemTooltipLeaves = null;
        _itemTooltipOuts = null;
    }

    private void ShowCombatItemTooltip(int slotIndex, VisualElement slot)
    {
        if (_swapper == null || slot == null)
            return;

        string name = GetActiveCombatSlotDisplayName(slotIndex);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _swapper.ShowTooltipAtElement(slot, name);
    }

    private void HideSimpleTooltip()
    {
        _swapper?.HideTooltip();
    }

    private string GetActiveCombatSlotDisplayName(int slotIndex)
    {
        if (!SaveSession.HasSave || SaveSession.Current == null)
            return null;

        var save = SaveSession.Current;
        save.activeCombatSlots ??= new List<SavedCombatActiveSlotEntry>();
        while (save.activeCombatSlots.Count < 4)
            save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

        if (slotIndex < 0 || slotIndex >= save.activeCombatSlots.Count)
            return null;

        var entry = save.activeCombatSlots[slotIndex];
        if (entry == null)
            return null;

        // Equipment precedence.
        if (!string.IsNullOrWhiteSpace(entry.equipmentInstanceId))
        {
            var equipment = RunSession.Equipment;
            var inst = equipment != null ? equipment.GetInstance(entry.equipmentInstanceId) : null;
            if (inst == null)
                return null;

            var db = GameConfigProvider.Instance?.EquipmentDatabase;
            var def = db != null ? db.GetById(inst.equipmentId) : null;
            return string.IsNullOrWhiteSpace(def?.displayName) ? inst.equipmentId : def.displayName;
        }

        if (!string.IsNullOrWhiteSpace(entry.itemId))
        {
            var db = GameConfigProvider.Instance?.ItemDatabase;
            var def = db != null ? db.GetById(entry.itemId) : null;
            return string.IsNullOrWhiteSpace(def?.displayName) ? entry.itemId : def.displayName;
        }

        return null;
    }

    private void BindSpellSlotTooltips(VisualElement[] spellSlots)
    {
        UnbindSpellSlotTooltips();

        if (_swapper == null)
            return;
        if (spellSlots == null || spellSlots.Length == 0)
            return;

        _spellTooltipEnters = new EventCallback<PointerEnterEvent>[spellSlots.Length];
        _spellTooltipLeaves = new EventCallback<PointerLeaveEvent>[spellSlots.Length];
        _spellTooltipOuts = new EventCallback<PointerOutEvent>[spellSlots.Length];

        for (int i = 0; i < spellSlots.Length; i++)
        {
            var slot = spellSlots[i];
            if (slot == null)
                continue;

            // Ensure hover works over child visuals.
            slot.pickingMode = PickingMode.Position;

            EventCallback<PointerEnterEvent> onEnter = _ => ShowCombatSpellTooltip(slot);
            EventCallback<PointerLeaveEvent> onLeave = _ => HideSimpleTooltip();
            EventCallback<PointerOutEvent> onOut = _ => HideSimpleTooltip();

            _spellTooltipEnters[i] = onEnter;
            _spellTooltipLeaves[i] = onLeave;
            _spellTooltipOuts[i] = onOut;

            slot.RegisterCallback(onEnter, TrickleDown.TrickleDown);
            slot.RegisterCallback(onLeave, TrickleDown.TrickleDown);
            slot.RegisterCallback(onOut, TrickleDown.TrickleDown);
        }
    }

    private void UnbindSpellSlotTooltips()
    {
        var slots = _view?.SpellSlots;
        if (slots == null || _spellTooltipEnters == null)
        {
            _spellTooltipEnters = null;
            _spellTooltipLeaves = null;
            _spellTooltipOuts = null;
            return;
        }

        int count = Mathf.Min(slots.Length, _spellTooltipEnters.Length);
        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            if (_spellTooltipEnters[i] != null)
                slot.UnregisterCallback(_spellTooltipEnters[i], TrickleDown.TrickleDown);
            if (
                _spellTooltipLeaves != null
                && i < _spellTooltipLeaves.Length
                && _spellTooltipLeaves[i] != null
            )
                slot.UnregisterCallback(_spellTooltipLeaves[i], TrickleDown.TrickleDown);
            if (
                _spellTooltipOuts != null
                && i < _spellTooltipOuts.Length
                && _spellTooltipOuts[i] != null
            )
                slot.UnregisterCallback(_spellTooltipOuts[i], TrickleDown.TrickleDown);
        }

        _spellTooltipEnters = null;
        _spellTooltipLeaves = null;
        _spellTooltipOuts = null;
    }

    private void ShowCombatSpellTooltip(VisualElement slot)
    {
        if (_swapper == null || slot == null)
            return;

        string spellId = slot.userData as string;
        if (string.IsNullOrWhiteSpace(spellId))
            return;

        var db = GameConfigProvider.Instance?.SpellDatabase;
        var def = db != null ? db.GetById(spellId) : null;
        string name = string.IsNullOrWhiteSpace(def?.displayName) ? spellId : def.displayName;
        _swapper.ShowTooltipAtElement(slot, name);
    }

    private void BindEffectSlotTooltips(VisualElement[] effectSlots)
    {
        if (_swapper == null)
            return;
        if (effectSlots == null || effectSlots.Length == 0)
            return;

        for (int i = 0; i < effectSlots.Length; i++)
        {
            var slot = effectSlots[i];
            if (slot == null)
                continue;

            // Ensure hover works over child icon/labels.
            slot.pickingMode = PickingMode.Position;

            var binding = new EffectTooltipBinding();
            binding.slot = slot;
            binding.enter = _ => ShowCombatEffectTooltip(slot);
            binding.leave = _ => HideSimpleTooltip();
            binding.outCb = _ => HideSimpleTooltip();
            _effectTooltipBindings.Add(binding);

            slot.RegisterCallback(binding.enter, TrickleDown.TrickleDown);
            slot.RegisterCallback(binding.leave, TrickleDown.TrickleDown);
            slot.RegisterCallback(binding.outCb, TrickleDown.TrickleDown);
        }
    }

    private void UnbindEffectSlotTooltips()
    {
        for (int i = 0; i < _effectTooltipBindings.Count; i++)
        {
            var b = _effectTooltipBindings[i];
            if (b?.slot == null)
                continue;

            if (b.enter != null)
                b.slot.UnregisterCallback(b.enter, TrickleDown.TrickleDown);
            if (b.leave != null)
                b.slot.UnregisterCallback(b.leave, TrickleDown.TrickleDown);
            if (b.outCb != null)
                b.slot.UnregisterCallback(b.outCb, TrickleDown.TrickleDown);
        }

        _effectTooltipBindings.Clear();
    }

    private void ShowCombatEffectTooltip(VisualElement slot)
    {
        if (_swapper == null || slot == null)
            return;

        // CombatTowerView.RenderEffects stores EffectDefinition in userData.
        var def = slot.userData as EffectDefinition;
        if (def == null)
            return;

        string name = string.IsNullOrWhiteSpace(def.displayName) ? def.effectId : def.displayName;
        if (string.IsNullOrWhiteSpace(name))
            return;

        _swapper.ShowTooltipAtElement(slot, name);
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

    private void OnItemSlotClicked(int slotIndex)
    {
        var engine = _combat?.Engine;
        if (engine == null)
            return;

        engine.TryUseActiveCombatItemSlot(slotIndex);
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
        _view.RefreshActiveCombatItemSlots();
        _view.RefreshItemCooldownUI(_combat.Engine);
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
