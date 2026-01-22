using UnityEngine.UIElements;

public sealed class SettingsGameTabController : ISettingsTabController
{
    private VisualElement _panel;

    public void Bind(VisualElement panelRoot, object context)
    {
        _panel = panelRoot;
    }

    public void OnShow() { }

    public void OnHide() { }

    public void Unbind()
    {
        _panel = null;
    }
}
