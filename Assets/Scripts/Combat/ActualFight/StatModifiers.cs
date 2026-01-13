using System;
using UnityEngine;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class StatModifiers
    {
        // ------------------------------------------------------------
        // More/Less multiplier bucket (the "more/less model")
        // - Buff:  +20% => more *= 1.2
        // - Debuff: 20% less => less *= 0.8
        // Final = more * less
        // ------------------------------------------------------------
        [Serializable]
        public struct MoreLessMult
        {
            public float more; // buffs: multiply by (1 + percent)
            public float less; // debuffs: multiply by (1 - percentLess)

            public float Final => more * less;

            public void Reset()
            {
                more = 1f;
                less = 1f;
            }

            /// <summary>+0.20 => more *= 1.2</summary>
            public void AddMore(float percent)
            {
                more *= (1f + percent);
            }

            /// <summary>Undo a previous AddMore(percent)</summary>
            public void RemoveMore(float percent)
            {
                float f = 1f + percent;
                if (f <= 0f)
                    return;
                more /= f;
            }

            /// <summary>0.20 => less *= 0.8</summary>
            public void AddLess(float percentLess)
            {
                float f = 1f - percentLess;
                if (f < 0f)
                    f = 0f;
                less *= f;
            }

            /// <summary>Undo a previous AddLess(percentLess)</summary>
            public void RemoveLess(float percentLess)
            {
                float f = 1f - percentLess;
                if (f <= 0f)
                    return;
                less /= f;
            }
        }

        // -------------------------
        // Generic flat bonuses
        // -------------------------
        public int attackPowerFlat; // bonus flat attack power -> +10
        public int magicPowerFlat; // bonus flat magic power -> +10

        // NEW: generic power (both attack + magic can use)
        public int powerFlat; // bonus flat power for both attack & magic

        public int spellBaseFlat;

        // Magic-only spells
        public int magicSpellBaseFlat;

        // Physical-only spells
        public int physicalSpellBaseFlat;

        public int defenceFlat; // flat bonus to all defences
        public int physicalDefenseFlat; // bonus flat physical defence -> +10
        public int magicDefenseFlat; // bonus flat magic defence -> +10

        public int attackSpeedFlat; // flat bonus to attack speed
        public int castingSpeedFlat; // flat bonus to casting speed

        public int damageFlat; // bonus flat damage (both) attack and magic damage
        public int magicDamageFlat; // bonus damage to magic attacks flat -> +10
        public int attackDamageFlat; // bonus damage to physical attacks flat -> +10

        // Power scaling flats
        public float powerScalingFlat; // flat power scaling general for both (magic / physical)
        public float attackPowerScalingFlat; // flat power scaling for physical attacks
        public float magicPowerScalingFlat; // flat power scaling for magic attacks

        // -------------------------
        // Generic multipliers (More/Less buckets)
        // -------------------------
        public MoreLessMult magicDamageMult; // bonus damage to magic attacks %
        public MoreLessMult physicalDamageMult; // bonus damage to physical attacks %
        public MoreLessMult damageMult; // bonus damage to all attacks %

        public MoreLessMult physicalSpellBaseMult;
        public MoreLessMult spellBaseMult;
        public MoreLessMult magicSpellBaseMult;

        // NEW: generic power multiplier (both attack + magic can use)
        public MoreLessMult powerMult; // bonus multiplier to power for both attack & magic

        // NEW: attack/magic-specific power multipliers
        public MoreLessMult attackPowerMult;
        public MoreLessMult magicPowerMult;

        public MoreLessMult attackPowerScalingMult; // power scaling % for physical attacks
        public MoreLessMult magicPowerScalingMult; // power scaling % for magic attacks
        public MoreLessMult powerScalingMult; // power scaling % for all attacks

        public MoreLessMult hitChanceMult;
        public MoreLessMult castingSpeedMult;
        public MoreLessMult attackSpeedMult;

        public MoreLessMult physicalDefenceMult; // % bonus to physical defence
        public MoreLessMult magicDefenceMult; // % bonus to magic defence
        public MoreLessMult defenceMult; // % bonus to all defence

        // -------------------------
        // Type-based layers (8 fields)
        // Each layer = Flat + Mult (starts at 0, mult starts at 1)
        // -------------------------

        // A) Attacker buffs -> increase outgoing damage (MORE)
        [SerializeField]
        private int[] attackerBonusFlatByType; // +50 fire

        [SerializeField]
        private float[] attackerBonusMultByType; // +20% fire

        // B) Defender debuffs -> increase damage taken (MORE)
        [SerializeField]
        private int[] defenderVulnFlatByType; // +30 fire

        [SerializeField]
        private float[] defenderVulnMultByType; // +20% fire

        // C) Defender buffs -> reduce damage taken (LESS)
        [SerializeField]
        private int[] defenderResistFlatByType; // -30 fire

        [SerializeField]
        private float[] defenderResistMultByType; // 20% less => *0.8

        // D) Attacker debuffs -> reduce outgoing damage (LESS)
        [SerializeField]
        private int[] attackerWeakenFlatByType; // -30 fire

        [SerializeField]
        private float[] attackerWeakenMultByType; // 30% less => *0.7

        public StatModifiers()
        {
            EnsureArrays();
            Reset();
        }

        // -------------------------
        // Helpers for NEW power fields (NO math pipeline here)
        // -------------------------
        public void AddPowerFlat(int value) => powerFlat += value;

        public void AddPowerMorePercent(float percent) => powerMult.AddMore(percent);

        public void RemovePowerMorePercent(float percent) => powerMult.RemoveMore(percent);

        public void AddPowerLessPercent(float percentLess) => powerMult.AddLess(percentLess);

        public void RemovePowerLessPercent(float percentLess) => powerMult.RemoveLess(percentLess);

        public void AddAttackPowerMorePercent(float percent) => attackPowerMult.AddMore(percent);

        public void RemoveAttackPowerMorePercent(float percent) =>
            attackPowerMult.RemoveMore(percent);

        public void AddAttackPowerLessPercent(float percentLess) =>
            attackPowerMult.AddLess(percentLess);

        public void RemoveAttackPowerLessPercent(float percentLess) =>
            attackPowerMult.RemoveLess(percentLess);

        public void AddMagicPowerMorePercent(float percent) => magicPowerMult.AddMore(percent);

        public void RemoveMagicPowerMorePercent(float percent) =>
            magicPowerMult.RemoveMore(percent);

        public void AddMagicPowerLessPercent(float percentLess) =>
            magicPowerMult.AddLess(percentLess);

        public void RemoveMagicPowerLessPercent(float percentLess) =>
            magicPowerMult.RemoveLess(percentLess);

        // -------------------------
        // Flat helpers for power scaling
        // -------------------------
        public void AddPowerScalingFlat(float value) => powerScalingFlat += value;

        public void AddAttackPowerScalingFlat(float value) => attackPowerScalingFlat += value;

        public void AddMagicPowerScalingFlat(float value) => magicPowerScalingFlat += value;

        // -------------------------
        // More/Less helpers for power scaling mults
        // -------------------------
        public void AddPowerScalingMorePercent(float percent) => powerScalingMult.AddMore(percent);

        public void RemovePowerScalingMorePercent(float percent) =>
            powerScalingMult.RemoveMore(percent);

        public void AddPowerScalingLessPercent(float percentLess) =>
            powerScalingMult.AddLess(percentLess);

        public void RemovePowerScalingLessPercent(float percentLess) =>
            powerScalingMult.RemoveLess(percentLess);

        public void AddAttackPowerScalingMorePercent(float percent) =>
            attackPowerScalingMult.AddMore(percent);

        public void RemoveAttackPowerScalingMorePercent(float percent) =>
            attackPowerScalingMult.RemoveMore(percent);

        public void AddAttackPowerScalingLessPercent(float percentLess) =>
            attackPowerScalingMult.AddLess(percentLess);

        public void RemoveAttackPowerScalingLessPercent(float percentLess) =>
            attackPowerScalingMult.RemoveLess(percentLess);

        public void AddMagicPowerScalingMorePercent(float percent) =>
            magicPowerScalingMult.AddMore(percent);

        public void RemoveMagicPowerScalingMorePercent(float percent) =>
            magicPowerScalingMult.RemoveMore(percent);

        public void AddMagicPowerScalingLessPercent(float percentLess) =>
            magicPowerScalingMult.AddLess(percentLess);

        public void RemoveMagicPowerScalingLessPercent(float percentLess) =>
            magicPowerScalingMult.RemoveLess(percentLess);

        // -------------------------
        // Existing helpers (damage, speeds, etc.)
        // -------------------------

        // Damage (all)
        public void AddDamageMorePercent(float percent) => damageMult.AddMore(percent);

        public void RemoveDamageMorePercent(float percent) => damageMult.RemoveMore(percent);

        public void AddDamageLessPercent(float percentLess) => damageMult.AddLess(percentLess);

        public void RemoveDamageLessPercent(float percentLess) =>
            damageMult.RemoveLess(percentLess);

        // SPell BaseDamage
        // Generic spell base
        public void AddSpellBaseFlat(int value) => spellBaseFlat += value;

        public void AddSpellBaseMorePercent(float percent) => spellBaseMult.AddMore(percent);

        public void AddSpellBaseLessPercent(float percentLess) =>
            spellBaseMult.AddLess(percentLess);

        // Magic spell base
        public void AddMagicSpellBaseFlat(int value) => magicSpellBaseFlat += value;

        public void AddMagicSpellBaseMorePercent(float percent) =>
            magicSpellBaseMult.AddMore(percent);

        public void AddMagicSpellBaseLessPercent(float percentLess) =>
            magicSpellBaseMult.AddLess(percentLess);

        // Physical spell base
        public void AddPhysicalSpellBaseFlat(int value) => physicalSpellBaseFlat += value;

        public void AddPhysicalSpellBaseMorePercent(float percent) =>
            physicalSpellBaseMult.AddMore(percent);

        public void AddPhysicalSpellBaseLessPercent(float percentLess) =>
            physicalSpellBaseMult.AddLess(percentLess);

        // Damage (physical only)
        public void AddPhysicalDamageMorePercent(float percent) =>
            physicalDamageMult.AddMore(percent);

        public void RemovePhysicalDamageMorePercent(float percent) =>
            physicalDamageMult.RemoveMore(percent);

        public void AddPhysicalDamageLessPercent(float percentLess) =>
            physicalDamageMult.AddLess(percentLess);

        public void RemovePhysicalDamageLessPercent(float percentLess) =>
            physicalDamageMult.RemoveLess(percentLess);

        // Damage (magic only)
        public void AddMagicDamageMorePercent(float percent) => magicDamageMult.AddMore(percent);

        public void RemoveMagicDamageMorePercent(float percent) =>
            magicDamageMult.RemoveMore(percent);

        public void AddMagicDamageLessPercent(float percentLess) =>
            magicDamageMult.AddLess(percentLess);

        public void RemoveMagicDamageLessPercent(float percentLess) =>
            magicDamageMult.RemoveLess(percentLess);

        // Hit chance
        public void AddHitChanceMorePercent(float percent) => hitChanceMult.AddMore(percent);

        public void RemoveHitChanceMorePercent(float percent) => hitChanceMult.RemoveMore(percent);

        public void AddHitChanceLessPercent(float percentLess) =>
            hitChanceMult.AddLess(percentLess);

        public void RemoveHitChanceLessPercent(float percentLess) =>
            hitChanceMult.RemoveLess(percentLess);

        // Casting speed
        public void AddCastingSpeedMorePercent(float percent) => castingSpeedMult.AddMore(percent);

        public void RemoveCastingSpeedMorePercent(float percent) =>
            castingSpeedMult.RemoveMore(percent);

        public void AddCastingSpeedLessPercent(float percentLess) =>
            castingSpeedMult.AddLess(percentLess);

        public void RemoveCastingSpeedLessPercent(float percentLess) =>
            castingSpeedMult.RemoveLess(percentLess);

        // Attack speed
        public void AddAttackSpeedMorePercent(float percent) => attackSpeedMult.AddMore(percent);

        public void RemoveAttackSpeedMorePercent(float percent) =>
            attackSpeedMult.RemoveMore(percent);

        public void AddAttackSpeedLessPercent(float percentLess) =>
            attackSpeedMult.AddLess(percentLess);

        public void RemoveAttackSpeedLessPercent(float percentLess) =>
            attackSpeedMult.RemoveLess(percentLess);

        // Defence (all)
        public void AddDefenceMorePercent(float percent) => defenceMult.AddMore(percent);

        public void RemoveDefenceMorePercent(float percent) => defenceMult.RemoveMore(percent);

        public void AddDefenceLessPercent(float percentLess) => defenceMult.AddLess(percentLess);

        public void RemoveDefenceLessPercent(float percentLess) =>
            defenceMult.RemoveLess(percentLess);

        // Physical defence
        public void AddPhysicalDefenceMorePercent(float percent) =>
            physicalDefenceMult.AddMore(percent);

        public void RemovePhysicalDefenceMorePercent(float percent) =>
            physicalDefenceMult.RemoveMore(percent);

        public void AddPhysicalDefenceLessPercent(float percentLess) =>
            physicalDefenceMult.AddLess(percentLess);

        public void RemovePhysicalDefenceLessPercent(float percentLess) =>
            physicalDefenceMult.RemoveLess(percentLess);

        // Magic defence
        public void AddMagicDefenceMorePercent(float percent) => magicDefenceMult.AddMore(percent);

        public void RemoveMagicDefenceMorePercent(float percent) =>
            magicDefenceMult.RemoveMore(percent);

        public void AddMagicDefenceLessPercent(float percentLess) =>
            magicDefenceMult.AddLess(percentLess);

        public void RemoveMagicDefenceLessPercent(float percentLess) =>
            magicDefenceMult.RemoveLess(percentLess);

        // -------------------------
        // Type arrays init/reset
        // -------------------------
        private static float[] NewMultArray(int count)
        {
            var arr = new float[count];
            for (int i = 0; i < count; i++)
                arr[i] = 1f;
            return arr;
        }

        private static int Idx(DamageType t) => (int)t;

        // Unity serialization note:
        // Constructors are not guaranteed to run for serialized instances,
        // so always ensure arrays exist before use.
        private void EnsureArrays()
        {
            int count = Enum.GetValues(typeof(DamageType)).Length;

            attackerBonusFlatByType ??= new int[count];
            attackerBonusMultByType ??= NewMultArray(count);

            defenderVulnFlatByType ??= new int[count];
            defenderVulnMultByType ??= NewMultArray(count);

            defenderResistFlatByType ??= new int[count];
            defenderResistMultByType ??= NewMultArray(count);

            attackerWeakenFlatByType ??= new int[count];
            attackerWeakenMultByType ??= NewMultArray(count);
        }

        // -------------------------
        // A) Attacker buffs (MORE)
        // -------------------------
        public int GetAttackerBonusFlat(DamageType t)
        {
            EnsureArrays();
            return attackerBonusFlatByType[Idx(t)];
        }

        public float GetAttackerBonusMult(DamageType t)
        {
            EnsureArrays();
            return attackerBonusMultByType[Idx(t)];
        }

        public void AddAttackerBonusFlat(DamageType t, int value)
        {
            EnsureArrays();
            attackerBonusFlatByType[Idx(t)] += value;
        }

        /// <summary>+0.20 => *1.2 (multiplicative stacking)</summary>
        public void AddAttackerBonusMorePercent(DamageType t, float percent)
        {
            EnsureArrays();
            attackerBonusMultByType[Idx(t)] *= (1f + percent);
        }

        /// <summary>Undo a previous AddAttackerBonusMorePercent(t, percent)</summary>
        public void RemoveAttackerBonusMorePercent(DamageType t, float percent)
        {
            EnsureArrays();
            float f = 1f + percent;
            if (f <= 0f)
                return;
            attackerBonusMultByType[Idx(t)] /= f;
        }

        // -------------------------
        // B) Defender vulnerability (MORE taken)
        // -------------------------
        public int GetDefenderVulnFlat(DamageType t)
        {
            EnsureArrays();
            return defenderVulnFlatByType[Idx(t)];
        }

        public float GetDefenderVulnMult(DamageType t)
        {
            EnsureArrays();
            return defenderVulnMultByType[Idx(t)];
        }

        public void AddDefenderVulnFlat(DamageType t, int value)
        {
            EnsureArrays();
            defenderVulnFlatByType[Idx(t)] += value;
        }

        /// <summary>+0.20 => target takes *1.2 (multiplicative stacking)</summary>
        public void AddDefenderVulnMorePercent(DamageType t, float percent)
        {
            EnsureArrays();
            defenderVulnMultByType[Idx(t)] *= (1f + percent);
        }

        /// <summary>Undo a previous AddDefenderVulnMorePercent(t, percent)</summary>
        public void RemoveDefenderVulnMorePercent(DamageType t, float percent)
        {
            EnsureArrays();
            float f = 1f + percent;
            if (f <= 0f)
                return;
            defenderVulnMultByType[Idx(t)] /= f;
        }

        // -------------------------
        // C) Defender resistance (LESS taken)
        // -------------------------
        public int GetDefenderResistFlat(DamageType t)
        {
            EnsureArrays();
            return defenderResistFlatByType[Idx(t)];
        }

        public float GetDefenderResistMult(DamageType t)
        {
            EnsureArrays();
            return defenderResistMultByType[Idx(t)];
        }

        public void AddDefenderResistFlat(DamageType t, int value)
        {
            EnsureArrays();
            defenderResistFlatByType[Idx(t)] += value;
        }

        /// <summary>0.20 => target takes *0.8 (multiplicative stacking)</summary>
        public void AddDefenderResistLessPercent(DamageType t, float percentLess)
        {
            EnsureArrays();
            defenderResistMultByType[Idx(t)] *= Clamp01Factor(1f - percentLess);
        }

        /// <summary>Undo a previous AddDefenderResistLessPercent(t, percentLess)</summary>
        public void RemoveDefenderResistLessPercent(DamageType t, float percentLess)
        {
            EnsureArrays();
            float f = Clamp01Factor(1f - percentLess);
            if (f <= 0f)
                return;
            defenderResistMultByType[Idx(t)] /= f;
        }

        // -------------------------
        // D) Attacker weaken (LESS dealt)
        // -------------------------
        public int GetAttackerWeakenFlat(DamageType t)
        {
            EnsureArrays();
            return attackerWeakenFlatByType[Idx(t)];
        }

        public float GetAttackerWeakenMult(DamageType t)
        {
            EnsureArrays();
            return attackerWeakenMultByType[Idx(t)];
        }

        public void AddAttackerWeakenFlat(DamageType t, int value)
        {
            EnsureArrays();
            attackerWeakenFlatByType[Idx(t)] += value;
        }

        /// <summary>0.20 => attacker deals *0.8 (multiplicative stacking)</summary>
        public void AddAttackerWeakenLessPercent(DamageType t, float percentLess)
        {
            EnsureArrays();
            attackerWeakenMultByType[Idx(t)] *= Clamp01Factor(1f - percentLess);
        }

        /// <summary>Undo a previous AddAttackerWeakenLessPercent(t, percentLess)</summary>
        public void RemoveAttackerWeakenLessPercent(DamageType t, float percentLess)
        {
            EnsureArrays();
            float f = Clamp01Factor(1f - percentLess);
            if (f <= 0f)
                return;
            attackerWeakenMultByType[Idx(t)] /= f;
        }

        // Utility: avoid negative multipliers for "less"
        private static float Clamp01Factor(float v) => v < 0f ? 0f : v;

        public void Reset()
        {
            EnsureArrays();

            // Flats
            attackPowerFlat = 0;
            magicPowerFlat = 0;
            powerFlat = 0;

            defenceFlat = 0;
            physicalDefenseFlat = 0;
            magicDefenseFlat = 0;

            attackSpeedFlat = 0;
            castingSpeedFlat = 0;

            damageFlat = 0;
            magicDamageFlat = 0;
            attackDamageFlat = 0;

            powerScalingFlat = 0f;
            attackPowerScalingFlat = 0f;
            magicPowerScalingFlat = 0f;

            spellBaseFlat = 0;
            magicSpellBaseFlat = 0;
            physicalSpellBaseFlat = 0;

            // Mults
            magicDamageMult.Reset();
            physicalDamageMult.Reset();
            damageMult.Reset();

            spellBaseMult.Reset();
            magicSpellBaseMult.Reset();
            physicalSpellBaseMult.Reset();

            powerMult.Reset();
            attackPowerMult.Reset();
            magicPowerMult.Reset();

            attackPowerScalingMult.Reset();
            magicPowerScalingMult.Reset();
            powerScalingMult.Reset();

            hitChanceMult.Reset();
            castingSpeedMult.Reset();
            attackSpeedMult.Reset();

            physicalDefenceMult.Reset();
            magicDefenceMult.Reset();
            defenceMult.Reset();

            // Type-based arrays
            ClearInts(attackerBonusFlatByType);
            ResetMults(attackerBonusMultByType);

            ClearInts(defenderVulnFlatByType);
            ResetMults(defenderVulnMultByType);

            ClearInts(defenderResistFlatByType);
            ResetMults(defenderResistMultByType);

            ClearInts(attackerWeakenFlatByType);
            ResetMults(attackerWeakenMultByType);
        }

        private static void ClearInts(int[] arr) => Array.Clear(arr, 0, arr.Length);

        private static void ResetMults(float[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
                arr[i] = 1f;
        }

        // Convenience accessors:
        public float DamageMultFinal => damageMult.Final;
        public float PhysicalDamageMultFinal => physicalDamageMult.Final;
        public float MagicDamageMultFinal => magicDamageMult.Final;

        public float PowerMultFinal => powerMult.Final;
        public float AttackPowerMultFinal => attackPowerMult.Final;
        public float MagicPowerMultFinal => magicPowerMult.Final;

        public float PowerScalingMultFinal => powerScalingMult.Final;
        public float AttackPowerScalingMultFinal => attackPowerScalingMult.Final;
        public float MagicPowerScalingMultFinal => magicPowerScalingMult.Final;

        public float HitChanceMultFinal => hitChanceMult.Final;
        public float CastingSpeedMultFinal => castingSpeedMult.Final;
        public float AttackSpeedMultFinal => attackSpeedMult.Final;

        public float DefenceMultFinal => defenceMult.Final;
        public float PhysicalDefenceMultFinal => physicalDefenceMult.Final;
        public float MagicDefenceMultFinal => magicDefenceMult.Final;

        public float SpellBaseMultFinal => spellBaseMult.Final;
        public float MagicSpellBaseMultFinal => magicSpellBaseMult.Final;
        public float PhysicalSpellBaseMultFinal => physicalSpellBaseMult.Final;
    }
}
