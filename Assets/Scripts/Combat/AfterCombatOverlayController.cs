using System;
using MyGame.Combat;
using MyGame.Common;
using MyGame.Inventory;
using MyGame.Rewards;
using MyGame.Run;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AfterCombatOverlayController : MonoBehaviour, IOverlayController
{
    private ScreenSwapper _swapper;
    private AfterCombatOverlayContext _ctx;
    private VisualElement _overlayRoot;

    private sealed class LootSlotUserData
    {
        public bool registered;
        public CombatRewardResult.LootItem item;
    }

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
        _overlayRoot = overlayHost;

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
        _overlayRoot = null;
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

            EnsureLootSlotVisuals(slot);

            var icon = slot.Q<VisualElement>("Icon");
            if (icon != null)
                icon.style.backgroundImage = StyleKeyword.None;

            var countLabel = slot.Q<Label>("Count");
            if (countLabel != null)
            {
                countLabel.text = string.Empty;
                countLabel.style.display = DisplayStyle.None;
            }

            // Tooltip uses ScreenSwapper overlay system.
            var ud = slot.userData as LootSlotUserData;
            if (ud == null)
            {
                ud = new LootSlotUserData();
                slot.userData = ud;
            }
            ud.item = null;

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

            EnsureLootSlotVisuals(slot);
            RegisterLootTooltipIfNeeded(slot);

            var item = loot[i];

            var ud = slot.userData as LootSlotUserData;
            if (ud == null)
            {
                ud = new LootSlotUserData();
                slot.userData = ud;
            }
            ud.item = item;

            ApplyLootSlotVisuals(slot, item);

            // Later: set icon background using a LootDatabase:
            // var icon = lootDb.GetIcon(item.lootId);
            // slot.style.backgroundImage = new StyleBackground(icon);
        }
    }

    private void EnsureLootSlotVisuals(VisualElement slot)
    {
        if (slot == null)
            return;

        // Make this a positioning container for the count label.
        slot.style.position = Position.Relative;
        slot.pickingMode = PickingMode.Position;

        var icon = slot.Q<VisualElement>("Icon");
        if (icon == null)
        {
            icon = new VisualElement { name = "Icon" };
            icon.pickingMode = PickingMode.Ignore;
            icon.style.position = Position.Absolute;
            icon.style.left = 8;
            icon.style.top = 8;
            icon.style.right = 8;
            icon.style.bottom = 8;
            icon.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            icon.style.backgroundPositionX = new BackgroundPosition(
                BackgroundPositionKeyword.Center
            );
            icon.style.backgroundPositionY = new BackgroundPosition(
                BackgroundPositionKeyword.Center
            );
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            slot.Add(icon);
        }

        var count = slot.Q<Label>("Count");
        if (count == null)
        {
            count = new Label { name = "Count" };
            count.pickingMode = PickingMode.Ignore;
            count.style.position = Position.Absolute;
            count.style.right = 4;
            count.style.bottom = 4;
            count.style.unityTextAlign = TextAnchor.LowerRight;
            count.style.fontSize = 18;
            count.style.color = Color.white;
            count.style.unityFontStyleAndWeight = FontStyle.Bold;
            count.style.display = DisplayStyle.None;
            slot.Add(count);
        }
    }

    private void ApplyLootSlotVisuals(VisualElement slot, CombatRewardResult.LootItem item)
    {
        if (slot == null)
            return;

        var iconEl = slot.Q<VisualElement>("Icon");
        var countEl = slot.Q<Label>("Count");

        if (item == null || string.IsNullOrWhiteSpace(item.lootId))
        {
            if (iconEl != null)
                iconEl.style.backgroundImage = StyleKeyword.None;
            if (countEl != null)
            {
                countEl.text = string.Empty;
                countEl.style.display = DisplayStyle.None;
            }
            return;
        }

        var cfg = GameConfigProvider.Instance;
        var itemDb = cfg != null ? cfg.ItemDatabase : null;
        var equipDb = cfg != null ? cfg.EquipmentDatabase : null;

        Sprite sprite = null;
        if (item.kind == LootDropKind.Item)
            sprite = itemDb != null ? itemDb.GetIcon(item.lootId) : null;
        else if (item.kind == LootDropKind.Equipment)
            sprite = equipDb != null ? equipDb.GetIcon(item.lootId) : null;

        if (iconEl != null)
            iconEl.style.backgroundImage =
                sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;

        if (countEl != null)
        {
            int stack = Mathf.Max(1, item.stackCount);
            bool show = item.kind == LootDropKind.Item && stack > 1;
            countEl.text = show ? stack.ToString() : string.Empty;
            countEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void RegisterLootTooltipIfNeeded(VisualElement slot)
    {
        if (slot == null || _swapper == null)
            return;

        if (slot.userData is not LootSlotUserData ud)
        {
            ud = new LootSlotUserData();
            slot.userData = ud;
        }

        if (ud.registered)
            return;
        ud.registered = true;

        slot.RegisterCallback<PointerEnterEvent>(_ => ShowLootTooltip(slot));
        slot.RegisterCallback<PointerLeaveEvent>(_ => HideLootTooltip());
        slot.RegisterCallback<PointerOutEvent>(_ => HideLootTooltip());
    }

    private void ShowLootTooltip(VisualElement slot)
    {
        if (_swapper == null || slot == null)
            return;
        if (slot.userData is not LootSlotUserData ud)
            return;
        var item = ud.item;
        if (item == null || string.IsNullOrWhiteSpace(item.lootId))
            return;

        // Prefer the inventory-style tooltip panel.
        var tooltip = _swapper.GetCustomTooltipElement("InventoryDetailTooltip");
        if (tooltip == null)
            tooltip = _overlayRoot?.Q<VisualElement>("InventoryDetailTooltip");
        if (tooltip == null)
        {
            // Fallback to the simple text tooltip.
            _swapper.ShowTooltipAtElement(slot, item.lootId);
            return;
        }

        PopulateInventoryDetailTooltip(tooltip, item);
        _swapper.ShowCustomTooltipAboveWorldPosition(tooltip, slot.worldBound.center, offsetPx: 8f);
    }

    private void HideLootTooltip()
    {
        if (_swapper == null)
            return;

        var tooltip = _swapper.GetCustomTooltipElement("InventoryDetailTooltip");
        if (tooltip != null)
            _swapper.HideCustomTooltip(tooltip);

        _swapper.HideTooltip();
    }

    private void PopulateInventoryDetailTooltip(
        VisualElement tooltip,
        CombatRewardResult.LootItem item
    )
    {
        if (tooltip == null || item == null || string.IsNullOrWhiteSpace(item.lootId))
            return;

        if (item.kind == LootDropKind.Item)
        {
            // Match Inventory tooltip behavior.
            if (
                !MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForItemId(
                    tooltip,
                    item.lootId
                )
            )
            {
                var nameEl = tooltip.Q<Label>("Name");
                if (nameEl != null)
                    nameEl.text = item.lootId;
            }
            return;
        }

        if (item.kind == LootDropKind.Equipment)
        {
            // Prefer the actual granted/rolled instance (so rolled stats show).
            PlayerEquipment.EquipmentInstance inst = null;
            if (!string.IsNullOrWhiteSpace(item.equipmentInstanceId) && RunSession.IsInitialized)
                inst = RunSession.Equipment?.GetInstance(item.equipmentInstanceId);

            if (inst != null)
            {
                MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentInstance(
                    tooltip,
                    inst
                );
                return;
            }

            // Fallback: show definition-only tooltip (no rolled stats).
            MyGame.Helpers.InventoryDetailTooltipBuilder.TryPopulateForEquipmentId(
                tooltip,
                item.lootId
            );
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
