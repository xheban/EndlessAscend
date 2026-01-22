using UnityEngine.UIElements;

public interface IDashboardTabController
{
    /// <summary>Called once when the tab VisualElement is created and inserted.</summary>
    void Bind(VisualElement tabRoot, object context);

    /// <summary>Called every time the tab becomes active/visible.</summary>
    void OnShow();

    /// <summary>Called every time the tab is switched away from.</summary>
    void OnHide();

    /// <summary>Called when dashboard is closing/unbinding.</summary>
    void Unbind();

    /// <summary>Marks the tab as needing a refresh on next show.</summary>
    void MarkDirty();
}
