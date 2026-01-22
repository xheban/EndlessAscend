// File: Assets/Scripts/Settings/KeyBindController.cs
// Type: Overlay controller
// Responsibility: Generic keybinding overlay (reusable for any action/slot).
// Unity 6 - UI Toolkit

using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class KeyBindController : MonoBehaviour, IOverlayController
{
    // Overlay id in ScreenSwapper.overlays list (must match exactly)
    private const string OverlayId = "key_bind"; // <-- set this to your OverlayEntry.overlayId

    private VisualElement _root;
    private ScreenSwapper _swapper;

    private Label _title;
    private Label _message;
    private Label _pressed;

    private Button _primary;
    private Button _secondary;
    private Button _exit;

    private KeyBindOverlayContext _ctx;

    private bool _capturing;
    private KeyCode _selectedKey = KeyCode.None;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;
        _ctx = context as KeyBindOverlayContext;

        if (_root == null)
        {
            Debug.LogError("KeyBindController.Bind: screenHost is null.");
            return;
        }

        if (_swapper == null)
        {
            Debug.LogError("KeyBindController.Bind: swapper is null.");
            return;
        }

        if (_ctx == null)
        {
            Debug.LogError(
                "KeyBindController.Bind: context is null or wrong type. Expected KeyBindOverlayContext."
            );
            return;
        }

        // Query UI
        var overlayRoot = _root.Q<VisualElement>("KeyBindOverlay");
        if (overlayRoot == null)
        {
            Debug.LogError(
                "KeyBindController.Bind: Could not find 'KeyBindOverlay' root element in UXML."
            );
            return;
        }

        _title = overlayRoot.Q<Label>("title");
        _message = overlayRoot.Q<Label>("message");
        _pressed = overlayRoot.Q<Label>("Pressed");

        _primary = overlayRoot.Q<Button>("primaryBtn");
        _secondary = overlayRoot.Q<Button>("secondaryBtn");
        _exit = overlayRoot.Q<Button>("exit");

        if (
            _title == null
            || _message == null
            || _pressed == null
            || _primary == null
            || _secondary == null
            || _exit == null
        )
        {
            Debug.LogError(
                "KeyBindController.Bind: Missing UI elements (title/message/Pressed/buttons/exit). Check UXML names."
            );
            return;
        }

        // Fill UI from context
        _title.text = string.IsNullOrEmpty(_ctx.Title) ? "Bind" : _ctx.Title;

        string target = string.IsNullOrEmpty(_ctx.TargetName) ? "" : $" ({_ctx.TargetName})";
        _message.text = string.IsNullOrEmpty(_ctx.Message)
            ? $"Press any key to bind{target}."
            : _ctx.Message;

        _selectedKey = _ctx.CurrentKey;
        _pressed.text = _selectedKey == KeyCode.None ? "-" : FormatKey(_selectedKey);

        // Wire buttons
        _primary.text = "Bind";
        _secondary.text = "Cancel";

        _primary.clicked += OnBindClicked;
        _secondary.clicked += OnCancelClicked;
        _exit.clicked += OnCancelClicked;

        // Start capturing immediately
        _capturing = true;
    }

    public void Unbind()
    {
        if (_primary != null)
            _primary.clicked -= OnBindClicked;
        if (_secondary != null)
            _secondary.clicked -= OnCancelClicked;
        if (_exit != null)
            _exit.clicked -= OnCancelClicked;

        _capturing = false;

        _ctx = null;
        _root = null;
        _swapper = null;

        _title = null;
        _message = null;
        _pressed = null;
        _primary = null;
        _secondary = null;
        _exit = null;
    }

    private void Update()
    {
        if (!_capturing)
            return;

        // This is the most reliable way to capture input regardless of UI Toolkit focus:
        if (!Input.anyKeyDown)
            return;

        var key = DetectPressedKey(_ctx.IgnoreMouseButtons);
        if (key == KeyCode.None)
            return;

        _selectedKey = key;
        if (_pressed != null)
            _pressed.text = FormatKey(_selectedKey);
    }

    private void OnBindClicked()
    {
        if (_ctx == null)
            return;

        if (_selectedKey == KeyCode.None)
        {
            // No key chosen yet; keep overlay open.
            // You can also flash the label, but keeping simple.
            return;
        }

        _ctx.OnBound?.Invoke(_selectedKey);
        CloseSelf();
    }

    private void OnCancelClicked()
    {
        _ctx?.OnCancelled?.Invoke();
        CloseSelf();
    }

    private void CloseSelf()
    {
        // Close this overlay by id
        _swapper.CloseOverlay(OverlayId);
    }

    private static KeyCode DetectPressedKey(bool ignoreMouseButtons)
    {
        foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
        {
            if (ignoreMouseButtons && code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6)
                continue;

            if (Input.GetKeyDown(code))
                return code;
        }

        return KeyCode.None;
    }

    private static string FormatKey(KeyCode key)
    {
        if (key == KeyCode.None)
            return "-";

        string s = key.ToString();
        if (s.StartsWith("Alpha"))
            return s.Substring("Alpha".Length);
        if (s.StartsWith("Keypad"))
            return "Num" + s.Substring("Keypad".Length);
        return s;
    }
}
