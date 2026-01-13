namespace MyGame.Combat
{
    /// <summary>
    /// Runtime resolved spell data used by CombatEngine.
    /// Computed from SpellDefinition + caster stats.
    /// </summary>
    public sealed class ResolvedSpell
    {
        public string spellId;
        public string displayName;

        public int manaCost;
        public int cooldownTurns;

        // Step 2: keep it simple (direct damage only)
        public int damage;

        // Needed for scaling rules now / resistances later
        public DamageKind damageKind;
        public DamageType[] damageTypes;

        // Spell-specific defense bypass
        public int ignoreDefenseFlat;
        public int ignoreDefensePercent; // 0..100
        public int hitChance;
        public int baseUseSpeed;

        public ResolvedSpell(
            string spellId,
            string displayName,
            int manaCost,
            int cooldownTurns,
            int damage,
            DamageKind damageKind,
            DamageType[] damageTypes,
            int ignoreDefenseFlat,
            int ignoreDefensePercent,
            int hitChance,
            int baseUseSpeed
        )
        {
            this.spellId = spellId;
            this.displayName = displayName;
            this.manaCost = manaCost;
            this.cooldownTurns = cooldownTurns;
            this.damage = damage;

            this.damageKind = damageKind;
            this.ignoreDefenseFlat = ignoreDefenseFlat;
            this.ignoreDefensePercent = ignoreDefensePercent;
            this.hitChance = hitChance;
            this.damageTypes = damageTypes;
            this.baseUseSpeed = baseUseSpeed;
        }
    }

    /// <summary>
    /// Converts (spellId, level, caster stats) -> runtime spell values.
    /// </summary>
    public interface ICombatSpellResolver
    {
        bool TryResolve(
            string spellId,
            int spellLevel,
            in DerivedCombatStats casterStats,
            out ResolvedSpell resolved
        );
    }
}
