using System;
using System.Collections.Generic;
using MyGame.Run;
using UnityEngine;
using UnityEngine.UIElements;

public class TowerController : MonoBehaviour, IScreenController
{
    private static readonly string[] TowerIds =
    {
        "TowerOfBeginnings",
        "TowerOfWisdom",
        "TowerOfLife",
        "TowerOfHardship",
        "TowerOfDeath",
        "EndlessTower",
    };

    private VisualElement _root;
    private ScreenSwapper _swapper;
    private FooterSectionController _footer;

    private readonly Dictionary<string, Button> _enterButtons = new();
    private readonly Dictionary<string, VisualElement> _namePlates = new();

    public void Bind(VisualElement screenHost, ScreenSwapper swapper, object context)
    {
        _root = screenHost;
        _swapper = swapper;

        _footer = FooterBinding.BindFooter(_root, swapper, activeTileName: "Tower");

        // Bind all tower UI by naming convention:
        // Button: Enter_<towerId>
        // Name:   Name_<towerId>
        foreach (var towerId in TowerIds)
        {
            var enterBtn = _root.Q<Button>($"Enter_{towerId}");
            var namePlate = _root.Q<VisualElement>($"Name_{towerId}");

            if (enterBtn == null)
            {
                Debug.LogError($"[TowerController] Missing enter button: Enter_{towerId}");
                continue;
            }

            _enterButtons[towerId] = enterBtn;

            if (namePlate != null)
                _namePlates[towerId] = namePlate;

            // safe bind/unbind using userData like you do elsewhere
            SetButtonClick(enterBtn, () => EnterTower(towerId));
        }

        ApplyTowerLocksToUI();
    }

    public void Unbind()
    {
        foreach (var kvp in _enterButtons)
            ClearButtonClick(kvp.Value);

        _enterButtons.Clear();
        _namePlates.Clear();

        _footer?.Unbind();
        _footer = null;

        _root = null;
        _swapper = null;
    }

    private void EnterTower(string towerId)
    {
        _swapper.ShowScreen("inside_tower", new InsideTowerContext(towerId));
    }

    private void ApplyTowerLocksToUI()
    {
        // If RunSession isn't ready for some reason, safest is: only Beginnings enabled
        bool runReady = RunSession.IsInitialized;

        foreach (var towerId in TowerIds)
        {
            if (!_enterButtons.TryGetValue(towerId, out var btn) || btn == null)
                continue;

            bool unlocked = runReady
                ? RunSession.Towers.IsUnlocked(towerId)
                : (towerId == "TowerOfBeginnings");

            btn.SetEnabled(unlocked);

            // Optional: dim the nameplate too
            if (_namePlates.TryGetValue(towerId, out var plate) && plate != null)
            {
                // If you added .towerName.locked in USS, use class toggling:
                plate.EnableInClassList("locked", !unlocked);

                // If you don't want USS, you can do direct opacity:
                // plate.style.opacity = unlocked ? 1f : 0.35f;
            }
        }
    }

    // ---------- Button click helpers (same safe pattern you used) ----------

    private void SetButtonClick(Button btn, Action onClick)
    {
        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = onClick;
        btn.clicked += onClick;
    }

    private void ClearButtonClick(Button btn)
    {
        if (btn == null)
            return;

        if (btn.userData is Action oldHandler)
            btn.clicked -= oldHandler;

        btn.userData = null;
    }
}
