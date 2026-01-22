using UnityEngine.UIElements;

public sealed class CharacterTabController : IDashboardTabController
{
    private VisualElement _root;
    private bool _dirty = true;

    private CharacterSectionController _section;
    private readonly ScreenSwapper _swapper;

    public CharacterTabController(ScreenSwapper swapper)
    {
        _swapper = swapper;
    }

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;

        _section = new CharacterSectionController();
        _section.Bind(_root, _swapper);
    }

    public void MarkDirty() => _dirty = true;

    public void OnShow()
    {
        if (!_dirty)
            return;
        _dirty = false;

        // Your section already knows how to read SaveSession and refresh UI
        _section.RefreshFromSave();
        MyGame.Combat.VitalsChangedBus.Changed += OnVitalsChanged;
        _section.RefreshFromSave();
    }

    public void OnHide()
    {
        MyGame.Combat.VitalsChangedBus.Changed -= OnVitalsChanged;
    }

    public void Unbind()
    {
        _section?.Unbind();
        _section = null;
        _root = null;
    }

    private void OnVitalsChanged(int hp, int mana)
    {
        // Only update bars, not full refresh
        _section.RefreshVitalsOnly();
    }
}
