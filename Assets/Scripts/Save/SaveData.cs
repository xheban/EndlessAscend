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
        public int version = 13;

        // Identity
        public string saveId; // GUID string
        public string characterName;

        public string playerIconId;

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
        public int exp = 0;
        public int expToNextLevel = 100;

        public Currency gold = new Currency(5);
        public int currentHp = 100;
        public int currentMana = 50;
        public Tier tier = Tier.Tier1;
        public SpellActiveSlotProgress spellActiveSlotProgress = new();

        // ✅ Spells (player-owned progression + runtime cooldowns)
        public List<SavedSpellEntry> spells = new List<SavedSpellEntry>();

        // Metadata
        public string createdUtc;
        public string lastSavedUtc;
    }
}
