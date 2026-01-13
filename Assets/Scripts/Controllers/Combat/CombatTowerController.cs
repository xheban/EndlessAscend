using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Rewards;
using MyGame.Run;
using MyGame.Save;
using MyGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

public class CombatTowerController : MonoBehaviour, IScreenController
{
    [Header("Databases")]
    [SerializeField]
    private TowerDatabase towerDatabase;

    private ScreenSwapper _swapper;
    private CombatTowerContext _context;

    private TowerDefinition _towerDef;
    private TowerFloorEntry _floorEntry;
    private MonsterDefinition _monsterDef;

    // --- UI (scoped) ---
    private VisualElement _playerPanel; // name="Player"
    private VisualElement _enemyPanel; // name="Enemy"

    private Label _playerAction;
    private Label _enemyAction;
    private VisualElement _playerBigIcon; // name="PlayerIcon"
    private VisualElement _enemyBigIcon; // name="EnemyIcon"

    private VisualElement _skillBar;
    private VisualElement[] _spellSlots;

    private SpellSlotView[] _slotViews;

    private PixelProgressBar _playerHpBar;
    private PixelProgressBar _playerManaBar;
    private PixelProgressBar _enemyHpBar;
    private PixelProgressBar _enemyManaBar;

    private PixelProgressBar _playerTurnBar;
    private PixelProgressBar _enemyTurnBar;

    private VisualElement _logRoot; // name="Log"
    private ScrollView _logScroll;
    private Button _runButton;
    private EventCallback<ClickEvent>[] _spellSlotCallbacks;

    // -------------------------
    // Cooldown visuals (UI only)
    // -------------------------
    private const string CooldownOverlayName = "CooldownOverlay";
    private const string CooldownLabelName = "CooldownLabel";

    [SerializeField]
    private float enemyDecisionDelay = 15f; // set to 0.5–3 in inspector

    private Coroutine _enemyDecisionRoutine;

    // COMBAT DATA
    private CombatEngine _engine;
    private CombatRewardsPipeline _rewards;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _swapper = swapper;
        _context = context as CombatTowerContext;

        if (_context == null)
        {
            Debug.LogError("[CombatTowerController] Missing CombatTowerContext.");
            return;
        }

        // Cache UI roots (important: duplicate names exist on both sides)
        _playerPanel = screenHost.Q<VisualElement>("PlayerSection");
        _enemyPanel = screenHost.Q<VisualElement>("EnemySection");

        _playerAction = screenHost.Q<Label>("PlayerAction");
        _enemyAction = screenHost.Q<Label>("EnemyAction");
        _playerBigIcon = screenHost.Q<VisualElement>("PlayerIcon");
        _enemyBigIcon = screenHost.Q<VisualElement>("EnemyIcon");

        _skillBar = screenHost.Q<VisualElement>("SkillBar");
        _runButton = screenHost.Q<Button>("Flee");
        SetButtonClick(_runButton, OnRunClicked);

        _rewards = new CombatRewardsPipeline(new DefaultCombatRewardCalculator());

        _spellSlots = new VisualElement[12];
        for (int i = 0; i < _spellSlots.Length; i++)
        {
            _spellSlots[i] = _skillBar?.Q<VisualElement>($"SpellSlot{i + 1}");
        }

        _slotViews = new SpellSlotView[_spellSlots.Length];
        for (int i = 0; i < _spellSlots.Length; i++)
        {
            if (_spellSlots[i] != null)
                _slotViews[i] = new SpellSlotView(_spellSlots[i]);
        }

        if (!ResolveEncounter())
            return;

        CacheCombatHud(screenHost);
        PopulatePlayerUI();
        PopulateEnemyUI();
        InitializeCombatEngine();
        BindSpellSlotClicks();
        RefreshCooldownUI();
    }

    public void Unbind()
    {
        // stop combat & detach engine events + slot callbacks
        CleanupCombat();

        // unbind Run button
        ClearButtonClick(_runButton);
        _runButton = null;

        // your existing cleanup...
        _swapper = null;
        _context = null;

        _towerDef = null;
        _floorEntry = null;
        _monsterDef = null;

        _playerPanel = null;
        _enemyPanel = null;
        _playerBigIcon = null;
        _enemyBigIcon = null;

        _skillBar = null;
        _spellSlots = null;
        _playerAction = null;
        _enemyAction = null;

        if (_enemyDecisionRoutine != null)
        {
            StopCoroutine(_enemyDecisionRoutine);
            _enemyDecisionRoutine = null;
        }
    }

    private void CacheCombatHud(VisualElement screenHost)
    {
        // Player bars live inside the Player panel (unique because we query from _playerPanel)
        _playerHpBar = _playerPanel?.Q<PixelProgressBar>("Hp");
        _playerManaBar = _playerPanel?.Q<PixelProgressBar>("Mana");

        // Enemy bars live inside the Enemy panel
        _enemyHpBar = _enemyPanel?.Q<PixelProgressBar>("Hp");
        _enemyManaBar = _enemyPanel?.Q<PixelProgressBar>("Mana");

        // Log container is a VisualElement named "Log" (bottom right)
        _logRoot = screenHost?.Q<VisualElement>("Log");

        _playerTurnBar = _playerPanel?.Q<PixelProgressBar>("TurnMeter");
        _enemyTurnBar = _enemyPanel?.Q<PixelProgressBar>("TurnMeter");
        _playerAction.text = "Choose action...";

        if (_playerTurnBar == null)
            Debug.LogWarning("[CombatTowerController] Player Turn bar not found (Player/Turn).");
        if (_enemyTurnBar == null)
            Debug.LogWarning("[CombatTowerController] Enemy Turn bar not found (Enemy/Turn).");

        // Create a ScrollView inside Log so we can append lines.
        // Your UXML Log currently has no children, so we create them at runtime.
        if (_logRoot != null)
        {
            _logRoot.Clear();

            _logScroll = new ScrollView();
            _logScroll.style.flexGrow = 1;
            _logScroll.style.paddingLeft = 16;
            _logScroll.style.paddingRight = 16;
            _logScroll.style.paddingTop = 16;
            _logScroll.style.paddingBottom = 16;

            _logRoot.Add(_logScroll);
        }
        else
        {
            Debug.LogWarning("[CombatTowerController] Log root not found (name='Log').");
            _logScroll = null;
        }

        if (_playerHpBar == null)
            Debug.LogWarning("[CombatTowerController] Player Hp bar not found (Player/Hp).");
        if (_playerManaBar == null)
            Debug.LogWarning("[CombatTowerController] Player Mana bar not found (Player/Mana).");
        if (_enemyHpBar == null)
            Debug.LogWarning("[CombatTowerController] Enemy Hp bar not found (Enemy/Hp).");
        if (_enemyManaBar == null)
            Debug.LogWarning("[CombatTowerController] Enemy Mana bar not found (Enemy/Mana).");
    }

    private void SetBar(PixelProgressBar bar, int current, int max)
    {
        if (bar == null)
            return;

        int safeMax = Mathf.Max(1, max);
        int safeVal = Mathf.Clamp(current, 0, safeMax);

        bar.SetRange(0, safeMax);
        bar.SetValue(safeVal);
    }

    private void AppendLog(string prefix, int value, string suffix, CombatLogType type)
    {
        if (_logScroll == null)
            return;

        // Row container for "prefix [number] suffix"
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 1;
        row.style.marginBottom = 1;

        // Prefix
        var left = new Label(string.IsNullOrEmpty(prefix) ? "" : (prefix + " "));
        left.AddToClassList("combat-log-line");
        left.style.color = Color.white;

        // Number
        var number = new Label(value.ToString());
        number.AddToClassList("combat-log-line");

        // Inline color overrides (guaranteed to work)
        switch (type)
        {
            case CombatLogType.Damage:
                number.style.color = new Color(0.85f, 0.2f, 0.2f); // red
                break;
            case CombatLogType.Heal:
                number.style.color = new Color(0.2f, 0.85f, 0.4f); // green
                break;
            default:
                number.style.color = Color.white;
                break;
        }

        // Suffix
        var right = new Label(string.IsNullOrEmpty(suffix) ? "" : (" " + suffix));
        right.AddToClassList("combat-log-line");
        right.style.color = Color.white;

        row.Add(left);
        row.Add(number);
        row.Add(right);

        _logScroll.Add(row);

        // Scroll to bottom
        _logScroll.schedule.Execute(() =>
        {
            _logScroll.scrollOffset = new Vector2(0, float.MaxValue);
        });
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (_logScroll == null)
            return;

        var label = new Label(line);
        label.AddToClassList("combat-log-line");
        label.style.color = Color.white;
        label.style.marginTop = 1;
        label.style.marginBottom = 1;

        _logScroll.Add(label);

        _logScroll.schedule.Execute(() =>
        {
            _logScroll.scrollOffset = new Vector2(0, float.MaxValue);
        });
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
        if (_playerPanel == null)
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

        // Name + level
        SetScopedText(
            _playerPanel,
            "Name",
            string.IsNullOrWhiteSpace(save.characterName) ? "Player" : save.characterName
        );
        SetScopedText(_playerPanel, "LevelValue", save.level.ToString());

        // Tier (shared Tier enum)
        SetScopedText(_playerPanel, "Tier", ToTierRoman(save.tier));

        //Class + spec DISPLAY names (fallback to ids if db missing)
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

        SetScopedText(_playerPanel, "Class", className);
        SetScopedText(_playerPanel, "Spec", specName);

        // Player icon comes from PlayerIconDatabase via id in SaveData
        Sprite playerSprite = cfg?.PlayerIconDatabase?.GetIcon(save.playerIconId);

        // Small icon inside Player panel
        SetSpriteBackground(_playerPanel.Q<VisualElement>("Icon"), playerSprite);

        // Big icon on background
        SetSpriteBackground(_playerBigIcon, playerSprite);

        // HP/Mana bars exist in your UXML as MyGame.UI.PixelProgressBar (name="Hp"/"Mana")
        // We’ll wire values after you paste PixelProgressBar API.

        PopulateActiveSpellSlots();
    }

    private void PopulateActiveSpellSlots()
    {
        if (_spellSlots == null || _spellSlots.Length == 0)
            return;

        // Clear all slots first
        for (int i = 0; i < _spellSlots.Length; i++)
        {
            var slotRoot = _spellSlots[i];
            if (slotRoot == null)
                continue;

            slotRoot.userData = null;
            slotRoot.tooltip = string.Empty;
            slotRoot.style.display = DisplayStyle.Flex;

            // IMPORTANT: icon is on the Image child, not the root
            var imageVe = slotRoot.Q<VisualElement>("Image");
            if (imageVe != null)
                imageVe.style.backgroundImage = StyleKeyword.None;

            // Reset cooldown visuals on bind (so they start correct)
            _slotViews?[i]?.ResetCooldownVisuals();
        }

        // Spellbook comes from RunSession
        var spellbook = RunSession.Spellbook;
        if (spellbook == null)
            return;

        var active = spellbook.GetActiveSpellsInOrder(); // order 0..7 skipping empties
        if (active == null || active.Count == 0)
            return;

        var spellDb = GameConfigProvider.Instance?.SpellDatabase;

        int fillCount = Mathf.Min(active.Count, _spellSlots.Length);

        for (int i = 0; i < fillCount; i++)
        {
            var slotRoot = _spellSlots[i];
            if (slotRoot == null)
                continue;

            var entry = active[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.spellId))
                continue;

            slotRoot.userData = entry.spellId;

            SpellDefinition def = spellDb != null ? spellDb.GetById(entry.spellId) : null;

            // Apply icon to Image child
            var imageVe = slotRoot.Q<VisualElement>("Image");
            if (imageVe != null)
            {
                if (def != null && def.icon != null)
                    imageVe.style.backgroundImage = new StyleBackground(def.icon);
                else
                    imageVe.style.backgroundImage = StyleKeyword.None;
            }

            slotRoot.tooltip =
                def != null
                    ? $"{def.displayName} (Lv {entry.level})"
                    : $"{entry.spellId} (Lv {entry.level})";
        }
    }

    private void PopulateEnemyUI()
    {
        if (_enemyPanel == null)
        {
            Debug.LogWarning("[CombatTowerController] Enemy panel not found (name=\"Enemy\").");
            return;
        }

        if (_monsterDef == null || _floorEntry == null)
        {
            SetScopedText(_enemyPanel, "Name", "MISSING ENEMY");
            SetScopedText(_enemyPanel, "LevelValue", "");
            SetScopedText(_enemyPanel, "Tier", "");
            SetScopedText(_enemyPanel, "Tags", "");
            SetSpriteBackground(_enemyPanel.Q<VisualElement>("Icon"), null);
            SetSpriteBackground(_enemyBigIcon, null);
            return;
        }

        // Name + level (floor instance level)
        SetScopedText(_enemyPanel, "Name", _monsterDef.DisplayName);
        SetScopedText(_enemyPanel, "LevelValue", _floorEntry.level.ToString());

        // Tier + tags
        SetScopedText(_enemyPanel, "Tier", ToTierRoman(_monsterDef.Tier));
        SetScopedText(_enemyPanel, "Tags", FormatMonsterTags(_monsterDef.Tags));

        // Small icon inside Enemy panel
        SetSpriteBackground(_enemyPanel.Q<VisualElement>("Icon"), _monsterDef.Icon);

        // Big icon on background
        SetSpriteBackground(_enemyBigIcon, _monsterDef.Icon);
    }

    // -------------------------
    // Helpers
    // -------------------------

    private static void SetScopedText(VisualElement root, string name, string value)
    {
        var label = root.Q<Label>(name);
        if (label != null)
            label.text = value ?? string.Empty;
    }

    private static void SetSpriteBackground(VisualElement ve, Sprite sprite)
    {
        if (ve == null)
            return;

        if (sprite == null)
        {
            ve.style.backgroundImage = StyleKeyword.None;
            return;
        }

        ve.style.backgroundImage = new StyleBackground(sprite);
    }

    private static string ToTierRoman(Tier tier)
    {
        return tier switch
        {
            Tier.Tier1 => "Tier I",
            Tier.Tier2 => "Tier II",
            Tier.Tier3 => "Tier III",
            Tier.Tier4 => "Tier IV",
            Tier.Tier5 => "Tier V",
            Tier.Tier6 => "Tier VI",
            _ => "Tier ?",
        };
    }

    private static string FormatMonsterTags(MonsterTag tags)
    {
        if (tags == MonsterTag.None)
            return string.Empty;

        var values = System.Enum.GetValues(typeof(MonsterTag));
        List<string> result = new();

        foreach (MonsterTag tag in values)
        {
            if (tag == MonsterTag.None)
                continue;

            if ((tags & tag) != 0)
            {
                result.Add(tag.ToString());
            }
        }

        return string.Join(" ", result);
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

        // Create resolver -> engine
        var resolver = new SpellDatabaseCombatResolver(cfg.SpellDatabase);
        _engine = new CombatEngine(resolver);

        // Subscribe to events
        _engine.OnEvent += HandleCombatEvent;

        // Start encounter using REAL data
        _engine.StartEncounter(
            save: SaveSession.Current,
            monsterDef: _monsterDef,
            monsterLevel: _floorEntry.level,
            spellbook: spellbook
        );

        if (_engine != null && _engine.State != null)
        {
            SetBar(_playerHpBar, _engine.State.player.hp, _engine.State.player.derived.maxHp);

            SetBar(_playerManaBar, _engine.State.player.mana, _engine.State.player.derived.maxMana);

            SetBar(_enemyHpBar, _engine.State.enemy.hp, _engine.State.enemy.derived.maxHp);

            SetBar(_enemyManaBar, _engine.State.enemy.mana, _engine.State.enemy.derived.maxMana);
        }
    }

    private void HandleCombatEvent(CombatEvent e)
    {
        switch (e)
        {
            case CombatLogEvent log:
                AppendLog(log.Text);
                break;
            case CombatAdvancedLogEvent log:
                AppendLog(log.Prefix, log.Value, log.Suffix, log.Type);
                break;
            case HpChangedEvent hp:
                if (hp.Actor == CombatActorType.Player)
                    SetBar(_playerHpBar, hp.NewHp, hp.MaxHp);
                else
                    SetBar(_enemyHpBar, hp.NewHp, hp.MaxHp);
                break;

            case ManaChangedEvent mana:
                if (mana.Actor == CombatActorType.Player)
                    SetBar(_playerManaBar, mana.NewMana, mana.MaxMana);
                else
                    SetBar(_enemyManaBar, mana.NewMana, mana.MaxMana);
                break;

            case CombatEndedEvent end:
                bool playerWon = end.Winner == CombatActorType.Player;

                AppendLog(playerWon ? "Victory!" : "Defeat!");

                _playerAction.text = playerWon ? "Winner" : "Defeated";
                _enemyAction.text = playerWon ? "Defeated" : "Winner";

                CommitCombatVitalsToSave();
                CombatRewardResult reward = CombatRewardResult.None();

                if (
                    playerWon
                    && SaveSession.Current != null
                    && _monsterDef != null
                    && _floorEntry != null
                )
                {
                    reward = _rewards.GrantVictoryRewards(
                        SaveSession.Current,
                        _monsterDef,
                        _floorEntry.level
                    );
                    RunSession.Towers.CompleteFloor(_context.towerId, _floorEntry);
                    AppendLog($"Gained {reward.exp} EXP and {reward.gold} gold.");
                }
                SaveSessionRuntimeSave.SaveNowWithRuntime();
                ShowAfterCombatOverlay(playerWon, reward);
                break;

            case TurnMeterChangedEvent tm:
                if (tm.Actor == CombatActorType.Player)
                    SetBar(_playerTurnBar, tm.NewValue, tm.MaxValue);
                else
                    SetBar(_enemyTurnBar, tm.NewValue, tm.MaxValue);
                break;
            case EnemyDecisionRequestedEvent:
                StartEnemyDecisionDelay();
                break;
            case SpellQueuedEvent q:
            {
                string spellName = GameConfigProvider.Instance?.SpellDatabase.GetDisplayName(
                    q.SpellId
                );
                string text = $"{q.CasterName} casting {spellName}…";

                if (q.Actor == CombatActorType.Player)
                    _playerAction.text = text;
                else
                    _enemyAction.text = text;
                break;
            }
            case SpellFiredEvent q:
            {
                if (q.Actor == CombatActorType.Player)
                {
                    _playerAction.text = "Choose action...";
                    RefreshCooldownUI();
                }
                else
                    _enemyAction.text = "Enemy deciding...";
                break;
            }
        }
    }

    private void StartEnemyDecisionDelay()
    {
        if (_enemyDecisionRoutine != null)
            StopCoroutine(_enemyDecisionRoutine);

        _enemyDecisionRoutine = StartCoroutine(EnemyDecisionDelayRoutine());
    }

    private System.Collections.IEnumerator EnemyDecisionDelayRoutine()
    {
        yield return new WaitForSeconds(enemyDecisionDelay);

        if (_engine == null || _engine.State == null || _engine.State.isFinished)
            yield break;

        // Tell engine: enemy decided, queue spell now
        _engine.QueueEnemySpellAfterDecision();

        // Continue simulation until player needs to act again (or fight ends)
        _engine.AdvanceUntilPlayerInputOrEnd();
    }

    private void BindSpellSlotClicks()
    {
        if (_spellSlots == null || _spellSlots.Length == 0)
            return;

        _spellSlotCallbacks = new EventCallback<ClickEvent>[_spellSlots.Length];

        for (int i = 0; i < _spellSlots.Length; i++)
        {
            var slot = _spellSlots[i];
            if (slot == null)
                continue;

            int index = i;

            EventCallback<ClickEvent> cb = _ =>
            {
                if (_engine == null)
                    return;

                string spellId = slot.userData as string;

                if (string.IsNullOrWhiteSpace(spellId))
                {
                    Debug.Log("[Combat] Clicked empty spell slot.");
                    return;
                }

                _engine.TryUseSpell(spellId);
            };

            _spellSlotCallbacks[index] = cb;
            slot.RegisterCallback(cb);
        }
    }

    private void UnbindSpellSlotClicks()
    {
        if (_spellSlots == null || _spellSlotCallbacks == null)
            return;

        for (int i = 0; i < _spellSlots.Length; i++)
        {
            var slot = _spellSlots[i];
            var cb = _spellSlotCallbacks[i];

            if (slot != null && cb != null)
                slot.UnregisterCallback(cb);
        }

        _spellSlotCallbacks = null;
    }

    private void OnRunClicked()
    {
        CommitCombatVitalsToSave();
        // 1) save runtime (spells XP + towers) + also saves currentHp/currentMana now
        SaveSessionRuntimeSave.SaveNowWithRuntime();

        // 2) cleanup combat state + UI bindings
        CleanupCombat();

        // 3) go back to Tower screen
        // Use the screen id you registered for your Tower screen.
        // (I'm assuming it's "tower"; change if yours differs.)
        _swapper.ShowScreen("inside_tower", new InsideTowerContext(_context.towerId));
    }

    private void CleanupCombat()
    {
        // Unsubscribe from engine events
        if (_engine != null)
        {
            _engine.OnEvent -= HandleCombatEvent;
            _engine = null;
        }

        if (_enemyDecisionRoutine != null)
        {
            StopCoroutine(_enemyDecisionRoutine);
            _enemyDecisionRoutine = null;
        }
        // Unregister spell slot callbacks (if we registered them with stored delegates)
        UnbindSpellSlotClicks();

        // Optionally clear log / bars (not required, but nice)
        _logScroll?.Clear();
    }

    private void SetButtonClick(Button btn, Action onClick)
    {
        if (btn == null)
            return;

        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = onClick;
        btn.clicked += onClick;
    }

    private void ClearButtonClick(Button btn)
    {
        if (btn == null)
            return;

        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = null;
    }

    private void CommitCombatVitalsToSave()
    {
        if (_engine == null || _engine.State == null)
            return;

        if (!SaveSession.HasSave || SaveSession.Current == null)
            return;

        var save = SaveSession.Current;
        var state = _engine.State;

        // Write remaining HP/Mana from combat back to save
        save.currentHp = state.player.hp;
        save.currentMana = state.player.mana;

        // Optional safety clamp (recommended)
        int maxHp = state.player.derived.maxHp;
        int maxMana = state.player.derived.maxMana;

        if (save.currentHp < 0)
            save.currentHp = 0;
        if (save.currentMana < 0)
            save.currentMana = 0;

        if (save.currentHp > maxHp)
            save.currentHp = maxHp;
        if (save.currentMana > maxMana)
            save.currentMana = maxMana;
    }

    private sealed class SpellSlotView
    {
        private readonly VisualElement _maskFull;
        private readonly VisualElement _maskResize;
        private readonly Label _cooldownLabel;

        public SpellSlotView(VisualElement slotRoot)
        {
            // Try common names (use whichever your UXML actually has)
            _maskFull = slotRoot.Q<VisualElement>("Mask") ?? slotRoot.Q<VisualElement>("MaskFull");
            _maskResize =
                slotRoot.Q<VisualElement>("MaskToResize")
                ?? slotRoot.Q<VisualElement>("MaskResize");
            _cooldownLabel = slotRoot.Q<Label>("Cooldown") ?? slotRoot.Q<Label>("CooldownLabel");

            // Ensure a clean initial state on bind
            ResetCooldownVisuals();
        }

        public void ResetCooldownVisuals()
        {
            SetDisplay(_maskFull, DisplayStyle.None);
            SetDisplay(_maskResize, DisplayStyle.None);
            SetDisplay(_cooldownLabel, DisplayStyle.None);

            // Also reset width so old percent widths never “stick”
            if (_maskResize != null)
                _maskResize.style.width = Length.Percent(0f);

            if (_cooldownLabel != null)
                _cooldownLabel.text = string.Empty;
        }

        public void SetCooldown(float remaining, float max)
        {
            if (max <= 0f)
                remaining = 0f;

            remaining = Mathf.Max(0f, remaining);
            if (max > 0f)
                remaining = Mathf.Min(remaining, max);

            // 0 => hide all
            if (remaining <= 0f)
            {
                ResetCooldownVisuals();
                return;
            }

            // full => show full mask only
            if (max > 0f && remaining >= max)
            {
                SetDisplay(_maskFull, DisplayStyle.Flex);
                SetDisplay(_maskResize, DisplayStyle.None);
                if (_cooldownLabel != null)
                {
                    _cooldownLabel.text = Mathf.CeilToInt(remaining).ToString();
                    _cooldownLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            // in-between => show resize with ratio
            SetDisplay(_maskFull, DisplayStyle.None);
            SetDisplay(_maskResize, DisplayStyle.Flex);

            float ratio = Mathf.Clamp01(remaining / max);
            if (_maskResize != null)
                _maskResize.style.width = Length.Percent(ratio * 100f);

            if (_cooldownLabel != null)
            {
                _cooldownLabel.text = Mathf.CeilToInt(remaining).ToString();
                _cooldownLabel.style.display = DisplayStyle.Flex;
            }
        }

        private static void SetDisplay(VisualElement ve, DisplayStyle display)
        {
            if (ve == null)
                return;
            ve.style.display = display;
        }
    }

    // CombatTowerController.cs

    private void RefreshCooldownUI()
    {
        if (_engine == null || _engine.State == null)
            return;

        if (_spellSlots == null || _slotViews == null)
            return;

        for (int i = 0; i < _spellSlots.Length; i++)
        {
            var slotRoot = _spellSlots[i];
            var view = _slotViews[i];

            if (slotRoot == null || view == null)
                continue;

            string spellId = slotRoot.userData as string;

            // Empty slot -> hide cooldown visuals
            if (string.IsNullOrWhiteSpace(spellId))
            {
                view.ResetCooldownVisuals();
                continue;
            }

            // Ask engine for correct remaining/max
            if (_engine.TryGetPlayerSpellCooldown(spellId, out int remaining, out int max))
            {
                view.SetCooldown(remaining, max);
            }
            else
            {
                // If anything failed (unknown spell, missing entry, etc.)
                // don't show cooldown overlay.
                view.ResetCooldownVisuals();
            }
        }
    }

    private void ShowAfterCombatOverlay(bool playerWon, CombatRewardResult rewards)
    {
        var ctx = new AfterCombatOverlayContext
        {
            playerWon = playerWon,
            rewards = rewards,

            OnRestartCombat = () =>
            {
                CleanupCombat();
                // reload same combat screen / same floor
                _swapper.ShowScreen("combat_tower", _context);
            },

            OnReturnToTower = () =>
            {
                CleanupCombat();
                Debug.Log("returnTo tower");
                _swapper.ShowScreen("inside_tower", new InsideTowerContext(_context.towerId));
            },

            OnNextFloor = () =>
            {
                CleanupCombat();
                _swapper.ShowScreen(
                    "combat_tower",
                    new CombatTowerContext(_context.towerId, _context.floor + 1)
                );
            },
        };

        _swapper.ShowOverlay("after_combat_overlay", ctx);
    }
}
