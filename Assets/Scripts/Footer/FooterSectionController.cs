using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class FooterSectionController
{
    // Classes used on the BG element (CharacterBg/TowerBg/CityBg)
    private const string ActiveClass = "footer-active";
    private const string HoveringClass = "footer-hovering";

    private VisualElement _screenRoot;
    private VisualElement _footerRoot;
    private ScreenSwapper _swapper;

    // We need to unregister callbacks => store them
    private sealed class TileBinding
    {
        public string tileName;
        public string screenId;

        public VisualElement tileRoot; // "Character", "Tower", "City"

        public EventCallback<PointerDownEvent> onDown;
        public EventCallback<PointerEnterEvent> onEnter;
        public EventCallback<PointerLeaveEvent> onLeave;
    }

    private readonly List<TileBinding> _bindings = new();

    /// <summary>
    /// Bind footer tiles to screen navigation.
    /// IMPORTANT: UXML must contain a VisualElement named footerRootName (default "Footer").
    /// Inside it, tiles are VisualElements named like "Character", "Tower", "City".
    /// Each tile must contain a BG element named "<TileName>Bg" (e.g. "CharacterBg") and that BG has class "footer".
    /// </summary>
    public void Bind(
        VisualElement screenRoot,
        ScreenSwapper swapper,
        IReadOnlyDictionary<string, string> tileToScreenName,
        string footerRootName = "Footer",
        string activeTileName = null // optional: "Character" / "Tower" / "City"
    )
    {
        _screenRoot = screenRoot;
        _swapper = swapper;

        if (_screenRoot == null)
        {
            Debug.LogError("FooterSectionController.Bind: screenRoot is null.");
            return;
        }

        if (_swapper == null)
        {
            Debug.LogError("FooterSectionController.Bind: swapper is null.");
            return;
        }

        if (tileToScreenName == null)
        {
            Debug.LogError("FooterSectionController.Bind: tileToScreenName is null.");
            return;
        }

        _footerRoot = _screenRoot.Q<VisualElement>(footerRootName);
        if (_footerRoot == null)
        {
            Debug.LogError(
                $"FooterSectionController.Bind: Could not find footer root named '{footerRootName}'. "
                    + $"Add name=\"{footerRootName}\" on the footer container."
            );
            return;
        }

        ClearHandlers();

        foreach (var kv in tileToScreenName)
        {
            string tileName = kv.Key;
            string screenId = kv.Value;

            var tileRoot = _footerRoot.Q<VisualElement>(tileName);
            if (tileRoot == null)
            {
                Debug.LogWarning(
                    $"FooterSectionController: Tile '{tileName}' not found under footer root '{footerRootName}'."
                );
                continue;
            }

            // Prefer strict naming: "<TileName>Bg"
            var bg = tileRoot.Q<VisualElement>($"{tileName}");

            if (bg == null)
            {
                Debug.LogWarning(
                    $"FooterSectionController: Could not find BG for tile '{tileName}'. "
                        + $"Expected '{tileName}Bg' or an element with class 'footer' inside that tile."
                );
            }

            // IMPORTANT:
            // Do NOT set children pickingMode Ignore.
            // You WANT hover/click to also work when cursor is over image/label.
            tileRoot.pickingMode = PickingMode.Position;

            var b = new TileBinding
            {
                tileName = tileName,
                screenId = screenId,
                tileRoot = tileRoot,
            };

            // Click: navigate + set active
            b.onDown = _ =>
            {
                SetActive(tileName);
                _swapper.ShowScreen(screenId);
            };

            // Hover: add/remove class on BG (not on tile root)
            b.onEnter = _ =>
            {
                if (b != null)
                    b.tileRoot.AddToClassList(HoveringClass);
            };

            b.onLeave = _ =>
            {
                if (b.tileRoot != null)
                    b.tileRoot.RemoveFromClassList(HoveringClass);
            };

            // Register hover callbacks on the tile root:
            // PointerEnter/Leave WILL fire when entering/leaving its children as well.
            tileRoot.RegisterCallback(b.onDown);
            tileRoot.RegisterCallback(b.onEnter);
            tileRoot.RegisterCallback(b.onLeave);

            _bindings.Add(b);
        }

        // Apply initial active state (optional)
        if (!string.IsNullOrWhiteSpace(activeTileName))
            SetActive(activeTileName);
    }

    public void Unbind()
    {
        ClearHandlers();
        _footerRoot = null;
        _screenRoot = null;
        _swapper = null;
    }

    /// <summary>
    /// Adds footer-active to the BG of the selected tile and removes it from others.
    /// </summary>
    public void SetActive(string tileName)
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            var b = _bindings[i];
            if (b.tileRoot == null)
                continue;

            if (string.Equals(b.tileName, tileName, StringComparison.OrdinalIgnoreCase))
                b.tileRoot.AddToClassList(ActiveClass);
            else
                b.tileRoot.RemoveFromClassList(ActiveClass);
        }
    }

    private void ClearHandlers()
    {
        foreach (var b in _bindings)
        {
            if (b.tileRoot == null)
                continue;

            if (b.onDown != null)
                b.tileRoot.UnregisterCallback(b.onDown);
            if (b.onEnter != null)
                b.tileRoot.UnregisterCallback(b.onEnter);
            if (b.onLeave != null)
                b.tileRoot.UnregisterCallback(b.onLeave);
        }

        _bindings.Clear();
    }
}
