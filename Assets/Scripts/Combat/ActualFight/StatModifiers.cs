using System;
using UnityEngine;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class StatModifiers
    {
        // ------------------------------------------------------------
        // Single multiplicative buckets (start at 1.0)
        // - Buff:  +20% => mult *= 1.2
        // - Debuff: -20% => mult *= 0.8
        // ------------------------------------------------------------

        // -------------------------
        // Generic flat bonuses
        // -------------------------
        public int spellBaseFlat;

        // Magic-only spells
        public int magicSpellBaseFlat;

        // Physical-only spells
        public int physicalSpellBaseFlat;

        public int damageFlat; // bonus flat damage (both) attack and magic damage
        public int magicDamageFlat; // bonus damage to magic attacks flat -> +10
        public int attackDamageFlat; // bonus damage to physical attacks flat -> +10
        public int meleeDamageBonusFlat; // bonus flat damage for melee spells
        public int rangedDamageBonusFlat; // bonus flat damage for ranged spells

        // Power scaling flats
        public float powerScalingFlat; // flat power scaling general for both (magic / physical)
        public float attackPowerScalingFlat; // flat power scaling for physical attacks
        public float magicPowerScalingFlat; // flat power scaling for magic attacks

        // -------------------------
        // Generic multipliers (single bucket)
        // -------------------------
        public float magicDamageMult = 1f; // bonus damage to magic attacks %
        public float physicalDamageMult = 1f; // bonus damage to physical attacks %
        public float damageMult = 1f; // bonus damage to all attacks %
        public float meleeDamageBonusMult = 1f; // bonus damage to melee attacks %
        public float rangedDamageBonusMult = 1f; // bonus damage to ranged attacks %

        public float physicalSpellBaseMult = 1f;
        public float spellBaseMult = 1f;
        public float magicSpellBaseMult = 1f;

        public float attackPowerScalingMult = 1f; // power scaling % for physical attacks
        public float magicPowerScalingMult = 1f; // power scaling % for magic attacks
        public float powerScalingMult = 1f; // power scaling % for all attacks


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
            ResetAll();
        }

        private StatModifiers(bool skipReset)
        {
            EnsureArrays();
            if (!skipReset)
                ResetAll();
        }

        private void EnsureInitialized()
        {
            EnsureArrays();

            if (magicDamageMult == 0f)
                magicDamageMult = 1f;
            if (physicalDamageMult == 0f)
                physicalDamageMult = 1f;
            if (damageMult == 0f)
                damageMult = 1f;
            if (meleeDamageBonusMult == 0f)
                meleeDamageBonusMult = 1f;
            if (rangedDamageBonusMult == 0f)
                rangedDamageBonusMult = 1f;

            if (physicalSpellBaseMult == 0f)
                physicalSpellBaseMult = 1f;
            if (spellBaseMult == 0f)
                spellBaseMult = 1f;
            if (magicSpellBaseMult == 0f)
                magicSpellBaseMult = 1f;

            if (attackPowerScalingMult == 0f)
                attackPowerScalingMult = 1f;
            if (magicPowerScalingMult == 0f)
                magicPowerScalingMult = 1f;
            if (powerScalingMult == 0f)
                powerScalingMult = 1f;
        }

        public StatModifiers Clone()
        {
            EnsureInitialized();

            var clone = new StatModifiers(skipReset: true);
            clone.EnsureInitialized();

            // ---------- Flats ----------
            clone.spellBaseFlat = spellBaseFlat;
            clone.magicSpellBaseFlat = magicSpellBaseFlat;
            clone.physicalSpellBaseFlat = physicalSpellBaseFlat;

            clone.damageFlat = damageFlat;
            clone.magicDamageFlat = magicDamageFlat;
            clone.attackDamageFlat = attackDamageFlat;
            clone.meleeDamageBonusFlat = meleeDamageBonusFlat;
            clone.rangedDamageBonusFlat = rangedDamageBonusFlat;

            clone.powerScalingFlat = powerScalingFlat;
            clone.attackPowerScalingFlat = attackPowerScalingFlat;
            clone.magicPowerScalingFlat = magicPowerScalingFlat;

            // ---------- More / Less buckets ----------
            clone.magicDamageMult = magicDamageMult;
            clone.physicalDamageMult = physicalDamageMult;
            clone.damageMult = damageMult;
            clone.meleeDamageBonusMult = meleeDamageBonusMult;
            clone.rangedDamageBonusMult = rangedDamageBonusMult;

            clone.spellBaseMult = spellBaseMult;
            clone.magicSpellBaseMult = magicSpellBaseMult;
            clone.physicalSpellBaseMult = physicalSpellBaseMult;

            clone.attackPowerScalingMult = attackPowerScalingMult;
            clone.magicPowerScalingMult = magicPowerScalingMult;
            clone.powerScalingMult = powerScalingMult;


            // ---------- Type-based arrays ----------
            clone.attackerBonusFlatByType = (int[])attackerBonusFlatByType.Clone();
            clone.attackerBonusMultByType = (float[])attackerBonusMultByType.Clone();

            clone.defenderVulnFlatByType = (int[])defenderVulnFlatByType.Clone();
            clone.defenderVulnMultByType = (float[])defenderVulnMultByType.Clone();

            clone.defenderResistFlatByType = (int[])defenderResistFlatByType.Clone();
            clone.defenderResistMultByType = (float[])defenderResistMultByType.Clone();

            clone.attackerWeakenFlatByType = (int[])attackerWeakenFlatByType.Clone();
            clone.attackerWeakenMultByType = (float[])attackerWeakenMultByType.Clone();

            return clone;
        }

        public void CopyFrom(StatModifiers other)
        {
            EnsureInitialized();
            other.EnsureInitialized();
            // ---------- Flats ----------
            spellBaseFlat = other.spellBaseFlat;
            magicSpellBaseFlat = other.magicSpellBaseFlat;
            physicalSpellBaseFlat = other.physicalSpellBaseFlat;

            damageFlat = other.damageFlat;
            magicDamageFlat = other.magicDamageFlat;
            attackDamageFlat = other.attackDamageFlat;
            meleeDamageBonusFlat = other.meleeDamageBonusFlat;
            rangedDamageBonusFlat = other.rangedDamageBonusFlat;

            powerScalingFlat = other.powerScalingFlat;
            attackPowerScalingFlat = other.attackPowerScalingFlat;
            magicPowerScalingFlat = other.magicPowerScalingFlat;

            // ---------- More / Less ----------
            magicDamageMult = other.magicDamageMult;
            physicalDamageMult = other.physicalDamageMult;
            damageMult = other.damageMult;
            meleeDamageBonusMult = other.meleeDamageBonusMult;
            rangedDamageBonusMult = other.rangedDamageBonusMult;

            spellBaseMult = other.spellBaseMult;
            magicSpellBaseMult = other.magicSpellBaseMult;
            physicalSpellBaseMult = other.physicalSpellBaseMult;

            attackPowerScalingMult = other.attackPowerScalingMult;
            magicPowerScalingMult = other.magicPowerScalingMult;
            powerScalingMult = other.powerScalingMult;


            // ---------- Arrays ----------
            Array.Copy(
                other.attackerBonusFlatByType,
                attackerBonusFlatByType,
                attackerBonusFlatByType.Length
            );
            Array.Copy(
                other.attackerBonusMultByType,
                attackerBonusMultByType,
                attackerBonusMultByType.Length
            );

            Array.Copy(
                other.defenderVulnFlatByType,
                defenderVulnFlatByType,
                defenderVulnFlatByType.Length
            );
            Array.Copy(
                other.defenderVulnMultByType,
                defenderVulnMultByType,
                defenderVulnMultByType.Length
            );

            Array.Copy(
                other.defenderResistFlatByType,
                defenderResistFlatByType,
                defenderResistFlatByType.Length
            );
            Array.Copy(
                other.defenderResistMultByType,
                defenderResistMultByType,
                defenderResistMultByType.Length
            );

            Array.Copy(
                other.attackerWeakenFlatByType,
                attackerWeakenFlatByType,
                attackerWeakenFlatByType.Length
            );
            Array.Copy(
                other.attackerWeakenMultByType,
                attackerWeakenMultByType,
                attackerWeakenMultByType.Length
            );

        }

        // -------------------------
        // Flat helpers for power scaling
        // -------------------------
        public void AddPowerScalingFlat(float value) => powerScalingFlat += value;

        public void AddAttackPowerScalingFlat(float value) => attackPowerScalingFlat += value;

        public void AddMagicPowerScalingFlat(float value) => magicPowerScalingFlat += value;

        // -------------------------
        // More/Less helpers for power scaling mults
        // -------------------------
        public void AddPowerScalingPercent(float percent) =>
            ApplyPercent(ref powerScalingMult, percent);

        public void RemovePowerScalingPercent(float percent) =>
            RemovePercent(ref powerScalingMult, percent);

        public void AddAttackPowerScalingPercent(float percent) =>
            ApplyPercent(ref attackPowerScalingMult, percent);

        public void RemoveAttackPowerScalingPercent(float percent) =>
            RemovePercent(ref attackPowerScalingMult, percent);

        public void AddMagicPowerScalingPercent(float percent) =>
            ApplyPercent(ref magicPowerScalingMult, percent);

        public void RemoveMagicPowerScalingPercent(float percent) =>
            RemovePercent(ref magicPowerScalingMult, percent);

        // -------------------------
        // Existing helpers (damage, speeds, etc.)
        // -------------------------

        // Damage (all)
        public void AddDamagePercent(float percent) => ApplyPercent(ref damageMult, percent);

        public void RemoveDamagePercent(float percent) => RemovePercent(ref damageMult, percent);

        // Melee/ranged bonus damage
        public int GetMeleeDamageBonusFlat() => meleeDamageBonusFlat;

        public float GetMeleeDamageBonusMult() => meleeDamageBonusMult;

        public void AddMeleeDamageBonusFlat(int value) => meleeDamageBonusFlat += value;

        public void RemoveMeleeDamageBonusFlat(int value) => meleeDamageBonusFlat -= value;

        public void AddMeleeDamageBonusPercent(float percent) =>
            ApplyPercent(ref meleeDamageBonusMult, percent);

        public void RemoveMeleeDamageBonusPercent(float percent) =>
            RemovePercent(ref meleeDamageBonusMult, percent);

        public int GetRangedDamageBonusFlat() => rangedDamageBonusFlat;

        public float GetRangedDamageBonusMult() => rangedDamageBonusMult;

        public void AddRangedDamageBonusFlat(int value) => rangedDamageBonusFlat += value;

        public void RemoveRangedDamageBonusFlat(int value) => rangedDamageBonusFlat -= value;

        public void AddRangedDamageBonusPercent(float percent) =>
            ApplyPercent(ref rangedDamageBonusMult, percent);

        public void RemoveRangedDamageBonusPercent(float percent) =>
            RemovePercent(ref rangedDamageBonusMult, percent);

        // SPell BaseDamage
        // Generic spell base
        public void AddSpellBaseFlat(int value) => spellBaseFlat += value;

        public void AddSpellBasePercent(float percent) =>
            ApplyPercent(ref spellBaseMult, percent);

        public void RemoveSpellBasePercent(float percent) =>
            RemovePercent(ref spellBaseMult, percent);

        // Magic spell base
        public void AddMagicSpellBaseFlat(int value) => magicSpellBaseFlat += value;

        public void AddMagicSpellBasePercent(float percent) =>
            ApplyPercent(ref magicSpellBaseMult, percent);

        public void RemoveMagicSpellBasePercent(float percent) =>
            RemovePercent(ref magicSpellBaseMult, percent);

        // Physical spell base
        public void AddPhysicalSpellBaseFlat(int value) => physicalSpellBaseFlat += value;

        public void AddPhysicalSpellBasePercent(float percent) =>
            ApplyPercent(ref physicalSpellBaseMult, percent);

        public void RemovePhysicalSpellBasePercent(float percent) =>
            RemovePercent(ref physicalSpellBaseMult, percent);

        // Damage (physical only)
        public void AddPhysicalDamagePercent(float percent) =>
            ApplyPercent(ref physicalDamageMult, percent);

        public void RemovePhysicalDamagePercent(float percent) =>
            RemovePercent(ref physicalDamageMult, percent);

        // Damage (magic only)
        public void AddMagicDamagePercent(float percent) =>
            ApplyPercent(ref magicDamageMult, percent);

        public void RemoveMagicDamagePercent(float percent) =>
            RemovePercent(ref magicDamageMult, percent);

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
        public void AddAttackerBonusPercent(DamageType t, float percent)
        {
            EnsureArrays();
            ApplyPercent(ref attackerBonusMultByType[Idx(t)], percent);
        }

        /// <summary>Undo a previous AddAttackerBonusPercent(t, percent)</summary>
        public void RemoveAttackerBonusPercent(DamageType t, float percent)
        {
            EnsureArrays();
            RemovePercent(ref attackerBonusMultByType[Idx(t)], percent);
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
        public void AddDefenderVulnPercent(DamageType t, float percent)
        {
            EnsureArrays();
            ApplyPercent(ref defenderVulnMultByType[Idx(t)], percent);
        }

        /// <summary>Undo a previous AddDefenderVulnPercent(t, percent)</summary>
        public void RemoveDefenderVulnPercent(DamageType t, float percent)
        {
            EnsureArrays();
            RemovePercent(ref defenderVulnMultByType[Idx(t)], percent);
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

        /// <summary>-0.20 => target takes *0.8 (multiplicative stacking)</summary>
        public void AddDefenderResistPercent(DamageType t, float percent)
        {
            EnsureArrays();
            ApplyPercent(ref defenderResistMultByType[Idx(t)], percent);
        }

        /// <summary>Undo a previous AddDefenderResistPercent(t, percent)</summary>
        public void RemoveDefenderResistPercent(DamageType t, float percent)
        {
            EnsureArrays();
            RemovePercent(ref defenderResistMultByType[Idx(t)], percent);
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

        /// <summary>-0.20 => attacker deals *0.8 (multiplicative stacking)</summary>
        public void AddAttackerWeakenPercent(DamageType t, float percent)
        {
            EnsureArrays();
            ApplyPercent(ref attackerWeakenMultByType[Idx(t)], percent);
        }

        /// <summary>Undo a previous AddAttackerWeakenPercent(t, percent)</summary>
        public void RemoveAttackerWeakenPercent(DamageType t, float percent)
        {
            EnsureArrays();
            RemovePercent(ref attackerWeakenMultByType[Idx(t)], percent);
        }

        // -------------------------
        // Type flat REMOVE helpers (NEW)
        // -------------------------
        public void RemoveAttackerBonusFlat(DamageType t, int value)
        {
            EnsureArrays();
            attackerBonusFlatByType[Idx(t)] -= value;
        }

        public void RemoveDefenderVulnFlat(DamageType t, int value)
        {
            EnsureArrays();
            defenderVulnFlatByType[Idx(t)] -= value;
        }

        public void RemoveDefenderResistFlat(DamageType t, int value)
        {
            EnsureArrays();
            defenderResistFlatByType[Idx(t)] -= value;
        }

        public void RemoveAttackerWeakenFlat(DamageType t, int value)
        {
            EnsureArrays();
            attackerWeakenFlatByType[Idx(t)] -= value;
        }

        // Utility: apply signed percent as multiplicative factor.
        private static void ApplyPercent(ref float mult, float percent)
        {
            float f = 1f + percent;
            if (f <= 0f)
                return;
            mult *= f;
        }

        private static void RemovePercent(ref float mult, float percent)
        {
            float f = 1f + percent;
            if (f <= 0f)
                return;
            mult /= f;
        }

        public void ResetAll()
        {
            EnsureArrays();

            // Flats
            damageFlat = 0;
            magicDamageFlat = 0;
            attackDamageFlat = 0;
            meleeDamageBonusFlat = 0;
            rangedDamageBonusFlat = 0;

            powerScalingFlat = 0f;
            attackPowerScalingFlat = 0f;
            magicPowerScalingFlat = 0f;

            spellBaseFlat = 0;
            magicSpellBaseFlat = 0;
            physicalSpellBaseFlat = 0;

            // Mults
            magicDamageMult = 1f;
            physicalDamageMult = 1f;
            damageMult = 1f;
            meleeDamageBonusMult = 1f;
            rangedDamageBonusMult = 1f;

            spellBaseMult = 1f;
            magicSpellBaseMult = 1f;
            physicalSpellBaseMult = 1f;

            attackPowerScalingMult = 1f;
            magicPowerScalingMult = 1f;
            powerScalingMult = 1f;


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
        public float DamageMultFinal => damageMult;
        public float PhysicalDamageMultFinal => physicalDamageMult;
        public float MagicDamageMultFinal => magicDamageMult;

        public float PowerScalingMultFinal => powerScalingMult;
        public float AttackPowerScalingMultFinal => attackPowerScalingMult;
        public float MagicPowerScalingMultFinal => magicPowerScalingMult;

        public float SpellBaseMultFinal => spellBaseMult;
        public float MagicSpellBaseMultFinal => magicSpellBaseMult;
        public float PhysicalSpellBaseMultFinal => physicalSpellBaseMult;
    }
}
