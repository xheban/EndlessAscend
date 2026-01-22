using UnityEngine;
using UnityEngine.UIElements;

public class CursorController : MonoBehaviour
{
    [Header("Cursor textures (32x32 PNGs imported as Texture Type = Cursor)")]
    [SerializeField]
    private Texture2D defaultCursor;

    [SerializeField]
    private Texture2D hoverCursor;

    [Header("Hotspot (click point) in pixels")]
    [SerializeField]
    private Vector2 hotspot = Vector2.zero; // top-left by default (common for arrows)

    private bool _isHover;

    void OnEnable()
    {
        // Set default immediately
        SetDefault();

        // Hook into every active UIDocument
        RegisterAllUIDocuments();
    }

    void OnDisable()
    {
        UnregisterAllUIDocuments();
    }

    private void RegisterAllUIDocuments()
    {
        // Unity 6-friendly: finds all active UIDocuments in the scene
        var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in docs)
            RegisterDocument(doc);
    }

    private void UnregisterAllUIDocuments()
    {
        var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in docs)
            UnregisterDocument(doc);
    }

    private void RegisterDocument(UIDocument doc)
    {
        if (doc == null)
            return;
        var root = doc.rootVisualElement;
        if (root == null)
            return;

        // Capture phase so we catch it early
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave, TrickleDown.TrickleDown);
    }

    private void UnregisterDocument(UIDocument doc)
    {
        if (doc == null)
            return;
        var root = doc.rootVisualElement;
        if (root == null)
            return;

        root.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave, TrickleDown.TrickleDown);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        // evt.target is the element directly under the pointer
        var ve = evt.target as VisualElement;

        bool hoveringClickable = IsClickableOrInsideClickable(ve);

        if (hoveringClickable && !_isHover)
            SetHover();
        else if (!hoveringClickable && _isHover)
            SetDefault();
    }

    private void OnPointerLeave(PointerLeaveEvent evt)
    {
        // Pointer left the UI document area (e.g., moved off the panel)
        if (_isHover)
            SetDefault();
    }

    private bool IsClickableOrInsideClickable(VisualElement ve)
    {
        if (ve == null)
            return false;

        // Walk up the hierarchy: if you're over a Label inside a Button, it should count.
        for (var cur = ve; cur != null; cur = cur.parent)
        {
            // UI Toolkit "clickable" common cases:
            // - Button
            // - Any element with a Clickable manipulator (used by Button internally)
            if (cur is Button)
                return true;

            // If you use custom clickable elements, give them a USS class like "clickable"
            // and this will automatically work.
            if (cur.ClassListContains("clickable"))
                return true;

            // Optional: also treat elements with pickingMode Ignore as not clickable
            // (but if parent is clickable, we still return true via parent checks)
        }

        return false;
    }

    private void SetDefault()
    {
        _isHover = false;
        UnityEngine.Cursor.SetCursor(defaultCursor, hotspot, CursorMode.Auto);
    }

    private void SetHover()
    {
        _isHover = true;
        UnityEngine.Cursor.SetCursor(hoverCursor, hotspot, CursorMode.Auto);
    }
}
