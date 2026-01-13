using UnityEngine.UIElements;

public interface IScreenController
{
    /// Called right after the UXML is cloned into screen-host.
    void Bind(VisualElement screenHost, ScreenSwapper swapper, object context);

    /// Called right before the screen-host is cleared.
    void Unbind();
}
