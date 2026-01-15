using System;
using System.Globalization;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

public class LoadGameController : MonoBehaviour, IOverlayController
{
    [SerializeField]
    private ScreenSwapper _screenSwapper;

    private Button _exitModalBtn;
    private SaveSlotOverlayContext _ctx;
    private Label _title;

    public void Bind(VisualElement overlayHost, ScreenSwapper swapper, object context = null)
    {
        _screenSwapper = swapper;
        _ctx = context as SaveSlotOverlayContext ?? new SaveSlotOverlayContext();

        _exitModalBtn = overlayHost.Q<Button>("Exit");
        if (_exitModalBtn == null)
        {
            Debug.LogError("LoadGameController: Could not find Button named 'Exit'.");
            return;
        }
        _exitModalBtn.clicked += OnExit;

        _title = overlayHost.Q<Label>("Title");
        if (_title != null)
            _title.text = _ctx.mode == SaveSlotOverlayMode.Load ? "Load Game" : "Overwrite";

        // Refresh 5 slots
        for (int slot = 1; slot <= 5; slot++)
        {
            RefreshSlotUI(overlayHost, slot);

            var btn = overlayHost.Q<Button>($"SaveSlot{slot}");
            int captured = slot;
            SetButtonClick(btn, () => OnSlotClicked(captured));
        }
    }

    public void Unbind()
    {
        if (_exitModalBtn != null)
            _exitModalBtn.clicked -= OnExit;
        for (int slot = 1; slot <= 5; slot++)
        {
            var btn = _exitModalBtn?.parent?.Q<Button>($"SaveSlot{slot}");
            ClearButtonClick(btn);
        }
        _ctx = null;
    }

    private void OnSlotClicked(int slot)
    {
        // ------------------------
        // LOAD MODE
        // ------------------------
        if (_ctx.mode == SaveSlotOverlayMode.Load)
        {
            var data = SaveService.LoadSlotOrNull(slot);
            if (data == null)
            {
                return;
            }
            RunSession.Clear();
            SaveSession.Clear();

            //Debug.Log($"Loading slot {slot}: {data.characterName}");

            SaveSession.SetCurrent(slot, data);
            InitializeRuntimeAndEnterDashboard(data);

            return;
        }

        // ------------------------
        // OVERWRITE MODE
        // ------------------------
        if (_ctx.pendingSave == null)
        {
            Debug.LogError("Overwrite mode opened without pendingSave.");
            return;
        }

        bool isTaken = SaveService.HasSaveInSlot(slot);

        // Empty slot → save immediately
        if (!isTaken)
        {
            SaveAndEnterDashboard(slot, _ctx.pendingSave);
            return;
        }

        // Slot taken → ask confirmation
        _screenSwapper.ShowGlobalModal(
            title: "Overwrite",
            message: $"Slot {slot} already has a save. Do you want to overwrite it?",
            primaryText: "Overwrite",
            onPrimary: () =>
            {
                SaveAndEnterDashboard(slot, _ctx.pendingSave);
            },
            secondaryText: "Cancel",
            onSecondary: null,
            closeOnOutsideClick: true
        );
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

    // ------------------------
    // SHARED SAVE + ENTER
    // ------------------------
    private void SaveAndEnterDashboard(int slot, SaveData data)
    {
        SaveService.SaveToSlot(slot, data);
        SaveSession.SetCurrent(slot, data);
        InitializeRuntimeAndEnterDashboard(data);
    }

    private void InitializeRuntimeAndEnterDashboard(SaveData data)
    {
        RunSession.InitializeFromSave(
            save: data,
            db: GameConfigProvider.Instance.SpellDatabase,
            progression: GameConfigProvider.Instance.SpellProgression
        );

        _screenSwapper.ShowScreen("character");
    }

    // ------------------------
    // UI HELPERS
    // ------------------------
    private void RefreshSlotUI(VisualElement overlayHost, int slot)
    {
        var slotBtn = overlayHost.Q<Button>($"SaveSlot{slot}");
        var empty = slotBtn.Q<VisualElement>("EmptyContainer");
        var filled = slotBtn.Q<VisualElement>("FilledContainer");

        var data = SaveService.LoadSlotOrNull(slot);
        bool taken = data != null;

        empty.style.display = taken ? DisplayStyle.None : DisplayStyle.Flex;
        filled.style.display = taken ? DisplayStyle.Flex : DisplayStyle.None;

        if (!taken)
            return;

        filled.Q<Label>("Name").text = string.IsNullOrWhiteSpace(data.characterName)
            ? "Unknown"
            : data.characterName;

        filled.Q<Label>("Level").text = data.level.ToString();
        filled.Q<Label>("TotalPlayTime").text = FormatSecondsToHms(data.totalPlayTimeSeconds);
        filled.Q<Label>("LastSavedAt").text = FormatUtcIsoToLocal(data.lastSavedUtc);
    }

    private string FormatSecondsToHms(int totalSeconds)
    {
        if (totalSeconds <= 0)
            return "00:00:00";

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private string FormatUtcIsoToLocal(string utcIso)
    {
        if (string.IsNullOrWhiteSpace(utcIso))
            return "Unknown";

        if (DateTime.TryParse(utcIso, null, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        return "Unknown";
    }

    private void OnExit()
    {
        _screenSwapper.CloseOverlay("load_game");
    }
}
