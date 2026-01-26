using System;
using MyGame.Common; // Tier, Stats (your core stats type)
using Unity.VisualScripting;
using UnityEngine;

namespace MyGame.Combat
{
    public static class CombatStatCalculator
    {
        // -------------------------
        // Public API (reusable)
        // -------------------------

        public static int CalculateMaxHp(Stats s, int level, Tier tier)
        {
            int tierBonus = TierToFlatBonus(tier, perTier: 25);
            int hp = 50 + (s.endurance * 12) + (s.strength * 1) + (level * 10) + tierBonus;

            return ClampMin(hp, 1);
        }

        public static int CalculateMaxMana(Stats s, int level, Tier tier)
        {
            int tierBonus = TierToFlatBonus(tier, perTier: 15);
            int mana = 20 + (s.intelligence * 8) + (s.spirit * 8) + (level * 6) + tierBonus;

            return ClampMin(mana, 0);
        }

        public static int CalculateOutOfCombatHpRegenPer10s(
            int maxHp,
            Stats s,
            int level,
            Tier tier
        )
        {
            int str = ClampMin(s.strength, 0);
            int spi = ClampMin(s.spirit, 0);

            int t = (int)tier;

            // Base: 1% of max mana.
            float baseRegen = maxHp / 100f;

            // Bonus: percent of max mana from stats (soft-capped).
            // END:SPIRIT = 2:8 ratio (fixed-point x10).
            int points10 = (str * 2 + spi * 8) * 10;

            // Slightly higher early-game impact than HP regen.
            // Example target: at maxMana=200 and Spirit=10, bonus ~ +3 per 10s.
            int maxBonusPercent = 6 + t;
            int k10 = (200 + level * 20 + t * 300) * 10;

            float bonusPercent = SoftCapPercentFromPoints10(points10, maxBonusPercent, k10);
            float bonusRegen = (maxHp * bonusPercent) / 100f;

            return Mathf.FloorToInt(Math.Max(baseRegen + bonusRegen, 1f));
        }

        public static int CalculateOutOfCombatManaRegenPer10s(
            int maxMana,
            Stats s,
            int level,
            Tier tier
        )
        {
            int intel = ClampMin(s.intelligence, 0);
            int spi = ClampMin(s.spirit, 0);

            int t = (int)tier;

            // Base: 1% of max mana.
            float baseRegen = maxMana / 100f;

            // Bonus: percent of max mana from stats (soft-capped).
            // END:SPIRIT = 2:8 ratio (fixed-point x10).
            int points10 = (intel * 2 + spi * 8) * 10;

            // Slightly higher early-game impact than HP regen.
            // Example target: at maxMana=200 and Spirit=10, bonus ~ +3 per 10s.
            int maxBonusPercent = 6 + t;
            int k10 = (200 + level * 20 + t * 300) * 10;

            float bonusPercent = SoftCapPercentFromPoints10(points10, maxBonusPercent, k10);
            float bonusRegen = (maxMana * bonusPercent) / 100f;

            return Mathf.FloorToInt(Math.Max(baseRegen + bonusRegen, 1f));
        }

        public static int CalculateAttackPower(Stats s, int level, Tier tier)
        {
            int str = ClampMin(s.strength, 0);
            int agi = ClampMin(s.agility, 0);

            // PowerPoints10: STR=0.8, AGI=0.2 (matches 4*STR + AGI)
            int points10 = str * 8 + agi * 2;

            int result = BasePower(level, tier) + PowerFromPoints10(points10, level, tier);
            return ClampMin(result, 0);
        }

        public static int CalculateMagicPower(Stats s, int level, Tier tier)
        {
            int intel = ClampMin(s.intelligence, 0);

            // INT only (fixed-point x10)
            int points10 = intel * 10;

            int result = BasePower(level, tier) + PowerFromPoints10(points10, level, tier);
            return ClampMin(result, 0);
        }

        public static int CalculatePhysicalDefence(Stats s, int level, Tier tier)
        {
            int end = ClampMin(s.endurance, 0);
            int str = ClampMin(s.strength, 0);

            // END:STR = 3:1  -> 9:3 in points12 space
            int points12 = end * 9 + str * 3;

            int result = BaseDefense(level, tier) + DefenseFromPoints12(points12, level, tier);
            return ClampMin(result, 0);
        }

        public static int CalculateMagicalDefence(Stats s, int level, Tier tier)
        {
            int end = ClampMin(s.endurance, 0);
            int intel = ClampMin(s.intelligence, 0);

            // END:INT = 3:1  -> 9:3 in points12 space
            int points12 = end * 9 + intel * 3;

            int result = BaseDefense(level, tier) + DefenseFromPoints12(points12, level, tier);
            return ClampMin(result, 0);
        }

        public static int CalculateEvasion(Stats s, int level, Tier tier)
        {
            int agi = ClampMin(s.agility, 0);
            int spi = ClampMin(s.spirit, 0);
            int t = (int)tier;

            // Base evasion: small, predictable growth
            int baseEva = 5 + level / 2 + t * 5;

            // 4:1 ratio (AGI:SPI) using fixed-point x10
            int points10 = agi * 8 + spi * 2;

            // Soft-capped bonus so 10k agi doesn't mean untouchable
            int maxBonus = 300 + t * 50; // cap on evasion bonus
            int k10 = (300 + level * 12 + t * 350) * 10; // curve control (scaled by 10)

            int bonus = SoftCapBonusFromPoints10(points10, maxBonus, k10);

            // Optional: absolute cap if you want one (recommended)
            // return Clamp(baseEva + bonus, 0, 500);
            return ClampMin(baseEva + bonus, 0);
        }

        public static int CalculateAccuracy(Stats s, int level, Tier tier)
        {
            int spi = ClampMin(s.spirit, 0);
            int t = (int)tier;

            // Base accuracy: steady progression
            int baseAcc = 15 + level / 2 + t * 6;

            // Spirit-only contribution (fixed-point x10)
            int points10 = spi * 10;

            // Soft-cap to prevent guaranteed hits
            int maxBonus = 300 + t * 50; // mirrors evasion scale
            int k10 = (300 + level * 12 + t * 350) * 10;

            int bonus = SoftCapBonusFromPoints10(points10, maxBonus, k10);

            // Optional absolute cap (recommended if accuracy is % based)
            // return Clamp(baseAcc + bonus, 0, 500);

            return ClampMin(baseAcc + bonus, 0);
        }

        public static int CalculateAttackSpeed(Stats s, int level, Tier tier)
        {
            int agi = ClampMin(s.agility, 0);

            int points10 = agi * 10; // AGI = 1.0 point
            int result = BaseSpeed(level, tier) + SpeedBonus(points10, level, tier);

            return Clamp(result, 1, 200);
        }

        public static int CalculateCastSpeed(Stats s, int level, Tier tier)
        {
            int agi = ClampMin(s.agility, 0);
            int intel = ClampMin(s.intelligence, 0);

            int points10 = agi * 4 + intel * 6; // AGI=0.4, INT=0.6 (scaled by 10)
            int result = BaseSpeed(level, tier) + SpeedBonus(points10, level, tier);

            return Clamp(result, 1, 200);
        }

        public static DerivedCombatStats CalculateAll(Stats s, int level, Tier tier)
        {
            var outStats = new DerivedCombatStats();

            outStats.maxHp = CalculateMaxHp(s, level, tier);
            outStats.maxMana = CalculateMaxMana(s, level, tier);

            // Powers
            outStats.attackPower = CalculateAttackPower(s, level, tier);
            outStats.magicPower = CalculateMagicPower(s, level, tier);

            // Defense / evasion
            outStats.physicalDefense = CalculatePhysicalDefence(s, level, tier);
            outStats.magicalDefense = CalculateMagicalDefence(s, level, tier);

            outStats.evasion = CalculateEvasion(s, level, tier);
            outStats.accuracy = CalculateAccuracy(s, level, tier);

            outStats.castSpeed = CalculateCastSpeed(s, level, tier);
            outStats.attackSpeed = CalculateAttackSpeed(s, level, tier);

            return outStats;
        }

        // -------------------------
        // Helpers
        // -------------------------

        private static int TierToFlatBonus(Tier tier, int perTier)
        {
            // Tier1 -> 0, Tier2 -> 1*perTier, ...
            int t = tier switch
            {
                Tier.Tier1 => 0,
                Tier.Tier2 => 1,
                Tier.Tier3 => 2,
                Tier.Tier4 => 3,
                Tier.Tier5 => 4,
                Tier.Tier6 => 5,
                _ => 0,
            };
            return t * perTier;
        }

        private static int ClampMin(int v, int min) => v < min ? min : v;

        private static int Clamp(int v, int min, int max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        private static float SoftCapPercentFromPoints10(int points10, int maxPercent, int k10)
        {
            if (points10 <= 0)
                return 0;

            float result = (points10 * (float)maxPercent) / (points10 + k10);
            return result;
        }

        // points10 is "speed points * 10"
        private static int SoftCapBonusFromPoints10(int points10, int maxBonus, int k10)
        {
            if (points10 <= 0)
                return 0;

            // bonus = points/(points + k) * maxBonus
            // with fixed-point: points10 and k10 are both scaled by 10, so ratio is preserved.
            return (points10 * maxBonus) / (points10 + k10);
        }

        private static int PowerFromPoints10(int powerPoints10, int level, Tier tier)
        {
            int t = (int)tier;

            // 1) Linear component: always increases (prevents "flat" feeling)
            // points10 / 50  ==  (points / 5)
            // Tune: /60 weaker, /40 stronger.
            int linear = powerPoints10 / 50;

            // 2) Diminishing bonus component: bounded and tier-scaled
            int maxBonus = 400 + t * 200; // hard cap on DR bonus
            int k10 = (600 + level * 30 + t * 600) * 10; // curve control, scaled by 10

            int drBonus = SoftCapBonusFromPoints10(powerPoints10, maxBonus, k10);

            return linear + drBonus;
        }

        private static int SoftCapBonusFromPoints12(int points12, int maxBonus, int k12)
        {
            if (points12 <= 0)
                return 0;
            return (points12 * maxBonus) / (points12 + k12); // bounded
        }

        private static int BaseDefense(int level, Tier tier)
        {
            int t = (int)tier;
            return 10 + level * 2 + t * 15;
        }

        private static int DefenseFromPoints12(int defPoints12, int level, Tier tier)
        {
            int t = (int)tier;

            // Always-growing part
            // points12/60 ~= (points/5) if you think in "normal sized" stats
            int linear = defPoints12 / 60; // tune: /80 weaker, /40 stronger

            // Bounded extra so insane stats don't make you immortal
            int maxBonus = 500 + t * 250;
            int k12 = (800 + level * 40 + t * 800) * 12;

            int drBonus = SoftCapBonusFromPoints12(defPoints12, maxBonus, k12);

            return linear + drBonus;
        }

        private static int BaseSpeed(int level, Tier tier)
        {
            int t = (int)tier;
            return 12 + level / 2 + t * 2;
        }

        private static int BasePower(int level, Tier tier)
        {
            int t = (int)tier;
            // stable progression from level + tier
            return 15 + level * 3 + t * 25;
        }

        private static int SpeedBonus(int speedPoints10, int level, Tier tier)
        {
            int t = (int)tier;

            int maxBonus = 60 + t * 10; // hard cap on bonus
            int k10 = (150 + level * 8 + t * 150) * 10; // k scaled by 10

            return SoftCapBonusFromPoints10(speedPoints10, maxBonus, k10);
        }
    }
}
