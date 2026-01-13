using UnityEngine;
using UnityEngine.UIElements;

public static class UIAspectRatioHelper
{
    public static void LockWidthToHeight(
        VisualElement target,
        VisualElement layoutSource,
        float widthOverHeight
    )
    {
        target.style.flexShrink = 0;

        void Apply()
        {
            float h = target.resolvedStyle.height;
            if (h <= 0.1f)
                return;

            target.style.width = h * widthOverHeight;
        }

        // Recalculate when layout changes
        layoutSource.RegisterCallback<GeometryChangedEvent>(_ => Apply());

        // Initial attempt
        Apply();
    }
}
