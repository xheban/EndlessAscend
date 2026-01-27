using UnityEngine.UIElements;

public abstract class SimpleHomesteadTabControllerBase : IHomesteadTabController
{
    protected VisualElement Root;

    private bool _dirty = true;

    protected abstract string Title { get; }

    public virtual void Bind(VisualElement tabRoot, object context)
    {
        Root = tabRoot;
        _dirty = true;
    }

    public virtual void OnShow()
    {
        if (Root == null)
            return;

        if (!_dirty)
            return;

        EnsurePlaceholder(Title);
        _dirty = false;
    }

    public virtual void OnHide() { }

    public virtual void Unbind()
    {
        Root = null;
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    protected virtual void EnsurePlaceholder(string title)
    {
        if (Root == null || Root.childCount > 0)
            return;

        Root.Add(new Label(title) { name = "PlaceholderLabel" });
    }
}
