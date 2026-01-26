using System.Collections.Generic;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using UnityEngine;

namespace MyGame.Common
{
    public static class BaseStatMap
    {
        public static int Get(in Stats s, BaseStatType stat) =>
            stat switch
            {
                BaseStatType.Strength => s.strength,
                BaseStatType.Agility => s.agility,
                BaseStatType.Intelligence => s.intelligence,
                BaseStatType.Spirit => s.spirit,
                BaseStatType.Endurance => s.endurance,
                _ => 0,
            };

        public static void Set(ref Stats s, BaseStatType stat, int value)
        {
            switch (stat)
            {
                case BaseStatType.Strength:
                    s.strength = value;
                    break;
                case BaseStatType.Agility:
                    s.agility = value;
                    break;
                case BaseStatType.Intelligence:
                    s.intelligence = value;
                    break;
                case BaseStatType.Spirit:
                    s.spirit = value;
                    break;
                case BaseStatType.Endurance:
                    s.endurance = value;
                    break;
            }
        }
    }

    public static class BaseStatModifierApplier
    {
        /// <summary>
        /// Applies base stat modifiers with: flat added, plus percent calculated from the base value.
        /// Percent modifiers are summed (not multiplicative).
        /// </summary>
        public static Stats ApplyAll(Stats baseStats, IEnumerable<BaseStatModifier> mods)
        {
            if (mods == null)
                return baseStats;

            int flatStr = 0,
                flatAgi = 0,
                flatInt = 0,
                flatSpr = 0,
                flatEnd = 0;
            int pctStr = 0,
                pctAgi = 0,
                pctInt = 0,
                pctSpr = 0,
                pctEnd = 0;

            foreach (var m in mods)
            {
                switch (m.stat)
                {
                    case BaseStatType.Strength:
                        if (m.op == ModOp.Flat)
                            flatStr += m.value;
                        else if (m.op == ModOp.Percent)
                            pctStr += m.value;
                        break;

                    case BaseStatType.Agility:
                        if (m.op == ModOp.Flat)
                            flatAgi += m.value;
                        else if (m.op == ModOp.Percent)
                            pctAgi += m.value;
                        break;

                    case BaseStatType.Intelligence:
                        if (m.op == ModOp.Flat)
                            flatInt += m.value;
                        else if (m.op == ModOp.Percent)
                            pctInt += m.value;
                        break;

                    case BaseStatType.Spirit:
                        if (m.op == ModOp.Flat)
                            flatSpr += m.value;
                        else if (m.op == ModOp.Percent)
                            pctSpr += m.value;
                        break;

                    case BaseStatType.Endurance:
                        if (m.op == ModOp.Flat)
                            flatEnd += m.value;
                        else if (m.op == ModOp.Percent)
                            pctEnd += m.value;
                        break;
                }
            }

            baseStats.strength = Apply(baseStats.strength, flatStr, pctStr);
            baseStats.agility = Apply(baseStats.agility, flatAgi, pctAgi);
            baseStats.intelligence = Apply(baseStats.intelligence, flatInt, pctInt);
            baseStats.spirit = Apply(baseStats.spirit, flatSpr, pctSpr);
            baseStats.endurance = Apply(baseStats.endurance, flatEnd, pctEnd);

            return baseStats;
        }

        private static int Apply(int baseValue, int flatAdd, int pctAdd)
        {
            // pctAdd is whole percent (10 => +10%). Round to nearest int.
            int pctFromBase = (baseValue * pctAdd + (pctAdd >= 0 ? 50 : -50)) / 100;
            int outV = baseValue + flatAdd + pctFromBase;
            return outV < 0 ? 0 : outV;
        }
    }

    public static class PlayerBaseStatsResolver
    {
        /// <summary>
        /// Returns the effective base stats used for derived-stat calculations.
        /// Includes class/spec baseStatMods and equipped rolledBaseStatMods.
        /// Does NOT mutate the save.
        /// </summary>
        public static Stats BuildEffectiveBaseStats(
            SaveData save,
            PlayerEquipment equipmentOverride = null
        )
        {
            if (save == null)
                return default;

            return BuildEffectiveBaseStats(save, save.finalStats, equipmentOverride);
        }

        /// <summary>
        /// Same as <see cref="BuildEffectiveBaseStats(SaveData, PlayerEquipment)"/> but lets callers provide
        /// the base stats to start from (ex: a UI preview with allocated points).
        /// </summary>
        public static Stats BuildEffectiveBaseStats(
            SaveData save,
            Stats baseStatsOverride,
            PlayerEquipment equipmentOverride = null
        )
        {
            if (save == null)
                return default;

            var stats = baseStatsOverride;

            // Collect all base-stat modifiers (class/spec + equipped rolls) so percent bonuses are
            // calculated from the same base values.
            var combined = new List<BaseStatModifier>(64);

            // Apply class/spec base-stat modifiers.
            var classDb =
                GameConfigProvider.Instance != null
                    ? GameConfigProvider.Instance.PlayerClassDatabase
                    : null;
            if (classDb != null)
            {
                var classSo = classDb.GetClass(save.classId);
                if (classSo?.baseStatMods != null && classSo.baseStatMods.Count > 0)
                    combined.AddRange(classSo.baseStatMods);

                var specSo = classDb.GetSpec(save.specId);
                if (specSo?.baseStatMods != null && specSo.baseStatMods.Count > 0)
                    combined.AddRange(specSo.baseStatMods);
            }

            // Apply equipped base-stat rolls.
            var equipment =
                equipmentOverride
                ?? RunSession.Equipment
                ?? InventorySaveMapper.LoadEquipmentFromSave(save);
            if (equipment != null)
            {
                foreach (var inst in equipment.GetEquippedInstances())
                {
                    if (inst?.rolledBaseStatMods == null || inst.rolledBaseStatMods.Count == 0)
                        continue;

                    combined.AddRange(inst.rolledBaseStatMods);
                }
            }

            if (combined.Count == 0)
                return stats;

            return BaseStatModifierApplier.ApplyAll(stats, combined);
        }
    }
}
