using MyGame.Common;
using MyGame.Spells;

namespace MyGame.Combat
{
    public sealed class SpellDatabaseCombatResolver : ICombatSpellResolver
    {
        private readonly SpellDatabase _db;

        public SpellDatabaseCombatResolver(SpellDatabase db)
        {
            _db = db;
        }

        public bool TryResolve(
            string spellId,
            int spellLevel,
            in DerivedCombatStats casterStats,
            out ResolvedSpell resolved
        )
        {
            resolved = null;

            if (_db == null)
                return false;

            SpellDefinition def = _db.GetById(spellId);
            if (def == null)
                return false;

            int baseSpellDamage = def.GetDamageAtLevel(spellLevel);

            int finalDamage = baseSpellDamage;
            if (finalDamage < 0)
                finalDamage = 0;

            int ignoreFlat = def.ignoreDefenseFlat < 0 ? 0 : def.ignoreDefenseFlat;
            int ignorePct = def.ignoreDefensePercent;
            if (ignorePct < 0)
                ignorePct = 0;
            if (ignorePct > 100)
                ignorePct = 100;

            resolved = new ResolvedSpell(
                spellId: def.spellId,
                displayName: string.IsNullOrWhiteSpace(def.displayName)
                    ? def.spellId
                    : def.displayName,
                manaCost: def.manaCost,
                cooldownTurns: def.cooldownTurns,
                damage: finalDamage,
                damageKind: def.damageKind,
                ignoreDefenseFlat: ignoreFlat,
                ignoreDefensePercent: ignorePct,
                hitChance: def.hitChance,
                damageTypes: def.damageTag,
                baseUseSpeed: def.baseUseSpeed
            );

            return true;
        }
    }
}
