using UnityEngine.UIElements;

public interface IOverlayController
{
    void Bind(VisualElement overlayRoot, ScreenSwapper swapper, object context = null);
    void Unbind();
}
