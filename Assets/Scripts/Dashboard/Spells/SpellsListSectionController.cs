using System;
using System.Linq;
using MyGame.Run;
using MyGame.Save;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SpellsListSectionController
{
    private readonly VisualTreeAsset _rowTemplate;

    private VisualElement _panelRoot;
    private ScrollView _list;

    public Action<string> RequestChooseActiveSlot;
    public Action RequestRefreshAll;
    private ScreenSwapper _swapper;

    // (spellId, iconSprite, pointerDown)
    public Action<string, Sprite, PointerDownEvent> RequestBeginDrag;

    // row click selects spell
    public Action<string> RequestSelectSpell;

    public Func<bool> IsDraggingSpell;

    public SpellsListSectionController(VisualTreeAsset rowTemplate)
    {
        _rowTemplate = rowTemplate;
    }

    public void Bind(VisualElement spellsPanel, ScreenSwapper swapper)
    {
        _panelRoot = spellsPanel;
        _swapper = swapper;

        if (_panelRoot == null)
        {
            Debug.LogError("SpellsListSectionController.Bind: spellsPanel is null.");
            return;
        }

        if (_rowTemplate == null)
        {
            Debug.LogError("SpellsListSectionController: Row template (VisualTreeAsset) is null.");
            return;
        }

        if (!SaveSession.HasSave)
        {
            Debug.LogError("SpellsListSectionController.Bind: No save loaded.");
            return;
        }

        if (!RunSession.IsInitialized || RunSession.Spellbook == null)
        {
            Debug.LogError("SpellsListSectionController.Bind: RunSession is not initialized.");
            return;
        }

        _list = _panelRoot.Q<ScrollView>("SpellList");
        if (_list == null)
        {
            Debug.LogError("SpellsListSectionController: Could not find ScrollView 'SpellList'.");
            return;
        }

        RebuildList();
    }

    public void Unbind()
    {
        _panelRoot = null;
        _swapper = null;
        _list = null;

        RequestChooseActiveSlot = null;
        RequestRefreshAll = null;
        RequestBeginDrag = null;
        RequestSelectSpell = null;
    }

    public void RebuildList()
    {
        if (_list == null)
            return;

        _list.contentContainer.Clear();

        var db = GameConfigProvider.Instance.SpellDatabase;
        var book = RunSession.Spellbook;
        int activeCount = CountActiveSpells(book);

        var ownedSpellIds = book.Entries.Keys.OrderBy(id => id).ToList();

        foreach (var spellId in ownedSpellIds)
        {
            var def = db.GetById(spellId);
            var entry = book.Get(spellId);

            if (def == null || entry == null)
                continue;

            // Clone template
            var tree = _rowTemplate.CloneTree();

            // Prefer the actual root inside template named "Row"
            var rowRoot = tree.Q<VisualElement>("Row");
            if (rowRoot == null)
                rowRoot = tree; // fallback

            rowRoot.pickingMode = PickingMode.Position;

            var icon = rowRoot.Q<VisualElement>("Icon");
            var iconFrame = rowRoot.Q<VisualElement>("IconFrame");
            var iconPart = rowRoot.Q<VisualElement>("IconPart");
            var nameLabel = rowRoot.Q<Label>("NameLabel");
            var masteryLevel = rowRoot.Q<Label>("MasteryLevel");
            var fill = rowRoot.Q<VisualElement>("Fill");

            var setActiveBtn = rowRoot.Q<Button>("SetActive");
            var detailBtn = rowRoot.Q<Button>("Detail");

            if (icon == null || nameLabel == null || masteryLevel == null || fill == null)
            {
                Debug.LogError(
                    "SpellsListSectionController: Row template missing Icon/NameLabel/MasteryLevel/Fill."
                );
                continue;
            }

            // Row click selects spell
            rowRoot.RegisterCallback<PointerDownEvent>(
                evt =>
                {
                    if (evt.button != 0)
                        return;

                    RequestSelectSpell?.Invoke(spellId);
                },
                TrickleDown.TrickleDown
            );

            bool isActive = entry.activeSlotIndex >= 0;

            if (setActiveBtn != null)
            {
                setActiveBtn.text = isActive ? "Remove" : "Set Active";

                bool isLastActiveSpell = isActive && activeCount <= 1;

                setActiveBtn.SetEnabled(!isLastActiveSpell);
                if (isLastActiveSpell && _swapper != null)
                {
                    setActiveBtn.EnableTooltip(
                        swapper: _swapper,
                        text: "Can't remove the spell. You must have at least 1 active spell.",
                        offset: 10f,
                        maxWidth: 300f
                    );
                }
                else
                {
                    setActiveBtn.RegisterCallback<PointerEnterEvent>(_ => _swapper?.HideTooltip());
                }
            }

            // Icon + drag ONLY from icon
            icon.style.backgroundImage =
                def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;

            icon.pickingMode = PickingMode.Position;
            if (iconPart != null)
                iconPart.pickingMode = PickingMode.Position;
            if (iconFrame != null)
                iconFrame.pickingMode = PickingMode.Position;
            if (iconFrame != null)
            {
                iconFrame.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    RequestBeginDrag?.Invoke(spellId, def.icon, evt);
                    evt.StopPropagation();
                });
            }

            // Tooltip on hover (use global SpellDetailTooltip instance)
            if (iconFrame != null)
            {
                iconFrame.RegisterCallback<PointerEnterEvent>(
                    evt =>
                    {
                        try
                        {
                            if (IsDraggingSpell?.Invoke() == true)
                                return;

                            if (_swapper == null)
                            {
                                Debug.LogWarning(
                                    "SpellsListSectionController: ScreenSwapper is null when hovering spell icon."
                                );
                                return;
                            }

                            var tooltip = _swapper.GetCustomTooltipElement("SpellDetailTooltip");
                            if (tooltip != null)
                            {
                                // learned = true for spells in owned list
                                bool ok =
                                    MyGame.Helpers.SpellDetailTooltipBuilder.TryPopulateForSpellId(
                                        tooltip,
                                        spellId,
                                        learned: true,
                                        swapper: _swapper
                                    );

                                if (!ok)
                                    Debug.LogWarning(
                                        $"SpellDetailTooltipBuilder failed to populate for '{spellId}'."
                                    );

                                var worldPos = evt.position;
                                _swapper.ShowCustomTooltipAtWorldPosition(tooltip, worldPos);
                            }
                            else
                            {
                                // Fallback: show simple text tooltip so we can confirm pointer events work
                                _swapper.ShowTooltipAtElement(
                                    (VisualElement)evt.currentTarget,
                                    def.displayName ?? spellId
                                );
                                Debug.Log(
                                    "SpellsListSectionController: SpellDetailTooltip not found â€” showing fallback text tooltip."
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    },
                    TrickleDown.TrickleDown
                );

                iconFrame.RegisterCallback<PointerMoveEvent>(
                    evt =>
                    {
                        if (IsDraggingSpell?.Invoke() == true)
                            return;
                        if (_swapper == null)
                            return;
                        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                            return;

                        var tooltip = _swapper.GetCustomTooltipElement("SpellDetailTooltip");
                        if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
                            return;

                        _swapper.PositionCustomTooltipAtWorldPosition(tooltip, evt.position);
                    },
                    TrickleDown.TrickleDown
                );

                iconFrame.RegisterCallback<PointerLeaveEvent>(
                    evt =>
                    {
                        if (_swapper != null && _swapper.TryFreezeCustomTooltipIfAltHeld())
                            return;
                        var anchor = evt.currentTarget as VisualElement;
                        if (anchor != null && anchor.worldBound.Contains(evt.position))
                            return;
                        if (IsPointerOverSpellDetailTooltip(evt.position))
                            return;
                        _swapper?.HideCustomTooltip();
                        _swapper?.HideTooltip();
                    },
                    TrickleDown.TrickleDown
                );
                iconFrame.RegisterCallback<PointerOutEvent>(
                    evt =>
                    {
                        if (_swapper != null && _swapper.TryFreezeCustomTooltipIfAltHeld())
                            return;
                        var anchor = evt.currentTarget as VisualElement;
                        if (anchor != null && anchor.worldBound.Contains(evt.position))
                            return;
                        if (IsPointerOverSpellDetailTooltip(evt.position))
                            return;
                        _swapper?.HideCustomTooltip();
                        _swapper?.HideTooltip();
                    },
                    TrickleDown.TrickleDown
                );
            }

            nameLabel.text = def.displayName;
            masteryLevel.text = entry.level.ToString();

            int xpToNext = book.GetXpToNextLevel(spellId);
            float pct = (xpToNext <= 0) ? 1f : Mathf.Clamp01(entry.experience / (float)xpToNext);
            fill.style.width = Length.Percent(pct * 100f);

            var activeSlotLabel = rowRoot.Q<Label>("ActiveSlotLabel");
            if (activeSlotLabel != null)
                activeSlotLabel.text =
                    entry.activeSlotIndex >= 0 ? $"Active: {entry.activeSlotIndex + 1}" : "";

            if (setActiveBtn != null)
            {
                setActiveBtn.clicked += () =>
                {
                    bool currentlyActive = entry.activeSlotIndex >= 0;

                    if (currentlyActive)
                    {
                        book.Deactivate(spellId);
                        SaveSessionRuntimeSave.SaveNowWithRuntime();
                        RequestRefreshAll?.Invoke();
                        return;
                    }

                    if (RequestChooseActiveSlot != null)
                    {
                        RequestChooseActiveSlot.Invoke(spellId);
                        return;
                    }
                };
            }

            if (detailBtn != null)
            {
                detailBtn.clicked += () =>
                {
                    Debug.Log($"Detail clicked: {def.displayName} ({spellId})");
                };
            }

            _list.contentContainer.Add(tree);
        }
    }

    private static int CountActiveSpells(PlayerSpellbook book)
    {
        if (book == null)
            return 0;

        int count = 0;
        foreach (var e in book.Entries.Values)
        {
            if (e != null && e.activeSlotIndex >= 0)
                count++;
        }
        return count;
    }

    private bool IsPointerOverSpellDetailTooltip(Vector2 pointerPosition)
    {
        if (_swapper == null)
            return false;

        var tooltip = _swapper.GetCustomTooltipElement("SpellDetailTooltip");
        if (tooltip == null || tooltip.style.display != DisplayStyle.Flex)
            return false;

        return tooltip.worldBound.Contains(pointerPosition);
    }
}
