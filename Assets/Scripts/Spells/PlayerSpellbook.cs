using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Spells
{
    public sealed class PlayerSpellbook
    {
        public const int ActiveSlotCount = 12;

        private readonly Dictionary<string, PlayerSpellEntry> _entries =
            new Dictionary<string, PlayerSpellEntry>();

        private readonly SpellDatabase _db;
        private readonly SpellProgressionConfig _progression;

        public PlayerSpellbook(SpellDatabase db, SpellProgressionConfig progression)
        {
            _db = db;
            _progression = progression;
        }

        public IReadOnlyDictionary<string, PlayerSpellEntry> Entries => _entries;

        public bool HasSpell(string spellId) => _entries.ContainsKey(spellId);

        public PlayerSpellEntry Get(string spellId) =>
            _entries.TryGetValue(spellId, out var e) ? e : null;

        public void AddOrReplace(PlayerSpellEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.spellId))
                return;

            // Clamp active slot to valid range or -1
            if (entry.activeSlotIndex < -1 || entry.activeSlotIndex >= ActiveSlotCount)
                entry.activeSlotIndex = -1;

            _entries[entry.spellId] = entry;

            // Ensure no duplicate slot occupancy
            EnforceUniqueActiveSlots();
        }

        public void UnlockIfMissing(string spellId, int startLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(spellId))
                return;

            if (HasSpell(spellId))
                return;

            AddOrReplace(new PlayerSpellEntry(spellId, startLevel));
        }

        // ------------------------
        // Active slot functionality
        // ------------------------

        /// <summary>
        /// Activates a spell in a given slot. If another spell is in that slot, it gets deactivated.
        /// If the spell was active in another slot, it moves.
        /// Returns true if successful.
        /// </summary>
        public bool SetActive(string spellId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ActiveSlotCount)
                return false;

            var entry = Get(spellId);
            if (entry == null)
                return false;

            // Deactivate any spell currently in that slot
            foreach (var e in _entries.Values)
            {
                if (e.activeSlotIndex == slotIndex && e.spellId != spellId)
                    e.activeSlotIndex = -1;
            }

            // Assign spell to the slot
            entry.activeSlotIndex = slotIndex;

            // Ensure no duplicates (just in case)
            EnforceUniqueActiveSlots();

            return true;
        }

        public bool Deactivate(string spellId)
        {
            var entry = Get(spellId);
            if (entry == null)
                return false;

            entry.activeSlotIndex = -1;
            return true;
        }

        public void ClearAllActive()
        {
            foreach (var e in _entries.Values)
                e.activeSlotIndex = -1;
        }

        /// <summary>
        /// Returns active spells in slot order (0..ActiveSlotCount-1). Missing slots are skipped.
        /// </summary>
        public List<PlayerSpellEntry> GetActiveSpellsInOrder()
        {
            var result = new List<PlayerSpellEntry>(ActiveSlotCount);

            for (int slot = 0; slot < ActiveSlotCount; slot++)
            {
                var e = GetSpellInSlot(slot);
                if (e != null)
                    result.Add(e);
            }

            return result;
        }

        public PlayerSpellEntry GetSpellInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ActiveSlotCount)
                return null;

            foreach (var e in _entries.Values)
            {
                if (e.activeSlotIndex == slotIndex)
                    return e;
            }

            return null;
        }

        private void EnforceUniqueActiveSlots()
        {
            // If multiple spells claim same slot (can happen from bad saves), keep the first and deactivate others.
            var occupied = new Dictionary<int, string>();

            foreach (var e in _entries.Values)
            {
                if (e.activeSlotIndex < 0)
                    continue;

                if (e.activeSlotIndex >= ActiveSlotCount)
                {
                    e.activeSlotIndex = -1;
                    continue;
                }

                if (!occupied.ContainsKey(e.activeSlotIndex))
                {
                    occupied[e.activeSlotIndex] = e.spellId;
                }
                else
                {
                    // Slot already taken -> deactivate this one
                    e.activeSlotIndex = -1;
                }
            }
        }

        // ------------------------
        // Cooldowns / XP (existing)
        // ------------------------

        public void TickCooldowns()
        {
            foreach (var e in _entries.Values)
            {
                if (e.cooldownRemainingTurns > 0)
                    e.cooldownRemainingTurns--;
            }
        }

        public bool TickCooldown(string spellId, int amount = 1)
        {
            if (amount <= 0)
                return false;

            var e = Get(spellId);
            if (e == null)
                return false;

            if (e.cooldownRemainingTurns <= 0)
                return false;

            e.cooldownRemainingTurns = Mathf.Max(0, e.cooldownRemainingTurns - amount);
            return true;
        }

        public void StartCooldown(string spellId, int cooldownTurns)
        {
            var e = Get(spellId);
            if (e == null)
                return;

            e.cooldownRemainingTurns = Mathf.Max(e.cooldownRemainingTurns, cooldownTurns);
        }

        public int GrantExperience(string spellId, int xpGained)
        {
            if (xpGained <= 0)
                return 0;

            var def = _db.GetById(spellId);
            if (def == null)
                return 0;

            var entry = Get(spellId);
            if (entry == null)
                return 0;

            entry.experience += xpGained;

            int gained = 0;

            while (entry.level < def.maxLevel)
            {
                int required = _progression.GetXpToNextLevel(def.rarity, entry.level);
                if (entry.experience < required)
                    break;

                entry.experience -= required;
                entry.level += 1;
                gained += 1;
            }

            if (entry.level >= def.maxLevel)
                entry.experience = 0;

            return gained;
        }

        public int GetXpToNextLevel(string spellId)
        {
            var def = _db.GetById(spellId);
            var entry = Get(spellId);

            if (def == null || entry == null)
                return 0;
            if (entry.level >= def.maxLevel)
                return 0;

            return _progression.GetXpToNextLevel(def.rarity, entry.level);
        }
    }
}
