using MyGame.Combat;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;

namespace MyGame.Common
{
    public static class PlayerDerivedStatsResolver
    {
        /// <summary>
        /// Returns the effective derived combat stats used for vitals/advanced stats.
        /// Includes class/spec derivedStatMods and equipped rolledDerivedStatMods.
        /// Does NOT mutate the save.
        /// </summary>
        public static DerivedCombatStats BuildEffectiveDerivedStats(
            SaveData save,
            PlayerEquipment equipmentOverride = null
        )
        {
            if (save == null)
                return default;

            var effectiveBaseStats = PlayerBaseStatsResolver.BuildEffectiveBaseStats(
                save,
                equipmentOverride
            );

            return BuildDerivedFromEffectiveBaseStats(save, effectiveBaseStats, equipmentOverride);
        }

        /// <summary>
        /// Same as <see cref="BuildEffectiveDerivedStats(SaveData, PlayerEquipment)"/> but lets callers provide
        /// the base stats to start from (ex: a UI preview with allocated points).
        /// </summary>
        public static DerivedCombatStats BuildEffectiveDerivedStats(
            SaveData save,
            Stats baseStatsOverride,
            PlayerEquipment equipmentOverride = null
        )
        {
            if (save == null)
                return default;

            var effectiveBaseStats = PlayerBaseStatsResolver.BuildEffectiveBaseStats(
                save,
                baseStatsOverride,
                equipmentOverride
            );

            return BuildDerivedFromEffectiveBaseStats(save, effectiveBaseStats, equipmentOverride);
        }

        /// <summary>
        /// Builds derived combat stats from already-effective base stats (i.e. base stats with base-stat bonuses applied).
        /// Then applies derived stat modifiers from class/spec and equipped rolls.
        /// </summary>
        public static DerivedCombatStats BuildDerivedFromEffectiveBaseStats(
            SaveData save,
            Stats effectiveBaseStats,
            PlayerEquipment equipmentOverride = null
        )
        {
            if (save == null)
                return default;

            // Base derived values from effective base stats.
            var derived = CombatStatCalculator.CalculateAll(
                effectiveBaseStats,
                save.level,
                save.tier
            );

            // Apply class/spec derived modifiers so these numbers match combat.
            var classDb =
                GameConfigProvider.Instance != null
                    ? GameConfigProvider.Instance.PlayerClassDatabase
                    : null;

            if (classDb != null)
            {
                var classSo = classDb.GetClass(save.classId);
                if (classSo?.derivedStatMods != null && classSo.derivedStatMods.Count > 0)
                    DerivedModifierApplier.ApplyAll(ref derived, classSo.derivedStatMods);

                var specSo = classDb.GetSpec(save.specId);
                if (specSo?.derivedStatMods != null && specSo.derivedStatMods.Count > 0)
                    DerivedModifierApplier.ApplyAll(ref derived, specSo.derivedStatMods);
            }

            // Apply equipped rolled derived modifiers.
            var equipment =
                equipmentOverride
                ?? RunSession.Equipment
                ?? InventorySaveMapper.LoadEquipmentFromSave(save);

            if (equipment != null)
            {
                foreach (var inst in equipment.GetEquippedInstances())
                {
                    if (
                        inst?.rolledDerivedStatMods == null
                        || inst.rolledDerivedStatMods.Count == 0
                    )
                        continue;

                    DerivedModifierApplier.ApplyAll(ref derived, inst.rolledDerivedStatMods);
                }
            }

            return derived;
        }
    }
}
