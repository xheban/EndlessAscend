using MyGame.Rewards;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AfterCombatOverlayController : MonoBehaviour, IOverlayController
{
    private ScreenSwapper _swapper;
    private AfterCombatOverlayContext _ctx;

    // UXML references
    private Label _combatText;
    private Label _title;

    private VisualElement _expRoot;
    private Label _expText;

    private VisualElement _goldRoot;
    private Label _goldText;

    private VisualElement _lootTable;

    private Button _btnNewCombat;
    private Button _btnReturn;
    private Button _btnNextFloor;

    public void Bind(VisualElement overlayHost, ScreenSwapper swapper, object context = null)
    {
        _swapper = swapper;
        _ctx = context as AfterCombatOverlayContext;

        // Labels
        _combatText = overlayHost.Q<Label>("CombatText");
        _title = overlayHost.Q<Label>("Title");

        // Scoped queries (because both Exp and Currency have Label name="Text")
        _expRoot = overlayHost.Q<VisualElement>("Exp");
        _goldRoot = overlayHost.Q<VisualElement>("Currency");

        _expText = _expRoot?.Q<Label>("Text");
        _goldText = _goldRoot?.Q<Label>("Text");

        // Loot table (contains LootSlot1..LootSlot16)
        _lootTable = overlayHost.Q<VisualElement>("LootTable");

        // Buttons
        _btnNewCombat = overlayHost.Q<Button>("NewCombat");
        _btnReturn = overlayHost.Q<Button>("Return");
        _btnNextFloor = overlayHost.Q<Button>("NextFloor");

        if (_btnNewCombat != null)
            _btnNewCombat.clicked += OnNewCombat;
        if (_btnReturn != null)
            _btnReturn.clicked += OnReturn;
        if (_btnNextFloor != null)
            _btnNextFloor.clicked += OnNextFloor;

        BuildUI();
    }

    public void Unbind()
    {
        if (_btnNewCombat != null)
            _btnNewCombat.clicked -= OnNewCombat;
        if (_btnReturn != null)
            _btnReturn.clicked -= OnReturn;
        if (_btnNextFloor != null)
            _btnNextFloor.clicked -= OnNextFloor;

        _ctx = null;
        _swapper = null;
    }

    private void BuildUI()
    {
        if (_ctx == null)
            return;

        if (_combatText != null)
            _combatText.text = _ctx.playerWon ? "You Won the Battle!" : "You Lost the Battle!";

        if (_title != null)
            _title.text = _ctx.playerWon ? "Victory" : "Defeat";

        // Rewards are always present (struct). On loss they should be CombatRewardResult.None()
        CombatRewardResult r = _ctx.rewards;

        if (_expText != null)
            _expText.text = r.exp.ToString();

        if (_goldText != null)
            _goldText.text = r.gold.ToString();

        PopulateLoot(r.loot);

        // Optional UX: no next floor on defeat
        _btnNextFloor?.SetEnabled(_ctx.playerWon);
    }

    private void PopulateLoot(CombatRewardResult.LootItem[] loot)
    {
        if (_lootTable == null)
            return;

        const int maxSlots = 16;

        // Hide all first, but keep layout stable (no shifting)
        for (int i = 1; i <= maxSlots; i++)
        {
            var slot = _lootTable.Q<VisualElement>($"LootSlot{i}");
            if (slot == null)
                continue;

            slot.style.visibility = Visibility.Hidden;
            slot.tooltip = string.Empty;

            // Later: clear icon background, count label, etc.
            // slot.style.backgroundImage = StyleKeyword.None;
        }

        if (loot == null || loot.Length == 0)
            return;

        int count = Mathf.Min(loot.Length, maxSlots);

        for (int i = 0; i < count; i++)
        {
            var slot = _lootTable.Q<VisualElement>($"LootSlot{i + 1}");
            if (slot == null)
                continue;

            slot.style.visibility = Visibility.Visible;

            var item = loot[i];
            if (item != null)
            {
                slot.tooltip = $"{item.lootId} x{item.stackCount}";
            }

            // Later: set icon background using a LootDatabase:
            // var icon = lootDb.GetIcon(item.lootId);
            // slot.style.backgroundImage = new StyleBackground(icon);
        }
    }

    private void OnNewCombat()
    {
        var action = _ctx?.OnRestartCombat;
        _swapper.CloseOverlay("after_combat_overlay");
        action?.Invoke();
    }

    private void OnReturn()
    {
        var action = _ctx?.OnReturnToTower;
        _swapper.CloseOverlay("after_combat_overlay");
        action?.Invoke();
    }

    private void OnNextFloor()
    {
        var action = _ctx?.OnNextFloor;
        _swapper.CloseOverlay("after_combat_overlay");
        action?.Invoke();
    }
}
