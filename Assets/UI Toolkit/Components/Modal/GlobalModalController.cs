using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GlobalModalController
{
    private VisualElement _root;

    private Label _title;
    private Label _message;

    private Button _primary;
    private Button _secondary;
    private Button _exit;

    private VisualElement _customContext; // ✅ where custom content goes

    private Action _onPrimary;
    private Action _onSecondary;
    private Action _onClose;
    public VisualElement Root => _root;

    public void Bind(VisualElement root)
    {
        _root = root;

        _title = _root.Q<Label>("title");
        _message = _root.Q<Label>("message");

        _primary = _root.Q<Button>("primaryBtn");
        _secondary = _root.Q<Button>("secondaryBtn");
        _exit = _root.Q<Button>("exit");

        _customContext = _root.Q<VisualElement>("CustomContext");

        // ✅ Stronger validation (prevents silent NRE)
        if (_title == null)
            Debug.LogError("GlobalModalController.Bind: Could not find Label 'title'.");
        if (_message == null)
            Debug.LogError("GlobalModalController.Bind: Could not find Label 'message'.");
        if (_primary == null)
            Debug.LogError("GlobalModalController.Bind: Could not find Button 'primaryBtn'.");
        if (_secondary == null)
            Debug.LogError("GlobalModalController.Bind: Could not find Button 'secondaryBtn'.");
        if (_exit == null)
            Debug.LogError("GlobalModalController.Bind: Could not find Button 'exit'.");
        if (_customContext == null)
            Debug.LogError(
                "GlobalModalController.Bind: Could not find VisualElement 'CustomContext'."
            );

        if (
            _primary == null
            || _secondary == null
            || _exit == null
            || _title == null
            || _message == null
            || _customContext == null
        )
            return;

        _primary.clicked += OnPrimary;
        _secondary.clicked += OnSecondary;
        _exit.clicked += Exit;
    }

    public void Unbind()
    {
        if (_primary != null)
            _primary.clicked -= OnPrimary;
        if (_secondary != null)
            _secondary.clicked -= OnSecondary;
        if (_exit != null)
            _exit.clicked -= Exit;

        _onPrimary = null;
        _onSecondary = null;
        _onClose = null;

        _customContext?.Clear();
        _root = null;
    }

    // -------------------------
    // SHOW (initial setup)
    // -------------------------

    public void Show(
        string title,
        string message,
        string primaryText,
        Action onPrimary,
        string secondaryText = null,
        Action onSecondary = null,
        Action onClose = null,
        bool clearCustomContent = true,
        bool centerMessage = false
    )
    {
        SetTitle(title);
        SetMessage(message);
        SetMessageAlignment(centerMessage ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);

        SetPrimary(primaryText, onPrimary);
        SetSecondary(secondaryText, onSecondary);

        _onClose = onClose;

        if (clearCustomContent)
            ClearCustomContent();
    }

    // -------------------------
    // RUNTIME UPDATES (new)
    // -------------------------

    public void SetTitle(string title)
    {
        if (_title == null)
            return;
        _title.text = title ?? "";
    }

    public void SetMessage(string message)
    {
        if (_message == null)
            return;
        _message.text = message ?? "";
    }

    public void SetMessageAlignment(TextAnchor alignment)
    {
        if (_message == null)
            return;
        _message.style.unityTextAlign = alignment;
    }

    public void SetPrimary(string text, Action onPrimary)
    {
        if (_primary == null)
            return;

        _primary.text = string.IsNullOrEmpty(text) ? "OK" : text;
        _onPrimary = onPrimary;
    }

    public void SetSecondary(string text, Action onSecondary)
    {
        if (_secondary == null)
            return;

        bool hasSecondary = !string.IsNullOrEmpty(text);
        _secondary.style.display = hasSecondary ? DisplayStyle.Flex : DisplayStyle.None;

        if (hasSecondary)
            _secondary.text = text;

        _onSecondary = onSecondary;
    }

    public void SetExitEnabled(bool enabled)
    {
        if (_exit == null)
            return;
        _exit.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // -------------------------
    // CUSTOM CONTENT (new)
    // -------------------------

    public void ClearCustomContent()
    {
        _customContext?.Clear();
    }

    /// <summary>
    /// Adds custom content under 'CustomContext'. If replace=true, clears first.
    /// </summary>
    public void SetCustomContent(VisualElement content, bool replace = true)
    {
        if (_customContext == null)
            return;

        if (replace)
            _customContext.Clear();

        if (content != null)
            _customContext.Add(content);
    }

    /// <summary>
    /// Convenience: clone a UXML template into CustomContext.
    /// </summary>
    public VisualElement SetCustomContent(VisualTreeAsset template, bool replace = true)
    {
        if (template == null)
            return null;

        var view = template.CloneTree();
        SetCustomContent(view, replace);
        return view;
    }

    // -------------------------
    // Button callbacks
    // -------------------------

    private void OnPrimary()
    {
        _onPrimary?.Invoke();
        _onClose?.Invoke();
    }

    private void OnSecondary()
    {
        _onSecondary?.Invoke();
        _onClose?.Invoke();
    }

    private void Exit()
    {
        _onClose?.Invoke();
    }
}
