// File: Assets/Scripts/Combat/CombatKeybindInputController.cs
// Unity 6
// Responsibility: Cast spells using keybindings (slotIndex -> KeyCode) while in combat.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.Combat
{
    public sealed class CombatKeybindInputController : MonoBehaviour
    {
        [Header("Runtime references (set by your combat screen/controller)")]
        public CombatEngine Engine;

        // We only need the SpellSlots array (userData carries spellId).
        // You can assign this from your combat controller after CombatTowerView.Bind().
        public VisualElement[] SpellSlots;

        // Combat item slots (index 0..3)
        public VisualElement[] ItemSlots;

        // Cached key -> slot mapping
        private readonly Dictionary<KeyCode, int> _keyToSpellSlot = new();
        private readonly Dictionary<KeyCode, int> _keyToItemSlot = new();

        // Detect changes in PlayerPrefs without scanning every frame
        private int _prefsVersion;

        private void OnEnable()
        {
            KeybindPrefs.EnsureSpellDefaults();
            KeybindPrefs.EnsureItemDefaults();
            RebuildBindingsCache(force: true);
        }

        private void Update()
        {
            if (Engine == null || Engine.State == null || Engine.State.isFinished)
                return;

            if (!Engine.State.waitingForPlayerInput)
                return;

            bool hasSpellSlots = SpellSlots != null && SpellSlots.Length > 0;
            bool hasItemSlots = ItemSlots != null && ItemSlots.Length > 0;
            if (!hasSpellSlots && !hasItemSlots)
                return;

            // Rebuild if prefs changed
            if (KeybindPrefs.Version != _prefsVersion)
                RebuildBindingsCache(force: true);

            if (_keyToSpellSlot.Count == 0 && _keyToItemSlot.Count == 0)
                return;

            // Spells first
            if (hasSpellSlots)
            {
                foreach (var kv in _keyToSpellSlot)
                {
                    var key = kv.Key;
                    if (!Input.GetKeyDown(key))
                        continue;

                    int slotIndex = kv.Value;
                    if (slotIndex < 0 || slotIndex >= SpellSlots.Length)
                        continue;

                    var slotRoot = SpellSlots[slotIndex];
                    if (slotRoot == null)
                        continue;

                    string spellId = slotRoot.userData as string;
                    if (string.IsNullOrWhiteSpace(spellId))
                        return;

                    Engine.TryUseSpell(spellId);
                    return;
                }
            }

            // Items
            if (hasItemSlots)
            {
                foreach (var kv in _keyToItemSlot)
                {
                    var key = kv.Key;
                    if (!Input.GetKeyDown(key))
                        continue;

                    int slotIndex = kv.Value;
                    if (slotIndex < 0 || slotIndex >= ItemSlots.Length)
                        continue;

                    Engine.TryUseActiveCombatItemSlot(slotIndex);
                    return;
                }
            }
        }

        public void NotifyBindingsChanged()
        {
            RebuildBindingsCache(force: true);
        }

        private void RebuildBindingsCache(bool force)
        {
            if (!force)
                return;

            _prefsVersion = KeybindPrefs.Version;

            _keyToSpellSlot.Clear();
            _keyToItemSlot.Clear();

            for (int slotIndex = 0; slotIndex < KeybindPrefs.SpellSlotCount; slotIndex++)
            {
                var key = KeybindPrefs.GetSpellSlotKey(slotIndex);
                if (key == KeyCode.None)
                    continue;

                // If duplicate keys exist, last one wins
                _keyToSpellSlot[key] = slotIndex;
            }

            for (int slotIndex = 0; slotIndex < KeybindPrefs.ItemSlotCount; slotIndex++)
            {
                var key = KeybindPrefs.GetItemSlotKey(slotIndex);
                if (key == KeyCode.None)
                    continue;

                // If duplicate keys exist, last one wins
                _keyToItemSlot[key] = slotIndex;
            }
        }
    }
}
