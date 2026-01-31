using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedEquipmentInstance
    {
        public string instanceId; // GUID string
        public string equipmentId;

        // Starter: save rolled base stats explicitly.
        public List<BaseStatModifier> rolledBaseStatMods = new();

        // Rolled derived stats (Flat / Percent)
        public List<DerivedStatModifier> rolledDerivedStatMods = new();

        // Rolled spell-combat modifiers (damage kind/type/range bonuses)
        public List<CombatStatModifier> rolledCombatStatMods = new();

        // Rolled spell-variable overrides (DamageKind / DamageType / IgnoreDefense)
        public List<SpellVariableOverride> rolledSpellOverrides = new();
    }
}
