using UnityEngine.UIElements;

public sealed class TalentsTabController : IDashboardTabController
{
    private VisualElement _root;
    private bool _dirty = true;

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void OnShow()
    {
        if (!_dirty)
            return;

        _dirty = false;

        // Later:
        // Apply talents data to UI
    }

    public void OnHide()
    {
        // Nothing needed
    }

    public void Unbind()
    {
        _root = null;
    }
}
