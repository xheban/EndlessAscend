using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChooseSlotOverlayController : MonoBehaviour, IOverlayController
{
    private ScreenSwapper _screenSwapper;
    private ChooseSpellSlotOverlayContext _ctx;

    private VisualElement _body;
    private Label _title;
    private Button _exitBtn;

    public void Bind(VisualElement overlayHost, ScreenSwapper swapper, object context = null)
    {
        _screenSwapper = swapper;
        _ctx = context as ChooseSpellSlotOverlayContext;

        _body = overlayHost.Q<VisualElement>("Body");
        _title = overlayHost.Q<Label>("Title");
        _exitBtn = overlayHost.Q<Button>("Exit");

        if (_body == null || _exitBtn == null)
        {
            Debug.LogError("ChooseSlotOverlayController: Missing Body or Exit.");
            return;
        }

        _exitBtn.clicked += OnExit;

        BuildUI();
    }

    public void Unbind()
    {
        if (_exitBtn != null)
            _exitBtn.clicked -= OnExit;

        _ctx = null;
        _screenSwapper = null;
    }

    private void BuildUI()
    {
        _body.Clear();
        if (_ctx == null || _ctx.slots == null)
            return;

        foreach (var slot in _ctx.slots)
        {
            // Row container
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 8;
            row.AddToClassList("slot-row"); // optional USS class

            // Label
            var label = new Label();
            label.text = string.IsNullOrEmpty(slot.occupiedSpellName)
                ? $"Slot {slot.slotIndex + 1}: (Empty)"
                : $"Slot {slot.slotIndex + 1}: {slot.occupiedSpellName}";
            label.AddToClassList("label-1");

            // Button
            var button = new Button();
            button.text = "Choose";
            button.AddToClassList("button-2");

            int slotIndex = slot.slotIndex;
            button.clicked += () =>
            {
                _ctx.OnSlotChosen?.Invoke(slotIndex);
                _screenSwapper.CloseOverlay("change_spell_slot");
            };

            row.Add(label);
            row.Add(button);
            _body.Add(row);
        }
    }

    private void OnExit()
    {
        _screenSwapper.CloseOverlay("change_spell_slot");
    }
}
