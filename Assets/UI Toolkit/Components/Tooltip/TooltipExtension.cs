using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

public static class TooltipExtensions
{
    private sealed class TooltipHandlers
    {
        public EventCallback<PointerEnterEvent> Enter;
        public EventCallback<PointerLeaveEvent> Leave;
        public EventCallback<PointerOutEvent> Out;
    }

    private static readonly ConditionalWeakTable<VisualElement, TooltipHandlers> Handlers =
        new ConditionalWeakTable<VisualElement, TooltipHandlers>();

    public static void EnableTooltip(
        this VisualElement element,
        ScreenSwapper swapper,
        string text,
        float? offset = 8f,
        float? maxWidth = null,
        float? maxHeight = null
    )
    {
        if (element == null || swapper == null)
            return;

        DisableTooltip(element);

        var handlers = new TooltipHandlers();

        handlers.Enter = evt =>
        {
            swapper.ShowTooltipAtElement(
                (VisualElement)evt.currentTarget,
                text,
                offsetPx: offset,
                maxWidth: maxWidth,
                maxHeight: maxHeight
            );
        };

        handlers.Leave = _ =>
        {
            swapper.HideTooltip();
        };

        handlers.Out = _ =>
        {
            swapper.HideTooltip();
        };

        element.RegisterCallback(handlers.Enter);
        element.RegisterCallback(handlers.Leave);
        element.RegisterCallback(handlers.Out);
        Handlers.Add(element, handlers);
    }

    public static void DisableTooltip(this VisualElement element)
    {
        if (element == null)
            return;

        if (!Handlers.TryGetValue(element, out var handlers))
            return;

        if (handlers.Enter != null)
            element.UnregisterCallback(handlers.Enter);
        if (handlers.Leave != null)
            element.UnregisterCallback(handlers.Leave);
        if (handlers.Out != null)
            element.UnregisterCallback(handlers.Out);

        Handlers.Remove(element);
    }
}
