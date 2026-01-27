using System;
using System.Collections.Generic;
using MyGame.Common;
using MyGame.Economy;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SaveData
    {
        // Increase when you change the structure
        public int version = 20;

        // Identity
        public string saveId; // GUID string
        public string characterName;

        public string avatarId;

        // Character build
        public string classId;
        public string specId;

        // Current stats (initially set at creation, later updated when player allocates points)
        public Stats finalStats;

        // How many unspent stat points are available on Dashboard
        public int unspentStatPoints = 3;

        public List<SavedTowerProgress> towers = new List<SavedTowerProgress>();

        // Total play time across ALL sessions (in seconds)
        public int totalPlayTimeSeconds;

        // Experience / Level
        public int level = 1;
        public long exp = 0;
        public long expToNextLevel = 100;

        public Currency gold = new Currency(5);
        public int currentHp = 100;
        public int currentMana = 50;
        public Tier tier = Tier.Tier1;
        public SpellActiveSlotProgress spellActiveSlotProgress = new();

        // Unlocks (one-off, persisted by id)
        public List<string> unlockedIds = new List<string>();

        // ✅ Spells (player-owned progression + runtime cooldowns)
        public List<SavedSpellEntry> spells = new List<SavedSpellEntry>();

        // ✅ Inventory (starter)
        public List<SavedItemStackEntry> items = new List<SavedItemStackEntry>();
        public List<SavedEquipmentInstance> equipmentInstances = new List<SavedEquipmentInstance>();
        public List<SavedEquippedSlot> equippedSlots = new List<SavedEquippedSlot>();

        // ✅ Equipment Inventory UI layout (fixed slot positions)
        // Stores where equipment instances appear in the Equipment Inventory grid.
        public List<SavedEquipmentInventorySlotEntry> equipmentInventorySlots =
            new List<SavedEquipmentInventorySlotEntry>();

        // ✅ Active combat item slots (Inventory UI)
        // 4 fixed slots which can hold either an itemId OR an equipmentInstanceId.
        public List<SavedCombatActiveSlotEntry> activeCombatSlots =
            new List<SavedCombatActiveSlotEntry>();

        // ✅ Persistent item cooldowns (optional)
        // Only used for items flagged to carry cooldown between fights.
        public List<SavedItemCooldownEntry> persistentItemCooldowns =
            new List<SavedItemCooldownEntry>();

        // Metadata
        public string createdUtc;
        public string lastSavedUtc;
    }
}
