// SpellSectionController.cs
using System.Linq;
using MyGame.Run;
using MyGame.Save;
using MyGame.UI;
using MyGame.UI.Spells; // SpellDragController namespace
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SpellSectionController
{
    private ScreenSwapper _screenSwapper;

    private readonly VisualTreeAsset _rowTemplate;

    private int _unlockedActiveSlots = 1;

    private VisualElement _panelRoot;

    // Details panel (SpellSlot) - optional
    private VisualElement _detailIcon;
    private Label _detailName;
    private Label _detailLevel;
    private PixelProgressBar _detailExpBar;
    private Button _detailAddExp;

    private string _currentSpellId;
    private const int ExpPerClick = 5;

    private SpellsListSectionController _list;
    private ActiveSpellsSectionController _active;

    private SpellDragController _drag;

    // Drag state
    private string _dragSpellId;
    private int _dragSourceSlotIndex = -1; // -1 = from learned list

    public SpellSectionController(VisualTreeAsset rowTemplate)
    {
        _rowTemplate = rowTemplate;
    }

    public void Bind(VisualElement spellsPanel, ScreenSwapper swapper)
    {
        _screenSwapper = swapper;
        _panelRoot = spellsPanel;

        if (_panelRoot == null)
        {
            Debug.LogError("SpellSectionController.Bind: spellsPanel is null.");
            return;
        }

        if (_screenSwapper == null)
        {
            Debug.LogError("SpellSectionController.Bind: swapper is null.");
            return;
        }

        if (!SaveSession.HasSave)
        {
            Debug.LogError("SpellSectionController.Bind: No save loaded. Load a slot first.");
            return;
        }

        if (!RunSession.IsInitialized || RunSession.Spellbook == null)
        {
            Debug.LogError(
                "SpellSectionController.Bind: RunSession is not initialized (Spellbook missing)."
            );
            return;
        }

        if (_rowTemplate == null)
        {
            Debug.LogError(
                "SpellSectionController.Bind: rowTemplate is null (SpellRow template missing)."
            );
            return;
        }

        _unlockedActiveSlots = ActiveSlotUnlocks.Calculate(SaveSession.Current);
        BindDetailsPanelIfPresent();

        // Learned list + active slots
        _list = new SpellsListSectionController(_rowTemplate);
        _list.Bind(_panelRoot, _screenSwapper);

        _active = new ActiveSpellsSectionController("Icons/lock");
        _active.Bind(_panelRoot, unlockedSlots: _unlockedActiveSlots);

        // Drag controller: uses root panel for ghost + hit-test
        _drag = new SpellDragController(
            root: _panelRoot,
            isSlotUnlocked: slotIndex => slotIndex >= 0 && slotIndex < _unlockedActiveSlots,
            onDropToSlotIndex: OnDroppedToActiveSlot
        );

        // Row click selects spell (details)
        _list.RequestSelectSpell = spellId =>
        {
            _currentSpellId = spellId;
            RefreshDetails();
        };

        // Drag start from learned list icon
        _list.RequestBeginDrag = (spellId, iconSprite, evt) =>
        {
            if (string.IsNullOrWhiteSpace(spellId))
                return;

            _dragSpellId = spellId;
            _dragSourceSlotIndex = -1; // from learned list
            _drag.BeginDrag(evt, iconSprite);
        };

        // Drag start from active slot icon
        _active.RequestBeginDragFromSlot = (slotIndex, evt) =>
        {
            if (slotIndex < 0 || slotIndex >= _unlockedActiveSlots)
                return;

            var entry = RunSession.Spellbook.GetSpellInSlot(slotIndex);
            if (entry == null || string.IsNullOrWhiteSpace(entry.spellId))
                return;

            var def = GameConfigProvider.Instance.SpellDatabase.GetById(entry.spellId);

            _dragSpellId = entry.spellId;
            _dragSourceSlotIndex = slotIndex; // from active
            _drag.BeginDrag(evt, def != null ? def.icon : null);
        };

        // Click button SetActive -> your modal flow
        _list.RequestChooseActiveSlot = spellId =>
        {
            int slot = FindFirstFreeUnlockedSlotOrMinusOne();
            if (slot < 0)
            {
                var ctx = BuildChooseSpellSlotContext(spellId);
                ctx.OnSlotChosen = slotIndex =>
                {
                    RunSession.Spellbook.SetActive(spellId, slotIndex);
                    SaveSessionRuntimeSave.SaveNowWithRuntime();
                    RefreshAll();
                };
                _screenSwapper.ShowOverlay("change_spell_slot", ctx);
                return;
            }

            RunSession.Spellbook.SetActive(spellId, slot);
            SaveSessionRuntimeSave.SaveNowWithRuntime();
            RefreshAll();
        };

        _list.RequestRefreshAll = RefreshAll;

        // Default details spell
        _currentSpellId = RunSession.Spellbook.Entries.Keys.FirstOrDefault();
        RefreshDetails();
        RefreshAll();
    }

    public void Unbind()
    {
        if (_detailAddExp != null)
            _detailAddExp.clicked -= OnAddExpClicked;

        // ✅ Cancel drag BEFORE nulling references
        _drag?.CancelDrag();
        ClearDragState();

        _list?.Unbind();
        _active?.Unbind();

        _list = null;
        _active = null;
        _drag = null;

        _panelRoot = null;

        _detailIcon = null;
        _detailName = null;
        _detailLevel = null;
        _detailExpBar = null;
        _detailAddExp = null;

        _currentSpellId = null;

        _dragSpellId = null;
        _dragSourceSlotIndex = -1;

        _screenSwapper = null;
    }

    public void OnTabShown()
    {
        if (_panelRoot == null)
            return;

        var sv = _panelRoot.Q<ScrollView>("SpellList");
        if (sv == null)
            return;

        // Run once when ScrollView gets real geometry after being shown
        void OnGeom(GeometryChangedEvent _)
        {
            sv.UnregisterCallback<GeometryChangedEvent>(OnGeom);

            RefreshAll();

            sv.contentContainer.MarkDirtyRepaint();
            sv.MarkDirtyRepaint();
        }

        sv.RegisterCallback<GeometryChangedEvent>(OnGeom);
    }

    // -----------------------------
    // DROP: list->active, active->active (move/swap)
    // -----------------------------
    private void OnDroppedToActiveSlot(int targetSlotIndex)
    {
        if (string.IsNullOrWhiteSpace(_dragSpellId))
            return;

        if (targetSlotIndex < 0 || targetSlotIndex >= _unlockedActiveSlots)
        {
            ClearDragState();
            return;
        }

        var book = RunSession.Spellbook;

        var targetEntry = book.GetSpellInSlot(targetSlotIndex);
        var targetSpellId = targetEntry != null ? targetEntry.spellId : null;

        if (_dragSourceSlotIndex == targetSlotIndex)
        {
            ClearDragState();
            return;
        }

        if (_dragSourceSlotIndex >= 0)
        {
            var sourceSlot = _dragSourceSlotIndex;

            if (string.IsNullOrWhiteSpace(targetSpellId))
            {
                book.SetActive(_dragSpellId, targetSlotIndex);
            }
            else
            {
                book.SetActive(targetSpellId, sourceSlot);
                book.SetActive(_dragSpellId, targetSlotIndex);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(targetSpellId))
                book.Deactivate(targetSpellId);

            book.SetActive(_dragSpellId, targetSlotIndex);
        }

        SaveSessionRuntimeSave.SaveNowWithRuntime();
        ClearDragState();
        RefreshAll();
    }

    private void ClearDragState()
    {
        _dragSpellId = null;
        _dragSourceSlotIndex = -1;
    }

    // -----------------------------
    // Details Panel
    // -----------------------------
    private void BindDetailsPanelIfPresent()
    {
        var spellSlot = _panelRoot.Q<VisualElement>("SpellSlot");
        if (spellSlot == null)
            return;

        _detailIcon = spellSlot.Q<VisualElement>("Icon");
        _detailName = spellSlot.Q<Label>("Name");
        _detailLevel = spellSlot.Q<Label>("Level");
        _detailExpBar = spellSlot.Q<PixelProgressBar>("SpellExp");
        _detailAddExp = spellSlot.Q<Button>("AddExp");

        if (
            _detailIcon == null
            || _detailName == null
            || _detailLevel == null
            || _detailExpBar == null
            || _detailAddExp == null
        )
        {
            Debug.LogWarning(
                "SpellSectionController: SpellSlot found but missing required elements."
            );
            return;
        }

        _detailAddExp.clicked += OnAddExpClicked;
    }

    private void OnAddExpClicked()
    {
        if (string.IsNullOrWhiteSpace(_currentSpellId))
            return;

        RunSession.Spellbook.GrantExperience(_currentSpellId, ExpPerClick);
        SaveSessionRuntimeSave.SaveNowWithRuntime();

        RefreshDetails();
        RefreshAll();
    }

    private void RefreshDetails()
    {
        if (_detailName == null)
            return;

        if (string.IsNullOrWhiteSpace(_currentSpellId))
        {
            _detailName.text = "No Spells";
            _detailLevel.text = "";
            _detailIcon.style.backgroundImage = StyleKeyword.None;

            _detailExpBar.LabelText = "EXP";
            _detailExpBar.SetRange(0, 1);
            _detailExpBar.SetValue(0);

            _detailAddExp.SetEnabled(false);
            return;
        }

        var db = GameConfigProvider.Instance.SpellDatabase;
        var def = db.GetById(_currentSpellId);
        var entry = RunSession.Spellbook.Get(_currentSpellId);

        if (def == null || entry == null)
        {
            _detailName.text = "Unknown Spell";
            _detailLevel.text = "";
            _detailIcon.style.backgroundImage = StyleKeyword.None;

            _detailExpBar.LabelText = "EXP";
            _detailExpBar.SetRange(0, 1);
            _detailExpBar.SetValue(0);

            _detailAddExp.SetEnabled(false);
            return;
        }

        _detailAddExp.SetEnabled(true);

        _detailIcon.style.backgroundImage =
            def.icon != null ? new StyleBackground(def.icon) : StyleKeyword.None;

        _detailName.text = def.displayName;
        _detailLevel.text = $"Lv {entry.level}";

        int xpToNext = RunSession.Spellbook.GetXpToNextLevel(_currentSpellId);
        if (xpToNext <= 0)
        {
            _detailExpBar.LabelText = "MAX";
            _detailExpBar.SetRange(0, 1);
            _detailExpBar.SetValue(1);
        }
        else
        {
            _detailExpBar.LabelText = "EXP";
            _detailExpBar.SetRange(0, xpToNext);
            _detailExpBar.SetValue(entry.experience);
        }
    }

    // -----------------------------
    // Active slots helpers
    // -----------------------------
    private int FindFirstFreeUnlockedSlotOrMinusOne()
    {
        var book = RunSession.Spellbook;

        for (int slot = 0; slot < _unlockedActiveSlots; slot++)
        {
            if (book.GetSpellInSlot(slot) == null)
                return slot;
        }

        return -1;
    }

    // -----------------------------
    // Refresh
    // -----------------------------
    private void RefreshAll()
    {
        if (_panelRoot == null)
            return;

        if (_panelRoot.resolvedStyle.display == DisplayStyle.None)
            return;

        _list?.RebuildList();
        _active?.Refresh();
    }

    private ChooseSpellSlotOverlayContext BuildChooseSpellSlotContext(string spellId)
    {
        var book = RunSession.Spellbook;
        var db = GameConfigProvider.Instance.SpellDatabase;

        var ctx = new ChooseSpellSlotOverlayContext
        {
            spellId = spellId,
            spellName = db.GetById(spellId)?.displayName ?? spellId,
            unlockedSlots = _unlockedActiveSlots,
            slots = new ChooseSpellSlotOverlayContext.SlotInfo[_unlockedActiveSlots],
        };

        for (int slot = 0; slot < _unlockedActiveSlots; slot++)
        {
            string occupiedSpellId = null;

            foreach (var kvp in book.Entries)
            {
                if (kvp.Value != null && kvp.Value.activeSlotIndex == slot)
                {
                    occupiedSpellId = kvp.Key;
                    break;
                }
            }

            string occupiedSpellName = null;
            if (!string.IsNullOrWhiteSpace(occupiedSpellId))
                occupiedSpellName = db.GetById(occupiedSpellId)?.displayName ?? occupiedSpellId;

            ctx.slots[slot] = new ChooseSpellSlotOverlayContext.SlotInfo
            {
                slotIndex = slot,
                occupiedSpellId = occupiedSpellId,
                occupiedSpellName = occupiedSpellName,
            };
        }

        return ctx;
    }
}
