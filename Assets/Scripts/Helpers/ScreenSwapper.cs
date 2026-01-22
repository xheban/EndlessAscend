using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ScreenSwapper : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private UIDocument uiDocument;

    [Serializable]
    public class ScreenEntry
    {
        public string screenId; // e.g. "MainMenu", "CharacterCreation"
        public VisualTreeAsset uxml; // screen UXML
        public MonoBehaviour[] controllers; // must implement IScreenController (optional)
    }

    [Header("Screens")]
    [SerializeField]
    private List<ScreenEntry> screens = new();

    [Serializable]
    public class OverlayEntry
    {
        public string overlayId; // e.g. "Shop", "Inventory"
        public VisualTreeAsset uxml;
        public MonoBehaviour[] controllers; // must implement IOverlayController (optional)

        public bool showBackdrop = true;
        public bool closeOnBackdropClick = false;

        // NEW: sometimes overlays should NOT block clicking the screen
        public bool blockScreenInput = true;
    }

    [Header("Overlays (multi-open)")]
    [SerializeField]
    private List<OverlayEntry> overlays = new();

    [Header("Start Screen")]
    [SerializeField]
    private string startScreenId = "MainMenu";

    private VisualElement _root;
    private VisualElement _screenHost;

    private VisualElement _overlayHost;
    private VisualElement _overlayBackdrop;
    private VisualElement _overlayContent;

    public event Action<string> ScreenChanged;

    private readonly List<IScreenController> _activeScreenControllers = new();
    private MonoBehaviour[] _activeScreenControllerMbs;

    // ---------- MULTI OVERLAY RUNTIME ----------
    private class OverlayRuntime
    {
        public string id;
        public VisualElement layer; // container for this overlay instance
        public OverlayEntry entry;

        public readonly List<IOverlayController> ctrls = new();
        public MonoBehaviour[] controllerMbs;
    }

    // One open overlay per id (prevents duplicates)
    private readonly Dictionary<string, OverlayRuntime> _openOverlays = new();

    // Visual order: top-most is last
    private readonly List<OverlayRuntime> _overlayOrder = new();

    [SerializeField]
    private VisualTreeAsset globalModalUxml;
    private VisualElement _modalHost;
    private VisualElement _modalBackdrop;
    private VisualElement _modalContent;

    private readonly GlobalModalController _globalModal = new();

    private bool _closeModalOnOutsideClick;
    private bool _globalModalOpen;

    //-------TOOLTIP PART----------------------
    [SerializeField]
    private VisualTreeAsset tooltipUxml;

    [SerializeField]
    private VisualTreeAsset inventoryDetailTooltipUxml;
    private VisualElement _tooltipHost;
    private VisualElement _tooltipContent;
    private VisualElement _customTooltipHost;
    private VisualElement _activeCustomTooltip;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        _root = uiDocument.rootVisualElement;

        _screenHost = _root.Q<VisualElement>("screen-host");
        if (_screenHost == null)
        {
            Debug.LogError("Could not find VisualElement named 'screen-host' in AppRoot.uxml.");
            return;
        }

        _overlayHost = _root.Q<VisualElement>("overlay-host");
        if (_overlayHost == null)
        {
            Debug.LogError("Could not find VisualElement named 'overlay-host' in AppRoot.uxml.");
            return;
        }

        _overlayBackdrop = _overlayHost.Q<VisualElement>("overlay-backdrop");
        _overlayContent = _overlayHost.Q<VisualElement>("overlay-content");
        if (_overlayBackdrop == null || _overlayContent == null)
        {
            Debug.LogError(
                "overlay-host must contain 'overlay-backdrop' and 'overlay-content' VisualElements."
            );
            return;
        }
        _modalHost = _root.Q<VisualElement>("modal-host");
        _modalBackdrop = _modalHost?.Q<VisualElement>("modal-backdrop");
        _modalContent = _modalHost?.Q<VisualElement>("modal-content");

        if (_modalHost == null || _modalBackdrop == null || _modalContent == null)
        {
            Debug.LogError("modal-host must contain 'modal-backdrop' and 'modal-content'.");
            return;
        }

        _tooltipHost = _root.Q<VisualElement>("tooltip-host");
        _tooltipContent = _tooltipHost?.Q<VisualElement>("tooltip-content");

        if (_tooltipHost == null || _tooltipContent == null)
        {
            Debug.LogError("tooltip-host must contain 'tooltip-content'.");
            return;
        }

        _tooltipContent.Clear();
        if (tooltipUxml == null)
        {
            Debug.LogError("Tooltip UXML is not assigned in ScreenSwapper.");
            return;
        }

        tooltipUxml.CloneTree(_tooltipContent);

        // Dedicated host for custom tooltip visuals (inventory detail, etc.)
        _customTooltipHost = new VisualElement { name = "custom-tooltip-host" };
        _customTooltipHost.pickingMode = PickingMode.Ignore;
        _customTooltipHost.style.position = Position.Absolute;
        _customTooltipHost.style.left = 0;
        _customTooltipHost.style.top = 0;
        _customTooltipHost.style.right = 0;
        _customTooltipHost.style.bottom = 0;
        _tooltipContent.Add(_customTooltipHost);

        // Optional: create the rich inventory detail tooltip in the global host.
        // This avoids keeping an extra instance inside Inventory.uxml.
        if (inventoryDetailTooltipUxml != null)
        {
            var temp = new VisualElement();
            inventoryDetailTooltipUxml.CloneTree(temp);

            // InventoryDetail.uxml has a single root VisualElement.
            if (temp.childCount > 0)
            {
                var detail = temp[0];
                detail.RemoveFromHierarchy();
                detail.name = "InventoryDetailTooltip";
                detail.pickingMode = PickingMode.Ignore;
                detail.style.display = DisplayStyle.None;
                _customTooltipHost.Add(detail);
            }
        }

        // Build modal UI once into modal-body (no extra layer)
        _modalContent.Clear();
        globalModalUxml.CloneTree(_modalContent);
        _globalModal.Bind(_modalContent);

        // Outside click close (same logic as overlays)
        _modalContent.RegisterCallback<PointerDownEvent>(
            OnModalBodyPointerDown,
            TrickleDown.TrickleDown
        );

        SetGlobalModalOpen(false);

        // Disable all controllers initially
        foreach (var s in screens)
        {
            if (s.controllers == null)
                continue;
            foreach (var c in s.controllers)
                if (c != null)
                    c.enabled = false;
        }

        foreach (var o in overlays)
        {
            if (o.controllers == null)
                continue;
            foreach (var c in o.controllers)
                if (c != null)
                    c.enabled = false;
        }

        RefreshOverlayState();
        ShowScreen(startScreenId);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            const string settingsId = "settings";
            if (IsOverlayOpen(settingsId))
                CloseOverlay(settingsId);
            else
                ShowOverlay(settingsId);
        }
    }

    public void ShowScreen(string screenId, object context)
    {
        ShowScreenInternal(screenId, context);
    }

    public void ShowScreen(string screenId)
    {
        ShowScreenInternal(screenId, context: null);
    }

    // ---------------- SCREENS ----------------

    private void ShowScreenInternal(string screenId, object context)
    {
        var next = screens.Find(s => s.screenId == screenId);
        if (next == null)
        {
            Debug.LogError($"Screen '{screenId}' not found in ScreenSwapper.screens.");
            return;
        }

        CloseAllOverlays();
        CloseGlobalModal();

        // Unbind + disable old controllers
        if (_activeScreenControllerMbs != null)
        {
            foreach (var ctrl in _activeScreenControllers)
                ctrl.Unbind();

            foreach (var mb in _activeScreenControllerMbs)
                if (mb != null)
                    mb.enabled = false;
        }

        _activeScreenControllers.Clear();

        // Load new UXML
        _screenHost.Clear();
        if (next.uxml == null)
        {
            Debug.LogError($"Screen '{screenId}' has no UXML assigned.");
            return;
        }
        next.uxml.CloneTree(_screenHost);

        // Enable + bind new controllers
        _activeScreenControllerMbs = next.controllers;

        if (_activeScreenControllerMbs != null)
        {
            foreach (var mb in _activeScreenControllerMbs)
            {
                if (mb == null)
                    continue;

                mb.enabled = true;

                if (mb is IScreenController screenCtrl)
                {
                    _activeScreenControllers.Add(screenCtrl);

                    // ✅ NEW: pass context
                    screenCtrl.Bind(_screenHost, this, context);
                }
                else
                {
                    Debug.LogError(
                        $"Controller '{mb.GetType().Name}' on screen '{screenId}' does not implement IScreenController."
                    );
                }
            }
        }

        ScreenChanged?.Invoke(screenId);
    }

    // ---------------- OVERLAYS (MULTI) ----------------

    public void ShowOverlay(string overlayId, object context)
    {
        ShowOverlayInternal(overlayId, context);
    }

    public void ShowOverlay(string overlayId)
    {
        ShowOverlayInternal(overlayId, context: null);
    }

    public void ShowOverlayInternal(string overlayId, object context)
    {
        var entry = overlays.Find(o => o.overlayId == overlayId);
        if (entry == null)
        {
            Debug.LogError($"Overlay '{overlayId}' not found in ScreenSwapper.overlays.");
            return;
        }

        // If already open, just bring to front
        if (_openOverlays.TryGetValue(overlayId, out var existing))
        {
            BringOverlayToFront(existing);
            RefreshOverlayState();
            return;
        }

        // Create a full-screen layer for this overlay (simple + consistent)
        var layer = new VisualElement { name = $"overlay-{overlayId}" };
        layer.style.position = Position.Absolute;
        layer.style.left = 0;
        layer.style.top = 0;
        layer.style.right = 0;
        layer.style.bottom = 0;
        layer.style.display = DisplayStyle.Flex;
        layer.style.justifyContent = Justify.Center;
        layer.style.alignItems = Align.Center;
        layer.pickingMode = PickingMode.Position;

        layer.RegisterCallback<PointerDownEvent>(
            evt =>
            {
                // Only close if click landed on the layer itself (empty area),
                // not on any child control (buttons/panels/etc).
                if (evt.target == layer && entry.closeOnBackdropClick)
                {
                    evt.StopImmediatePropagation();
                    CloseOverlay(overlayId);
                }
            },
            TrickleDown.TrickleDown
        );

        _overlayContent.Add(layer);

        // Clone overlay UI into layer
        if (entry.uxml == null)
        {
            Debug.LogError($"Overlay '{overlayId}' has no UXML assigned.");
            layer.RemoveFromHierarchy();
            return;
        }
        entry.uxml.CloneTree(layer);

        var runtime = new OverlayRuntime
        {
            id = overlayId,
            layer = layer,
            entry = entry,
            controllerMbs = entry.controllers,
        };

        // Enable + bind controllers (bind to THIS overlay layer)
        if (runtime.controllerMbs != null)
        {
            foreach (var mb in runtime.controllerMbs)
            {
                if (mb == null)
                    continue;

                mb.enabled = true;

                if (mb is IOverlayController overlayCtrl)
                {
                    runtime.ctrls.Add(overlayCtrl);
                    overlayCtrl.Bind(layer, this, context);
                }
                else
                {
                    Debug.LogError(
                        $"Overlay controller '{mb.GetType().Name}' on overlay '{overlayId}' does not implement IOverlayController."
                    );
                }
            }
        }

        _openOverlays.Add(overlayId, runtime);
        _overlayOrder.Add(runtime);

        BringOverlayToFront(runtime);
        RefreshOverlayState();
    }

    public void CloseOverlay() // closes top-most overlay
    {
        if (_overlayOrder.Count == 0)
            return;

        CloseOverlay(_overlayOrder[^1].id);
    }

    public void CloseOverlay(string overlayId) // closes overlay by id
    {
        if (!_openOverlays.TryGetValue(overlayId, out var rt))
            return;

        foreach (var ctrl in rt.ctrls)
            ctrl.Unbind();

        if (rt.controllerMbs != null)
        {
            foreach (var mb in rt.controllerMbs)
                if (mb != null)
                    mb.enabled = false;
        }

        rt.layer.RemoveFromHierarchy();

        _openOverlays.Remove(overlayId);
        _overlayOrder.Remove(rt);

        RefreshOverlayState();
    }

    public bool IsOverlayOpen(string overlayId) => _openOverlays.ContainsKey(overlayId);

    private void BringOverlayToFront(OverlayRuntime rt)
    {
        // Update order list (top-most is last)
        _overlayOrder.Remove(rt);
        _overlayOrder.Add(rt);

        // THIS is the correct way in UI Toolkit
        rt.layer.BringToFront();
    }

    private void RefreshOverlayState()
    {
        bool anyOpen = _overlayOrder.Count > 0;

        // Overlay host visible if any overlay open
        _overlayHost.style.display = anyOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (!anyOpen)
        {
            _screenHost.pickingMode = PickingMode.Position;

            _overlayBackdrop.style.display = DisplayStyle.None;
            _overlayBackdrop.pickingMode = PickingMode.Ignore;
            return;
        }

        // Screen blocking: if ANY open overlay wants to block, block.
        bool shouldBlockScreen = false;
        for (int i = 0; i < _overlayOrder.Count; i++)
        {
            if (_overlayOrder[i].entry.blockScreenInput)
            {
                shouldBlockScreen = true;
                break;
            }
        }
        _screenHost.pickingMode = shouldBlockScreen ? PickingMode.Ignore : PickingMode.Position;

        // Backdrop decided by TOP overlay
        var top = _overlayOrder[^1];
        bool showBackdrop = top.entry.showBackdrop;

        _overlayBackdrop.style.display = showBackdrop ? DisplayStyle.Flex : DisplayStyle.None;
        _overlayBackdrop.pickingMode = showBackdrop ? PickingMode.Position : PickingMode.Ignore;
    }

    public GlobalModalController ShowGlobalModal(
        string title,
        string message,
        string primaryText,
        Action onPrimary,
        string secondaryText = null,
        Action onSecondary = null,
        bool closeOnOutsideClick = false,
        VisualElement customContent = null,
        bool replaceCustomContent = true
    )
    {
        SetGlobalModalOpen(true);
        _closeModalOnOutsideClick = closeOnOutsideClick;

        _globalModal.Show(
            title,
            message,
            primaryText,
            onPrimary,
            secondaryText,
            onSecondary,
            onClose: () => SetGlobalModalOpen(false)
        );

        if (customContent != null)
            _globalModal.SetCustomContent(customContent, replace: replaceCustomContent);
        else
            _globalModal.ClearCustomContent();

        _modalContent.focusable = true;
        _modalContent.tabIndex = 0;
        _modalContent.Focus();

        RevealModalAfterLayoutStable();

        return _globalModal; // ✅ handle for runtime updates
    }

    public void CloseGlobalModal()
    {
        if (!_globalModalOpen)
            return;
        SetGlobalModalOpen(false);
    }

    private void SetGlobalModalOpen(bool open)
    {
        _globalModalOpen = open;

        _modalHost.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        _modalBackdrop.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

        // Block everything under the modal
        _screenHost.pickingMode = open ? PickingMode.Ignore : PickingMode.Position;

        // If you have multi-overlays:
        _overlayHost.pickingMode = open ? PickingMode.Ignore : PickingMode.Position;
    }

    private void OnModalBodyPointerDown(PointerDownEvent evt)
    {
        if (!_globalModalOpen)
            return;
        if (!_closeModalOnOutsideClick)
            return;

        // Close only if click landed on empty body area, not on the modal panel/buttons
        if (evt.target == _modalContent)
        {
            evt.StopImmediatePropagation();
            CloseGlobalModal();
        }
    }

    private void RevealModalAfterLayoutStable()
    {
        // keep modal present but invisible
        _modalHost.style.visibility = Visibility.Hidden;
        _modalHost.style.opacity = 0f;

        // Tick 1: style resolve + first layout

        _modalHost
            .schedule.Execute(() =>
            {
                _modalHost.style.visibility = Visibility.Visible;
                _modalHost.style.opacity = 1f;
            })
            .StartingIn(100);
    }

    public void CloseAllOverlays()
    {
        // Close top-most first (safe order)
        for (int i = _overlayOrder.Count - 1; i >= 0; i--)
        {
            CloseOverlay(_overlayOrder[i].id);
        }
    }

    public void ShowTooltip(string text)
    {
        if (_tooltipContent == null)
            return;

        var body = _tooltipContent.Q<VisualElement>("Body");
        var label = _tooltipContent.Q<Label>("Text");

        if (body == null || label == null)
            return;

        label.text = text ?? string.Empty;
        body.style.display = DisplayStyle.Flex;
    }

    public void HideTooltip()
    {
        if (_tooltipContent == null)
            return;

        var body = _tooltipContent.Q<VisualElement>("Body");
        if (body == null)
            return;

        body.style.display = DisplayStyle.None;
    }

    public void ShowCustomTooltipAtWorldPosition(
        VisualElement tooltip,
        Vector2 worldPos,
        float offsetPx = 8f,
        float edgePaddingPx = 8f,
        float fallbackWidthPx = 360f,
        float fallbackHeightPx = 180f
    )
    {
        if (
            tooltip == null
            || _tooltipContent == null
            || _customTooltipHost == null
            || _root == null
        )
            return;

        // Hide the standard text tooltip if it's up.
        HideTooltip();

        _activeCustomTooltip = tooltip;

        // Reparent into the global tooltip overlay host.
        if (tooltip.parent != _customTooltipHost)
        {
            tooltip.RemoveFromHierarchy();
            _customTooltipHost.Add(tooltip);
        }

        tooltip.style.display = DisplayStyle.Flex;
        tooltip.style.position = Position.Absolute;
        tooltip.BringToFront();

        // Delay positioning until layout resolves.
        tooltip.schedule.Execute(() =>
        {
            PositionCustomTooltipAtWorldPosition(
                tooltip,
                worldPos,
                offsetPx,
                edgePaddingPx,
                fallbackWidthPx,
                fallbackHeightPx
            );
        });
    }

    public void PositionCustomTooltipAtWorldPosition(
        VisualElement tooltip,
        Vector2 worldPos,
        float offsetPx = 8f,
        float edgePaddingPx = 8f,
        float fallbackWidthPx = 360f,
        float fallbackHeightPx = 180f
    )
    {
        if (tooltip == null || _root == null)
            return;

        var local = _root.WorldToLocal(worldPos);

        float w = tooltip.resolvedStyle.width;
        float h = tooltip.resolvedStyle.height;
        if (w <= 1f)
            w = fallbackWidthPx;
        if (h <= 1f)
            h = fallbackHeightPx;

        float panelW = _root.resolvedStyle.width;
        float panelH = _root.resolvedStyle.height;

        float x = local.x + offsetPx;
        float y = local.y + offsetPx;

        // Prefer flipping left/up if it would overflow.
        if (panelW > 0 && x + w + edgePaddingPx > panelW)
            x = local.x - w - offsetPx;
        if (panelH > 0 && y + h + edgePaddingPx > panelH)
            y = local.y - h - offsetPx;

        if (panelW > 0)
            x = Mathf.Clamp(x, edgePaddingPx, Mathf.Max(edgePaddingPx, panelW - w - edgePaddingPx));
        if (panelH > 0)
            y = Mathf.Clamp(y, edgePaddingPx, Mathf.Max(edgePaddingPx, panelH - h - edgePaddingPx));

        tooltip.style.left = x;
        tooltip.style.top = y;
    }

    public void ShowCustomTooltipAboveWorldPosition(
        VisualElement tooltip,
        Vector2 worldPos,
        float offsetPx = 8f,
        float edgePaddingPx = 8f,
        float fallbackWidthPx = 360f,
        float fallbackHeightPx = 180f
    )
    {
        if (
            tooltip == null
            || _tooltipContent == null
            || _customTooltipHost == null
            || _root == null
        )
            return;

        HideTooltip();
        _activeCustomTooltip = tooltip;

        if (tooltip.parent != _customTooltipHost)
        {
            tooltip.RemoveFromHierarchy();
            _customTooltipHost.Add(tooltip);
        }

        tooltip.style.display = DisplayStyle.Flex;
        tooltip.style.position = Position.Absolute;
        tooltip.BringToFront();

        tooltip.schedule.Execute(() =>
        {
            PositionCustomTooltipAboveWorldPosition(
                tooltip,
                worldPos,
                offsetPx,
                edgePaddingPx,
                fallbackWidthPx,
                fallbackHeightPx
            );
        });
    }

    public void PositionCustomTooltipAboveWorldPosition(
        VisualElement tooltip,
        Vector2 worldPos,
        float offsetPx = 8f,
        float edgePaddingPx = 8f,
        float fallbackWidthPx = 360f,
        float fallbackHeightPx = 180f
    )
    {
        if (tooltip == null || _root == null)
            return;

        var local = _root.WorldToLocal(worldPos);

        float w = tooltip.resolvedStyle.width;
        float h = tooltip.resolvedStyle.height;
        if (w <= 1f)
            w = fallbackWidthPx;
        if (h <= 1f)
            h = fallbackHeightPx;

        float panelW = _root.resolvedStyle.width;
        float panelH = _root.resolvedStyle.height;

        // Center horizontally on the anchor, and place above it.
        float x = local.x - (w * 0.5f);
        float y = local.y - h - offsetPx;

        // If there's no room above, place below.
        if (panelH > 0 && y < edgePaddingPx)
            y = local.y + offsetPx;

        if (panelW > 0)
            x = Mathf.Clamp(x, edgePaddingPx, Mathf.Max(edgePaddingPx, panelW - w - edgePaddingPx));
        if (panelH > 0)
            y = Mathf.Clamp(y, edgePaddingPx, Mathf.Max(edgePaddingPx, panelH - h - edgePaddingPx));

        tooltip.style.left = x;
        tooltip.style.top = y;
    }

    public void HideCustomTooltip(VisualElement tooltip = null)
    {
        if (_customTooltipHost == null)
            return;

        var t = tooltip ?? _activeCustomTooltip;
        if (t == null)
            return;

        t.style.display = DisplayStyle.None;

        if (ReferenceEquals(t, _activeCustomTooltip))
            _activeCustomTooltip = null;
    }

    public VisualElement GetCustomTooltipElement(string name)
    {
        if (_customTooltipHost == null || string.IsNullOrWhiteSpace(name))
            return null;

        return _customTooltipHost.Q<VisualElement>(name);
    }

    public void ShowTooltipAtElement(
        VisualElement anchor,
        string text,
        float? offsetPx = null,
        float? maxWidth = null,
        float? maxHeight = null
    )
    {
        if (anchor == null || _tooltipContent == null)
            return;

        var body = _tooltipContent.Q<VisualElement>("Body");
        var label = _tooltipContent.Q<Label>("Text");
        float _offset = 8f;

        if (body == null || label == null)
            return;

        if (maxWidth.HasValue)
            body.style.maxWidth = maxWidth.Value;
        else
            body.style.maxWidth = StyleKeyword.None;

        if (maxHeight.HasValue)
            body.style.maxHeight = maxHeight.Value;
        else
            body.style.maxHeight = StyleKeyword.None;

        if (offsetPx.HasValue)
            _offset = offsetPx.Value;

        // Optional: clip overflow if height is constrained
        body.style.overflow = maxHeight.HasValue ? Overflow.Hidden : Overflow.Visible;

        // Set text first so layout can calculate size
        label.text = text ?? string.Empty;
        body.style.display = DisplayStyle.Flex;

        // Delay positioning until layout is resolved (important!)
        body.schedule.Execute(() =>
        {
            PositionTooltip(anchor, body, _offset);
        });
    }

    private void PositionTooltip(VisualElement anchor, VisualElement tooltip, float offsetPx)
    {
        // Anchor bounds in panel space
        Rect anchorRect = anchor.worldBound;

        // Tooltip size (already measured because of schedule)
        float tooltipW = tooltip.resolvedStyle.width;
        float tooltipH = tooltip.resolvedStyle.height;

        // Panel size
        var panel = _root.panel;
        float panelW = panel.visualTree.resolvedStyle.width;
        float panelH = panel.visualTree.resolvedStyle.height;

        // Preferred positions (in order)
        Vector2 top = new(
            anchorRect.center.x - tooltipW * 0.5f,
            anchorRect.yMin - tooltipH - offsetPx
        );

        Vector2 bottom = new(anchorRect.center.x - tooltipW * 0.5f, anchorRect.yMax + offsetPx);

        Vector2 right = new(anchorRect.xMax + offsetPx, anchorRect.center.y - tooltipH * 0.5f);

        Vector2 left = new(
            anchorRect.xMin - tooltipW - offsetPx,
            anchorRect.center.y - tooltipH * 0.5f
        );

        // Choose first position that fits
        Vector2 finalPos =
            Fits(top, tooltipW, tooltipH, panelW, panelH) ? top
            : Fits(bottom, tooltipW, tooltipH, panelW, panelH) ? bottom
            : Fits(right, tooltipW, tooltipH, panelW, panelH) ? right
            : Fits(left, tooltipW, tooltipH, panelW, panelH) ? left
            : ClampToPanel(top, tooltipW, tooltipH, panelW, panelH);

        tooltip.style.left = finalPos.x;
        tooltip.style.top = finalPos.y;
    }

    private static bool Fits(Vector2 pos, float w, float h, float panelW, float panelH)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x + w <= panelW && pos.y + h <= panelH;
    }

    private static Vector2 ClampToPanel(Vector2 pos, float w, float h, float panelW, float panelH)
    {
        float x = Mathf.Clamp(pos.x, 0, panelW - w);
        float y = Mathf.Clamp(pos.y, 0, panelH - h);
        return new Vector2(x, y);
    }

    // Convenience public method to open the Settings overlay from code.
    public void OpenSettingsOverlay() => ShowOverlay("Settings");
}
