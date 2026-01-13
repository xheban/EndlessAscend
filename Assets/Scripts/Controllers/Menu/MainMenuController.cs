using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour, IScreenController
{
    private ScreenSwapper _swapper;

    private Button _newGameBtn;
    private Button _loadGameBtn;
    private Button _exitGameBtn;
    private Button _settingsBtn;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _swapper = swapper;

        _newGameBtn = screenHost.Q<Button>("NewGame");
        _loadGameBtn = screenHost.Q<Button>("LoadGame");
        _exitGameBtn = screenHost.Q<Button>("ExitGame");
        _settingsBtn = screenHost.Q<Button>("Settings");

        if (_newGameBtn == null)
            Debug.LogError("MainMenuController: Button 'NewGame' not found.");

        if (_loadGameBtn == null)
            Debug.LogError("MainMenuController: Button 'LoadGame' not found.");

        if (_exitGameBtn == null)
            Debug.LogError("MainMenuController: Button 'ExitGame' not found.");

        if (_settingsBtn == null)
            Debug.LogError("MainMenuController: Button 'Settings' not found.");

        if (_newGameBtn != null)
            _newGameBtn.clicked += OnNewGameClicked;

        if (_loadGameBtn != null)
            _loadGameBtn.clicked += OnLoadClicked;

        if (_exitGameBtn != null)
            _exitGameBtn.clicked += OnExitClicked;

        if (_settingsBtn != null)
        {
            _settingsBtn.clicked += OnSettingsClicked;

            _settingsBtn.EnableTooltip(
                _swapper,
                "This is a testing tooltip to show how long the text can go on before it will break down",
                null,
                120
            );
        }
    }

    public void Unbind()
    {
        if (_newGameBtn != null)
            _newGameBtn.clicked -= OnNewGameClicked;

        if (_loadGameBtn != null)
            _loadGameBtn.clicked -= OnLoadClicked;

        if (_exitGameBtn != null)
            _exitGameBtn.clicked -= OnExitClicked;

        if (_settingsBtn != null)
            _settingsBtn.clicked -= OnSettingsClicked;

        _newGameBtn = null;
        _loadGameBtn = null;
        _exitGameBtn = null;
        _settingsBtn = null;

        _swapper = null;
    }

    private void OnNewGameClicked()
    {
        _swapper.ShowScreen("char_creation");
    }

    private void OnLoadClicked()
    {
        _swapper.ShowOverlay("load_game");
    }

    private void OnExitClicked()
    {
        _swapper.ShowGlobalModal(
            title: "Exit",
            message: "Are you sure you want to quit?",
            primaryText: "Quit",
            onPrimary: () =>
            {
                Application.Quit();
            },
            secondaryText: "Cancel",
            onSecondary: () => { },
            closeOnOutsideClick: false
        );
    }

    private void OnSettingsClicked()
    {
        _swapper.ShowGlobalModal(
            title: "Settings",
            message: "Settings are not implemented yet.",
            primaryText: "OK",
            onPrimary: () => { },
            closeOnOutsideClick: true
        );
    }
}
