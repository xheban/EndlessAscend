using UnityEngine;
using UnityEngine.UIElements;

public sealed class SpellsTabController : IDashboardTabController
{
    private readonly VisualTreeAsset _spellRowTemplate;
    private readonly ScreenSwapper _swapper;

    private VisualElement _root;

    private SpellSectionController _section;

    public SpellsTabController(VisualTreeAsset spellRowTemplate, ScreenSwapper swapper)
    {
        _spellRowTemplate = spellRowTemplate;
        _swapper = swapper;
    }

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;

        _section = new SpellSectionController(_spellRowTemplate);
        _section.Bind(_root, _swapper);
    }

    public void MarkDirty() { }

    public void OnShow()
    {
        // Keep your “geometry exists” refresh behavior.
        _section?.OnTabShown();
    }

    public void OnHide()
    {
        // Nothing needed
    }

    public void Unbind()
    {
        _section?.Unbind();
        _section = null;

        _root = null;
    }
}
