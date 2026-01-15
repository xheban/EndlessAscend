using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class CombatTowerView : ICombatLogSink, ICombatUiSink
{
    // Panels
    public VisualElement PlayerPanel { get; private set; }
    public VisualElement EnemyPanel { get; private set; }

    // Action labels + big icons
    public Label PlayerAction { get; private set; }
    public Label EnemyAction { get; private set; }
    public VisualElement PlayerBigIcon { get; private set; }
    public VisualElement EnemyBigIcon { get; private set; }

    // Skill bar + slots
    public VisualElement SkillBar { get; private set; }
    public VisualElement[] SpellSlots { get; private set; }
    public SpellSlotView[] SlotViews { get; private set; }

    // Bars
    public PixelProgressBar PlayerHpBar { get; private set; }
    public PixelProgressBar PlayerManaBar { get; private set; }
    public PixelProgressBar EnemyHpBar { get; private set; }
    public PixelProgressBar EnemyManaBar { get; private set; }

    public PixelProgressBar PlayerTurnBar { get; private set; }
    public PixelProgressBar EnemyTurnBar { get; private set; }

    // Log
    public VisualElement LogRoot { get; private set; }
    public ScrollView LogScroll { get; private set; }

    public VisualElement[] PlayerBuffSlots { get; private set; }
    public VisualElement[] PlayerDebuffSlots { get; private set; }

    public VisualElement[] EnemyBuffSlots { get; private set; }
    public VisualElement[] EnemyDebuffSlots { get; private set; }

    // Buttons
    public Button RunButton { get; private set; }

    public void Bind(VisualElement screenHost)
    {
        if (screenHost == null)
        {
            Debug.LogError("[CombatTowerView] screenHost is null.");
            return;
        }

        // Cache UI roots (important: duplicate names exist on both sides)
        PlayerPanel = screenHost.Q<VisualElement>("PlayerSection");
        EnemyPanel = screenHost.Q<VisualElement>("EnemySection");

        PlayerAction = screenHost.Q<Label>("PlayerAction");
        EnemyAction = screenHost.Q<Label>("EnemyAction");
        PlayerBigIcon = screenHost.Q<VisualElement>("PlayerIcon");
        EnemyBigIcon = screenHost.Q<VisualElement>("EnemyIcon");

        SkillBar = screenHost.Q<VisualElement>("SkillBar");
        RunButton = screenHost.Q<Button>("Flee");

        SpellSlots = new VisualElement[12];
        if (SkillBar != null)
        {
            for (int i = 0; i < SpellSlots.Length; i++)
                SpellSlots[i] = SkillBar.Q<VisualElement>($"SpellSlot{i + 1}");
        }

        SlotViews = new SpellSlotView[SpellSlots.Length];
        for (int i = 0; i < SpellSlots.Length; i++)
        {
            if (SpellSlots[i] != null)
                SlotViews[i] = new SpellSlotView(SpellSlots[i]);
        }

        CacheCombatHud(screenHost);
    }

    public void Unbind()
    {
        PlayerPanel = null;
        EnemyPanel = null;

        PlayerAction = null;
        EnemyAction = null;
        PlayerBigIcon = null;
        EnemyBigIcon = null;

        SkillBar = null;
        SpellSlots = null;
        SlotViews = null;

        PlayerHpBar = null;
        PlayerManaBar = null;
        EnemyHpBar = null;
        EnemyManaBar = null;

        PlayerTurnBar = null;
        EnemyTurnBar = null;

        LogRoot = null;
        LogScroll = null;

        RunButton = null;
    }

    public void LogLine(string line) => AppendLog(line);

    public void LogAdvanced(string prefix, int value, string suffix, CombatLogType type) =>
        AppendLog(prefix, value, suffix, type);

    public void AttachLog(ScrollView logScroll)
    {
        LogScroll = logScroll;
    }

    private void CacheCombatHud(VisualElement screenHost)
    {
        // Player bars live inside the Player panel
        PlayerHpBar = PlayerPanel?.Q<PixelProgressBar>("Hp");
        PlayerManaBar = PlayerPanel?.Q<PixelProgressBar>("Mana");

        // Enemy bars live inside the Enemy panel
        EnemyHpBar = EnemyPanel?.Q<PixelProgressBar>("Hp");
        EnemyManaBar = EnemyPanel?.Q<PixelProgressBar>("Mana");

        PlayerTurnBar = PlayerPanel?.Q<PixelProgressBar>("TurnMeter");
        EnemyTurnBar = EnemyPanel?.Q<PixelProgressBar>("TurnMeter");

        // Log container
        LogRoot = screenHost?.Q<VisualElement>("Log");
        CacheBuffDebuffSlots();

        // Create a ScrollView inside Log so we can append lines (same behavior as controller)
        if (LogRoot != null)
        {
            LogRoot.Clear();

            LogScroll = new ScrollView();
            LogScroll.style.flexGrow = 1;
            LogScroll.style.paddingLeft = 16;
            LogScroll.style.paddingRight = 16;
            LogScroll.style.paddingTop = 16;
            LogScroll.style.paddingBottom = 16;

            LogRoot.Add(LogScroll);
        }
        else
        {
            Debug.LogWarning("[CombatTowerView] Log root not found (name='Log').");
            LogScroll = null;
        }
    }

    private void CacheBuffDebuffSlots()
    {
        // Player side
        CacheSlotsInPanel(PlayerPanel, out var pBuffs, out var pDebuffs);
        PlayerBuffSlots = pBuffs;
        PlayerDebuffSlots = pDebuffs;

        // Enemy side
        CacheSlotsInPanel(EnemyPanel, out var eBuffs, out var eDebuffs);
        EnemyBuffSlots = eBuffs;
        EnemyDebuffSlots = eDebuffs;
    }

    private static void CacheSlotsInPanel(
        VisualElement panelRoot,
        out VisualElement[] buffSlots,
        out VisualElement[] debuffSlots
    )
    {
        buffSlots = new VisualElement[10];
        debuffSlots = new VisualElement[10];

        if (panelRoot == null)
            return;

        // Buff area: panelRoot -> BuffsDebuffsTurnMeter -> Buffs -> List -> BuffSlotX
        var buffsList = panelRoot
            .Q<VisualElement>("BuffsDebuffsTurnMeter")
            ?.Q<VisualElement>("Buffs")
            ?.Q<VisualElement>("List");

        // Debuff area: panelRoot -> BuffsDebuffsTurnMeter -> Debuffs -> List -> DebuffSlotX
        var debuffsList = panelRoot
            .Q<VisualElement>("BuffsDebuffsTurnMeter")
            ?.Q<VisualElement>("Debuffs")
            ?.Q<VisualElement>("List");

        for (int i = 0; i < 10; i++)
        {
            buffSlots[i] = buffsList?.Q<VisualElement>($"BuffSlot{i + 1}");
            debuffSlots[i] = debuffsList?.Q<VisualElement>($"DebuffSlot{i + 1}");
        }
    }

    private static void HideSlotKeepLayout(VisualElement slot)
    {
        if (slot == null)
            return;

        slot.style.visibility = Visibility.Hidden;
        slot.pickingMode = PickingMode.Ignore; // no hover/click/tooltip

        // Reset visuals
        var icon = slot.Q<VisualElement>("Icon");
        if (icon != null)
            icon.style.backgroundImage = StyleKeyword.None;

        var stacks = slot.Q<Label>("Stacks");
        if (stacks != null)
            stacks.text = string.Empty;

        var duration = slot.Q<Label>("Duration");
        if (duration != null)
            duration.text = string.Empty;

        slot.tooltip = string.Empty;
    }

    private static void ShowSlot(VisualElement slot)
    {
        if (slot == null)
            return;

        slot.style.visibility = Visibility.Visible;
        slot.pickingMode = PickingMode.Position;
    }

    private static void ClearSlotsKeepLayout(VisualElement[] slots)
    {
        if (slots == null)
            return;
        for (int i = 0; i < slots.Length; i++)
            HideSlotKeepLayout(slots[i]);
    }

    public void RenderEffects(CombatActorType actor, IReadOnlyList<ActiveEffect> effects)
    {
        var buffSlots = actor == CombatActorType.Player ? PlayerBuffSlots : EnemyBuffSlots;
        var debuffSlots = actor == CombatActorType.Player ? PlayerDebuffSlots : EnemyDebuffSlots;

        ClearSlotsKeepLayout(buffSlots);
        ClearSlotsKeepLayout(debuffSlots);

        if (effects == null || effects.Count == 0)
            return;

        int b = 0;
        int d = 0;

        for (int i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            if (e == null || e.definition == null)
                continue;

            // Decide target row based on polarity
            bool isBuff = e.polarity == EffectPolarity.Buff;

            if (isBuff)
            {
                if (b >= buffSlots.Length)
                    continue;
                RenderOne(buffSlots[b], e);
                b++;
            }
            else
            {
                if (d >= debuffSlots.Length)
                    continue;
                RenderOne(debuffSlots[d], e);
                d++;
            }
        }
    }

    private static void RenderOne(VisualElement slot, ActiveEffect e)
    {
        if (slot == null || e == null)
            return;

        ShowSlot(slot);

        // ICON
        // Replace "icon" with your real field name in EffectDefinition
        Sprite iconSprite = e.definition.icon; // <-- adjust if needed

        var icon = slot.Q<VisualElement>("Icon");
        if (icon != null)
            icon.style.backgroundImage =
                iconSprite != null ? new StyleBackground(iconSprite) : StyleKeyword.None;

        // STACKS (you don't have stacks yet -> hide)
        var stacks = slot.Q<Label>("Stacks");
        if (stacks != null)
            stacks.text = e.TotalStacks > 0 ? e.TotalStacks.ToString() : "";

        // DURATION
        var duration = slot.Q<Label>("Duration");
        if (duration != null)
            duration.text = e.MaxRemainingTurns > 0 ? e.MaxRemainingTurns.ToString() : "";

        // Tooltip (nice for debugging)
        slot.tooltip = $"{e.effectId} ({e.polarity})";
    }

    // -------------------------
    // UI helpers (same logic as controller)
    // -------------------------

    public void SetBar(PixelProgressBar bar, int current, int max)
    {
        if (bar == null)
            return;

        int safeMax = Mathf.Max(1, max);
        int safeVal = Mathf.Clamp(current, 0, safeMax);

        bar.SetRange(0, safeMax);
        bar.SetValue(safeVal);
    }

    public void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (LogScroll == null)
            return;

        var label = new Label(line);
        label.AddToClassList("combat-log-line");
        label.style.color = Color.white;
        label.style.marginTop = 1;
        label.style.marginBottom = 1;

        LogScroll.Add(label);

        LogScroll.schedule.Execute(() =>
        {
            LogScroll.scrollOffset = new Vector2(0, float.MaxValue);
        });
    }

    public void AppendLog(string prefix, int value, string suffix, CombatLogType type)
    {
        if (LogScroll == null)
            return;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 1;
        row.style.marginBottom = 1;

        var left = new Label(string.IsNullOrEmpty(prefix) ? "" : (prefix + " "));
        left.AddToClassList("combat-log-line");
        left.style.color = Color.white;

        var number = new Label(value.ToString());
        number.AddToClassList("combat-log-line");

        switch (type)
        {
            case CombatLogType.Damage:
                number.style.color = new Color(0.85f, 0.2f, 0.2f);
                break;
            case CombatLogType.Heal:
                number.style.color = new Color(0.2f, 0.85f, 0.4f);
                break;
            default:
                number.style.color = Color.white;
                break;
        }

        var right = new Label(string.IsNullOrEmpty(suffix) ? "" : (" " + suffix));
        right.AddToClassList("combat-log-line");
        right.style.color = Color.white;

        row.Add(left);
        row.Add(number);
        row.Add(right);

        LogScroll.Add(row);

        LogScroll.schedule.Execute(() =>
        {
            LogScroll.scrollOffset = new Vector2(0, float.MaxValue);
        });
    }

    public static void SetScopedText(VisualElement root, string name, string value)
    {
        var label = root?.Q<Label>(name);
        if (label != null)
            label.text = value ?? string.Empty;
    }

    public static void SetSpriteBackground(VisualElement ve, Sprite sprite)
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

    // -------------------------
    // Cooldown visuals (moved as-is)
    // -------------------------

    public sealed class SpellSlotView
    {
        private readonly VisualElement _maskFull;
        private readonly VisualElement _maskResize;
        private readonly Label _cooldownLabel;

        public SpellSlotView(VisualElement slotRoot)
        {
            _maskFull = slotRoot.Q<VisualElement>("Mask") ?? slotRoot.Q<VisualElement>("MaskFull");
            _maskResize =
                slotRoot.Q<VisualElement>("MaskToResize")
                ?? slotRoot.Q<VisualElement>("MaskResize");
            _cooldownLabel = slotRoot.Q<Label>("Cooldown") ?? slotRoot.Q<Label>("CooldownLabel");

            ResetCooldownVisuals();
        }

        public void ResetCooldownVisuals()
        {
            SetDisplay(_maskFull, DisplayStyle.None);
            SetDisplay(_maskResize, DisplayStyle.None);
            SetDisplay(_cooldownLabel, DisplayStyle.None);

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

            if (remaining <= 0f)
            {
                ResetCooldownVisuals();
                return;
            }

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

    public static string ToTierRoman(Tier tier)
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

    public static string FormatMonsterTags(MonsterTag tags)
    {
        if (tags == MonsterTag.None)
            return string.Empty;

        var values = Enum.GetValues(typeof(MonsterTag));
        System.Collections.Generic.List<string> result = new();

        foreach (MonsterTag tag in values)
        {
            if (tag == MonsterTag.None)
                continue;

            if ((tags & tag) != 0)
                result.Add(tag.ToString());
        }

        return string.Join(" ", result);
    }

    public void RefreshCooldownUI(
        CombatEngine engine,
        VisualElement[] spellSlots,
        SpellSlotView[] slotViews
    )
    {
        if (engine == null || engine.State == null)
            return;

        if (spellSlots == null || slotViews == null)
            return;

        int count = Mathf.Min(spellSlots.Length, slotViews.Length);

        for (int i = 0; i < count; i++)
        {
            var slotRoot = spellSlots[i];
            var view = slotViews[i];

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
            if (engine.TryGetPlayerSpellCooldown(spellId, out int remaining, out int max))
            {
                view.SetCooldown(remaining, max);
            }
            else
            {
                view.ResetCooldownVisuals();
            }
        }
    }

    public void RenderPlayerPanel(PlayerPanelRenderData data)
    {
        if (PlayerPanel == null || data == null)
            return;

        SetScopedText(PlayerPanel, "Name", data.Name);
        SetScopedText(PlayerPanel, "LevelValue", data.LevelText);
        SetScopedText(PlayerPanel, "Tier", data.TierText);

        SetScopedText(PlayerPanel, "Class", data.ClassText);
        SetScopedText(PlayerPanel, "Spec", data.SpecText);

        // Small icon inside panel
        SetSpriteBackground(PlayerPanel.Q<VisualElement>("Icon"), data.SmallIcon);

        // Big icon on background
        SetSpriteBackground(PlayerBigIcon, data.BigIcon);
    }

    public void RenderEnemyPanel(EnemyPanelRenderData data)
    {
        if (EnemyPanel == null || data == null)
            return;

        SetScopedText(EnemyPanel, "Name", data.Name);
        SetScopedText(EnemyPanel, "LevelValue", data.LevelText);
        SetScopedText(EnemyPanel, "Tier", data.TierText);
        SetScopedText(EnemyPanel, "Tags", data.TagsText);

        SetSpriteBackground(EnemyPanel.Q<VisualElement>("Icon"), data.SmallIcon);
        SetSpriteBackground(EnemyBigIcon, data.BigIcon);
    }

    public void RenderActiveSpellSlots(List<ActiveSpellSlotData> activeSlots)
    {
        if (SpellSlots == null || SpellSlots.Length == 0)
            return;

        // Clear all slots first
        for (int i = 0; i < SpellSlots.Length; i++)
        {
            var slotRoot = SpellSlots[i];
            if (slotRoot == null)
                continue;

            slotRoot.userData = null;
            slotRoot.tooltip = string.Empty;
            slotRoot.style.display = DisplayStyle.Flex;

            // IMPORTANT: icon is on the Image child, not the root
            var imageVe = slotRoot.Q<VisualElement>("Image");
            if (imageVe != null)
                imageVe.style.backgroundImage = StyleKeyword.None;

            // Reset cooldown visuals
            SlotViews?[i]?.ResetCooldownVisuals();
        }

        if (activeSlots == null || activeSlots.Count == 0)
            return;

        int fillCount = Mathf.Min(activeSlots.Count, SpellSlots.Length);

        for (int i = 0; i < fillCount; i++)
        {
            var slotRoot = SpellSlots[i];
            if (slotRoot == null)
                continue;

            var entry = activeSlots[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.SpellId))
                continue;

            slotRoot.userData = entry.SpellId;

            // Apply icon to Image child
            var imageVe = slotRoot.Q<VisualElement>("Image");
            if (imageVe != null)
            {
                if (entry.Icon != null)
                    imageVe.style.backgroundImage = new StyleBackground(entry.Icon);
                else
                    imageVe.style.backgroundImage = StyleKeyword.None;
            }

            // Tooltip
            string name = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.SpellId
                : entry.DisplayName;
            slotRoot.tooltip = $"{name} (Lv {entry.Level})";
        }
    }

    public void UpdateHp(CombatActorType actor, int newHp, int maxHp)
    {
        if (actor == CombatActorType.Player)
            SetBar(PlayerHpBar, newHp, maxHp);
        else
            SetBar(EnemyHpBar, newHp, maxHp);
    }

    public void UpdateMana(CombatActorType actor, int newMana, int maxMana)
    {
        if (actor == CombatActorType.Player)
            SetBar(PlayerManaBar, newMana, maxMana);
        else
            SetBar(EnemyManaBar, newMana, maxMana);
    }

    public void UpdateTurn(CombatActorType actor, int newValue, int maxValue)
    {
        if (actor == CombatActorType.Player)
            SetBar(PlayerTurnBar, newValue, maxValue);
        else
            SetBar(EnemyTurnBar, newValue, maxValue);
    }

    public void SetActionText(CombatActorType actor, string text)
    {
        if (actor == CombatActorType.Player)
        {
            if (PlayerAction != null)
                PlayerAction.text = text;
        }
        else
        {
            if (EnemyAction != null)
                EnemyAction.text = text;
        }
    }

    public void ClearLog()
    {
        LogScroll?.Clear();
    }

    public void OnPlayerSpellFired()
    {
        // View does NOT know about engine; controller will call RefreshCooldownUI separately for now
        // We'll handle cooldown refresh through coordinator in next step (with a cooldown provider).
    }

    public void OnEnemySpellFired() { }
}
