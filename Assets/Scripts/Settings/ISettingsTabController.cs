using UnityEngine.UIElements;

public interface ISettingsTabController
{
    void Bind(VisualElement panelRoot, object context);
    void OnShow();
    void OnHide();
    void Unbind();
}
