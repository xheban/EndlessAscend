using System;
using UnityEngine;
using UnityEngine.UIElements;

public class GlobalModalController
{
    private VisualElement _root;
    private Label _title;
    private Label _message;
    private Button _primary;
    private Button _secondary;
    private Button _exit;

    private Action _onPrimary;
    private Action _onSecondary;
    private Action _onClose;

    public void Bind(VisualElement root)
    {
        _root = root;
        _title = _root.Q<Label>("title");
        _message = _root.Q<Label>("message");
        _primary = _root.Q<Button>("primaryBtn");
        _secondary = _root.Q<Button>("secondaryBtn");
        _exit = _root.Q<Button>("exit");

        if (_exit == null)
        {
            Debug.LogError("Could not find exit button.");
            return;
        }

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
    }

    public void Show(
        string title,
        string message,
        string primaryText,
        Action onPrimary,
        string secondaryText = null,
        Action onSecondary = null,
        Action onClose = null
    )
    {
        _title.text = title ?? "";
        _message.text = message ?? "";

        _primary.text = string.IsNullOrEmpty(primaryText) ? "OK" : primaryText;
        _onPrimary = onPrimary;

        bool hasSecondary = !string.IsNullOrEmpty(secondaryText);
        _secondary.style.display = hasSecondary ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasSecondary)
            _secondary.text = secondaryText;

        _onSecondary = onSecondary;
        _onClose = onClose;
    }

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
