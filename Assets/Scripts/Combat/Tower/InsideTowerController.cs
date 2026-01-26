using System;
using MyGame.Run;
using UnityEngine;
using UnityEngine.UIElements;

public class InsideTowerController : MonoBehaviour, IScreenController
{
    private const int VisibleCount = 5;
    private const int ShiftAmount = 5;

    [SerializeField]
    private VisualTreeAsset floorInfoCardTemplate;

    [SerializeField]
    private int maxDescendVisibility = 5;

    [SerializeField]
    private TowerDatabase towerDatabase;
    private TowerDefinition _towerDef;

    private InsideTowerContext _context;
    private ScreenSwapper _swapper;

    private Button _backButton;
    private VisualElement _ascend;
    private VisualElement _descend;

    private IntegerField _floorNumberField;
    private Button _goButton;

    private VisualElement[] _slots;
    private VisualElement[] _cards;

    private sealed class TooltipUserData
    {
        public bool registered;
        public string text;
    }

    // The first floor shown in the 5-card window (1-based)
    private int _windowStartFloor = 1;

    private struct FloorData
    {
        public int floor;
        public string monsterName;

        // Stats (raw numbers)
        public int str;
        public int agi;
        public int intel;
        public int end;
        public int spr;

        // Rewards (raw numbers)
        public int exp;
        public int goldMin;
        public int goldMax;

        // Icons
        public Sprite monsterIcon;

        // Drops
        public IconPreview[] skills; // 0..6
        public IconPreview[] loot; // 0..8
    }

    private readonly struct IconPreview
    {
        public readonly Sprite icon;
        public readonly string tooltip;

        public IconPreview(Sprite icon, string tooltip)
        {
            this.icon = icon;
            this.tooltip = tooltip;
        }
    }

    private sealed class CardClickData
    {
        public int floor;
        public bool locked;
    }

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _swapper = swapper;
        _context = context as InsideTowerContext;

        if (_context == null)
        {
            Debug.LogError("InsideTower opened without context.");
            return;
        }

        _towerDef = towerDatabase.GetById(_context.towerId);

        if (_towerDef == null)
        {
            Debug.LogError($"No tower definition found for '{_context.towerId}'.");
        }

        // --- Back button ---
        _backButton = screenHost.Q<Button>("back");
        _backButton.clicked += OnBackClicked;

        // --- Slots ---
        _slots = new[]
        {
            screenHost.Q<VisualElement>("FloorSlot1"),
            screenHost.Q<VisualElement>("FloorSlot2"),
            screenHost.Q<VisualElement>("FloorSlot3"),
            screenHost.Q<VisualElement>("FloorSlot4"),
            screenHost.Q<VisualElement>("FloorSlot5"),
        };

        // --- Instantiate 5 cards once, keep references ---
        _cards = new VisualElement[VisibleCount];
        for (int i = 0; i < VisibleCount; i++)
        {
            _slots[i].Clear();
            _cards[i] = floorInfoCardTemplate.Instantiate();
            _slots[i].Add(_cards[i]);

            MakeCardClickable(_cards[i]);
        }

        // --- Move controls ---
        _ascend = screenHost.Q<VisualElement>("Ascend");
        _descend = screenHost.Q<VisualElement>("Descend");

        _floorNumberField = screenHost.Q<IntegerField>("FloorNumber");
        _goButton = screenHost.Q<Button>("Go");

        // Make VisualElements clickable like buttons
        MakeClickable(_ascend, OnAscendClicked);
        MakeClickable(_descend, OnDescendClicked);

        _goButton.clicked += OnGoClicked;

        // Initial view
        // If you have a "current floor" in context, use it here:
        // CenterOnFloor(_context.CurrentFloor);
        CenterOnFloor(GetCurrentTowerFloor());
        UpdateArrowInteractivity();
    }

    public void Unbind()
    {
        if (_backButton != null)
            _backButton.clicked -= OnBackClicked;
        if (_goButton != null)
            _goButton.clicked -= OnGoClicked;

        // Clickable manipulators don’t need explicit unsubscribe (we attach a manipulator instance),
        // but you can Clear() if you want to be strict.

        _context = null;
        _swapper = null;
        _backButton = null;
        _ascend = null;
        _descend = null;
        _floorNumberField = null;
        _goButton = null;
        _slots = null;
        _cards = null;
    }

    private void OnBackClicked()
    {
        _swapper.ShowScreen("tower");
    }

    private static string FormatNumber(int value)
    {
        if (value >= 1_000_000)
            return (value / 1_000_000f).ToString("0.00") + "M";

        if (value >= 1_000)
        {
            float k = value / 1_000f;
            if (k >= 1000f)
                return (k / 1000f).ToString("0.00") + "M";

            return k.ToString("0.00") + "K";
        }

        return value.ToString();
    }

    private void OnDescendClicked()
    {
        int maxStart = GetMaxWindowStartAllowed();

        // clamp shift so we move as far as possible (maybe less than 5)
        int newStart = Mathf.Min(_windowStartFloor + ShiftAmount, maxStart);

        if (newStart == _windowStartFloor)
        {
            UpdateArrowInteractivity();
            return;
        }

        _windowStartFloor = newStart;
        RefreshVisibleCards();
        UpdateMiddleFloorField();
        UpdateArrowInteractivity();
    }

    private void OnAscendClicked()
    {
        int newStart = Mathf.Max(1, _windowStartFloor - ShiftAmount);

        if (newStart == _windowStartFloor)
        {
            UpdateArrowInteractivity();
            return;
        }

        _windowStartFloor = newStart;
        RefreshVisibleCards();
        UpdateMiddleFloorField();
        UpdateArrowInteractivity();
    }

    private void OnGoClicked()
    {
        int requested = _floorNumberField.value;

        int currentFloor = GetCurrentFloor();
        int maxReachable = Mathf.Min(GetMaxFloor(), currentFloor + maxDescendVisibility);

        int clamped = Mathf.Clamp(requested, 1, maxReachable);

        // Optional: reflect the clamp back into the input field so player sees it
        _floorNumberField.SetValueWithoutNotify(clamped);

        CenterOnFloor(clamped);
    }

    private void ShiftWindow(int delta)
    {
        int newStart = _windowStartFloor + delta;
        newStart = ClampWindowStart(newStart);

        _windowStartFloor = newStart;
        RefreshVisibleCards();
        UpdateMiddleFloorField();
        UpdateArrowInteractivity(); // ✅ add
    }

    private void CenterOnFloor(int targetFloor)
    {
        int newStart = targetFloor - 2;
        newStart = ClampWindowStart(newStart);

        _windowStartFloor = newStart;
        RefreshVisibleCards();
        UpdateMiddleFloorField();
        UpdateArrowInteractivity(); // ✅ add
    }

    private int ClampWindowStart(int start)
    {
        int maxFloor = GetMaxFloor();
        int maxStart = Mathf.Max(1, maxFloor - (VisibleCount - 1));

        if (start < 1)
            start = 1;
        if (start > maxStart)
            start = maxStart;

        return start;
    }

    private int GetCurrentTowerFloor()
    {
        if (!RunSession.IsInitialized)
            return 1;

        // If missing for any reason, default to 1
        return RunSession.Towers.GetFloor(_context.towerId);
    }

    private int GetMaxFloor()
    {
        return _towerDef != null ? _towerDef.MaxFloor : 1;
    }

    private void RefreshVisibleCards()
    {
        int currentFloor = GetCurrentFloor();
        int maxInfoFloor = currentFloor;

        for (int i = 0; i < VisibleCount; i++)
        {
            int floorNumber = _windowStartFloor + i;
            bool locked = floorNumber > maxInfoFloor;

            // Use FloorTile for consistent click/hover behavior
            var tile = _cards[i].Q<VisualElement>("FloorTile") ?? _cards[i];

            // Store click payload where click handler reads it
            tile.userData = new CardClickData { floor = floorNumber, locked = locked };

            // ✅ Only unlocked cards are clickable + hoverable
            SetCardInteractable(_cards[i], interactable: !locked);

            SetCardLocked(_cards[i], locked);

            if (locked)
                PopulateLockedCard(_cards[i], floorNumber);
            else
                PopulateCard(_cards[i], GetFloorData(floorNumber));
        }
    }

    private int GetCurrentFloor()
    {
        if (_context == null)
            return 1;

        if (!RunSession.IsInitialized)
            return 1;

        return RunSession.Towers.GetFloor(_context.towerId);
    }

    private void PopulateLockedCard(VisualElement cardRoot, int floorNumber)
    {
        var root = cardRoot.Q<VisualElement>("FloorTile") ?? cardRoot;

        var floorLabel = root.Q<Label>("Floor");
        if (floorLabel != null)
            floorLabel.text = $"FLOOR {floorNumber}";
    }

    private void UpdateMiddleFloorField()
    {
        // Middle of 5 cards is index 2
        int middleFloor = _windowStartFloor + 2;

        // Avoid triggering any potential change callbacks (if you add them later)
        _floorNumberField.SetValueWithoutNotify(middleFloor);
    }

    // -------------------------
    // Helpers
    // -------------------------

    private void MakeClickable(VisualElement ve, System.Action onClick)
    {
        if (ve == null)
            return;

        ve.focusable = true;
        ve.pickingMode = PickingMode.Position;

        // Clickable gives you proper "button-like" clicking in UI Toolkit
        ve.AddManipulator(new Clickable(() => onClick?.Invoke()));
    }

    // -------------------------
    // Data + Populate (replace later)
    // -------------------------

    private FloorData GetFloorData(int floorNumber)
    {
        // Safe fallback so UI never explodes
        if (_towerDef == null)
        {
            return new FloorData
            {
                floor = floorNumber,
                monsterName = "???",
                str = 0,
                agi = 0,
                intel = 0,
                end = 0,
                spr = 0,
                exp = 0,
                goldMin = 0,
                goldMax = 0,
                monsterIcon = null,
                skills = Array.Empty<IconPreview>(),
                loot = Array.Empty<IconPreview>(),
            };
        }

        var entry = _towerDef.GetFloor(floorNumber);
        if (entry == null || entry.monster == null)
        {
            return new FloorData
            {
                floor = floorNumber,
                monsterName = "MISSING DATA",
                str = 0,
                agi = 0,
                intel = 0,
                end = 0,
                spr = 0,
                exp = 0,
                goldMin = 0,
                goldMax = 0,
                monsterIcon = null,
                skills = Array.Empty<IconPreview>(),
                loot = Array.Empty<IconPreview>(),
            };
        }

        var m = entry.monster;
        var s = m.BaseStats;

        // exp/gold: if you set overrides on the floor, use them; otherwise monster base
        int exp = entry.expOverride > 0 ? entry.expOverride : m.BaseExp;
        int goldMin = entry.goldOverride > 0 ? entry.goldOverride : m.GoldMin;
        int goldMax = entry.goldOverride > 0 ? entry.goldOverride : m.GoldMax;

        IconPreview[] skillsPreview = BuildSkillPreview(m);
        IconPreview[] lootPreview = BuildLootPreview(m);

        return new FloorData
        {
            floor = floorNumber,
            monsterName = m.DisplayName,

            // If your MonsterStats fields are lowercase, change these accordingly:
            str = s.strength,
            agi = s.agility,
            intel = s.intelligence,
            end = s.endurance,
            spr = s.spirit,

            exp = exp,
            goldMin = goldMin,
            goldMax = goldMax,

            monsterIcon = m.Icon,

            skills = skillsPreview,
            loot = lootPreview,
        };
    }

    private void PopulateCard(VisualElement cardRoot, FloorData data)
    {
        var root = cardRoot.Q<VisualElement>("FloorTile") ?? cardRoot;

        root.Q<Label>("Floor").text = $"FLOOR {data.floor}";
        root.Q<Label>("MonsterName").text = data.monsterName;

        root.Q<Label>("StrValue").text = FormatNumber(data.str);
        root.Q<Label>("AgiValue").text = FormatNumber(data.agi);
        root.Q<Label>("IntValue").text = FormatNumber(data.intel);
        root.Q<Label>("EndValue").text = FormatNumber(data.end);
        root.Q<Label>("SprValue").text = FormatNumber(data.spr);

        PopulateMonsterIcon(root, data.monsterIcon);
        PopulateExpGold(root, data.exp, data.goldMin, data.goldMax);
        PopulateSkills(root, data.skills);
        PopulateLoot(root, data.loot);
    }

    private void PopulateExpGold(VisualElement root, int exp, int goldMin, int goldMax)
    {
        var expLabel = root.Q<Label>("ExpValue");
        if (expLabel != null)
            expLabel.text = FormatNumber(exp);

        var goldLabel = root.Q<Label>("GoldValue");
        if (goldLabel != null)
            goldLabel.text = $"{FormatNumber(goldMin)} - {FormatNumber(goldMax)}";
    }

    private void PopulateMonsterIcon(VisualElement root, Sprite iconSprite)
    {
        var icon = root.Q<VisualElement>("MonsterIcon");
        if (icon != null && iconSprite != null)
            icon.style.backgroundImage = new StyleBackground(iconSprite);
    }

    private void PopulateSkills(VisualElement root, IconPreview[] skills)
    {
        skills ??= Array.Empty<IconPreview>();

        for (int i = 1; i <= 6; i++)
        {
            var slot = root.Q<VisualElement>($"Skill{i}");
            if (slot == null)
                continue;

            int idx = i - 1;
            bool has = idx < skills.Length && skills[idx].icon != null;
            slot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

            string tooltipText = has ? (skills[idx].tooltip ?? string.Empty) : string.Empty;
            SetTooltip(slot, tooltipText);

            var icon = slot.Q<VisualElement>("Icon");
            if (icon != null)
            {
                icon.style.backgroundImage = has
                    ? new StyleBackground(skills[idx].icon)
                    : StyleKeyword.None;

                // Make the tooltip work even if the pointer is over the child element.
                SetTooltip(icon, tooltipText);
            }
        }
    }

    private void PopulateLoot(VisualElement root, IconPreview[] loot)
    {
        loot ??= Array.Empty<IconPreview>();

        for (int i = 1; i <= 8; i++)
        {
            var slot = root.Q<VisualElement>($"Loot{i}");
            if (slot == null)
                continue;

            int idx = i - 1;
            bool has = idx < loot.Length && loot[idx].icon != null;
            slot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

            string tooltipText = has ? (loot[idx].tooltip ?? string.Empty) : string.Empty;
            SetTooltip(slot, tooltipText);

            var icon = slot.Q<VisualElement>("Icon");
            if (icon != null)
            {
                icon.style.backgroundImage = has
                    ? new StyleBackground(loot[idx].icon)
                    : StyleKeyword.None;

                SetTooltip(icon, tooltipText);
            }
        }
    }

    private void SetTooltip(VisualElement element, string text)
    {
        if (element == null)
            return;

        // This project uses ScreenSwapper's tooltip overlay (not VisualElement.tooltip).
        if (_swapper == null)
            return;

        element.pickingMode = PickingMode.Position;

        if (element.userData is not TooltipUserData data)
        {
            data = new TooltipUserData();
            element.userData = data;
        }

        data.text = text ?? string.Empty;

        if (data.registered)
            return;

        data.registered = true;

        element.RegisterCallback<PointerEnterEvent>(evt =>
        {
            if (evt.currentTarget is not VisualElement ve)
                return;
            if (ve.userData is not TooltipUserData d)
                return;
            if (string.IsNullOrWhiteSpace(d.text))
                return;

            _swapper.ShowTooltipAtElement(ve, d.text);
        });

        element.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            _swapper.HideTooltip();
        });

        element.RegisterCallback<PointerOutEvent>(_ =>
        {
            _swapper.HideTooltip();
        });
    }

    private static IconPreview[] BuildSkillPreview(MonsterDefinition monster)
    {
        if (monster == null)
            return Array.Empty<IconPreview>();

        var spellDb = GameConfigProvider.Instance?.SpellDatabase;
        if (spellDb == null)
            return Array.Empty<IconPreview>();

        var spells = monster.Spells;
        if (spells == null || spells.Count == 0)
            return Array.Empty<IconPreview>();

        var icons = new System.Collections.Generic.List<IconPreview>(6);
        var seen = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < spells.Count && icons.Count < 6; i++)
        {
            var e = spells[i];
            if (e == null || string.IsNullOrWhiteSpace(e.SpellId))
                continue;
            if (!seen.Add(e.SpellId))
                continue;

            var icon = spellDb.GetIcon(e.SpellId);
            if (icon != null)
            {
                string name = spellDb.GetDisplayName(e.SpellId);
                icons.Add(new IconPreview(icon, name));
            }
        }

        return icons.ToArray();
    }

    private static IconPreview[] BuildLootPreview(MonsterDefinition monster)
    {
        if (monster == null)
            return Array.Empty<IconPreview>();

        var table = monster.BaseLoot;
        if (table == null)
            return Array.Empty<IconPreview>();

        var cfg = GameConfigProvider.Instance;
        var itemDb = cfg != null ? cfg.ItemDatabase : null;
        var equipDb = cfg != null ? cfg.EquipmentDatabase : null;

        var icons = new System.Collections.Generic.List<IconPreview>(8);
        var seen = new System.Collections.Generic.HashSet<(LootDropKind kind, string id)>();

        bool TryAdd(LootDropDefinition def)
        {
            if (def == null)
                return false;
            if (icons.Count >= 8)
                return false;

            string id;
            Sprite icon;
            string name;

            if (def.kind == LootDropKind.Item)
            {
                id = def.itemId;
                if (string.IsNullOrWhiteSpace(id))
                    return false;
                icon = itemDb != null ? itemDb.GetIcon(id) : null;
                name = itemDb != null ? itemDb.GetDisplayName(id) : id;
            }
            else if (def.kind == LootDropKind.Equipment)
            {
                id = def.equipmentId;
                if (string.IsNullOrWhiteSpace(id))
                    return false;
                icon = equipDb != null ? equipDb.GetIcon(id) : null;
                name = equipDb != null ? equipDb.GetDisplayName(id) : id;
            }
            else
            {
                return false;
            }

            if (icon == null)
                return false;

            if (!seen.Add((def.kind, id)))
                return false;

            icons.Add(new IconPreview(icon, name));
            return true;
        }

        var guaranteed = table.GuaranteedDrops;
        if (guaranteed != null)
        {
            for (int i = 0; i < guaranteed.Count && icons.Count < 8; i++)
                TryAdd(guaranteed[i]?.drop);
        }

        var pool = table.WeightedPool;
        if (pool != null)
        {
            for (int i = 0; i < pool.Count && icons.Count < 8; i++)
                TryAdd(pool[i]?.drop);
        }

        return icons.ToArray();
    }

    private void SetCardInteractable(VisualElement cardRoot, bool interactable)
    {
        // Use FloorTile if it exists so hover styling targets the visible tile
        var tile = cardRoot.Q<VisualElement>("FloorTile") ?? cardRoot;

        // Pointer events ON/OFF (this controls hover + click)
        tile.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;

        // Optional: allow keyboard focus only when interactable
        tile.focusable = interactable;

        // Toggle a class for hover styling
        tile.EnableInClassList("floor-card--clickable", interactable);

        // Optional: cursor hint (Unity 6 supports this)
        // tile.style.cursor = interactable ? new StyleCursor(MouseCursor.Link) : StyleKeyword.None;
    }

    private void UpdateArrowInteractivity()
    {
        int maxStart = GetMaxWindowStartAllowed();

        bool canGoUp = _windowStartFloor > 1;
        bool canGoDown = _windowStartFloor < maxStart;

        _ascend?.SetEnabled(canGoUp);
        _descend?.SetEnabled(canGoDown);

        // if you want to fully prevent hover/click
        if (_ascend != null)
            _ascend.pickingMode = canGoUp ? PickingMode.Position : PickingMode.Ignore;
        if (_descend != null)
            _descend.pickingMode = canGoDown ? PickingMode.Position : PickingMode.Ignore;
    }

    private int GetMaxWindowStartAllowed()
    {
        int currentFloor = GetCurrentFloor();
        int maxReachable = Mathf.Min(GetMaxFloor(), currentFloor + maxDescendVisibility);

        // VisibleCount = 5, so last visible = start + 4
        int maxStart = maxReachable - (VisibleCount - 1);
        return Mathf.Max(1, maxStart);
    }

    private void MakeCardClickable(VisualElement cardRoot)
    {
        if (cardRoot == null)
            return;

        var tile = cardRoot.Q<VisualElement>("FloorTile") ?? cardRoot;

        tile.focusable = true;
        tile.pickingMode = PickingMode.Position;

        tile.AddManipulator(new Clickable(() => OnCardClicked(tile)));
    }

    private void OnCardClicked(VisualElement tile)
    {
        if (tile == null || _context == null || _swapper == null)
            return;

        if (tile.userData is not CardClickData cd)
            return;

        if (cd.locked)
            return;

        _swapper.ShowScreen("combat_tower", new CombatTowerContext(_context.towerId, cd.floor));
    }

    private void SetCardLocked(VisualElement cardRoot, bool locked)
    {
        // Card root may be the template wrapper or FloorTile itself
        var root = cardRoot.Q<VisualElement>("FloorTile") ?? cardRoot;

        var monsterName = root.Q<VisualElement>("MonsterName");
        var decorativeLine = root.Q<VisualElement>("DecorativeLine");
        var monsterInfo = root.Q<VisualElement>("MonsterInfo");
        var lockVe = root.Q<VisualElement>("Lock");

        if (locked)
        {
            if (monsterName != null)
                monsterName.style.display = DisplayStyle.None;
            if (decorativeLine != null)
                decorativeLine.style.display = DisplayStyle.None;
            if (monsterInfo != null)
                monsterInfo.style.display = DisplayStyle.None;
            if (lockVe != null)
                lockVe.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (monsterName != null)
                monsterName.style.display = DisplayStyle.Flex;
            if (decorativeLine != null)
                decorativeLine.style.display = DisplayStyle.Flex;
            if (monsterInfo != null)
                monsterInfo.style.display = DisplayStyle.Flex;
            if (lockVe != null)
                lockVe.style.display = DisplayStyle.None;
        }
    }
}
