using UnityEngine;
using UnityEngine.UIElements;

public class CityController : MonoBehaviour, IScreenController
{
    private VisualElement _root;
    private FooterSectionController _footer;

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;

        // Optional: keep footer highlight on "City"
        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "City");
    }

    public void Unbind()
    {
        _footer?.Unbind();
        _footer = null;

        _root = null;
    }
}
