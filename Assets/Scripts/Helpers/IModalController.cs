using UnityEngine.UIElements;

public interface IModalController
{
    void Bind(VisualElement modalRoot, ScreenSwapper swapper);
    void Unbind();
}
