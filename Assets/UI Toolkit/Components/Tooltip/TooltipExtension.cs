using UnityEngine.UIElements;

public static class TooltipExtensions
{
    public static void EnableTooltip(
        this VisualElement element,
        ScreenSwapper swapper,
        string text,
        float? offset = 8f,
        float? maxWidth = null,
        float? maxHeight = null
    )
    {
        element.RegisterCallback<PointerEnterEvent>(evt =>
        {
            swapper.ShowTooltipAtElement(
                (VisualElement)evt.currentTarget,
                text,
                offsetPx: offset,
                maxWidth: maxWidth,
                maxHeight: maxHeight
            );
        });

        element.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            swapper.HideTooltip();
        });

        element.RegisterCallback<PointerOutEvent>(_ =>
        {
            swapper.HideTooltip();
        });
    }
}
