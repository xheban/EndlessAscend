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

        // Spell-specific defense bypass
        public int ignoreDefenseFlat;
        public int ignoreDefensePercent; // 0..100
        public int hitChance;
        public int baseUseSpeed;
        public int level;

        // Needed for scaling rules now / resistances later
        public DamageKind damageKind;
        public DamageType[] damageTypes;
        public EffectInstance[] onHitEffects;
        public EffectInstance[] onCastEffects;
        public SpellIntent intent;

        public ResolvedSpell(
            string spellId,
            string displayName,
            int manaCost,
            int cooldownTurns,
            int damage,
            int ignoreDefenseFlat,
            int ignoreDefensePercent,
            int hitChance,
            int baseUseSpeed,
            int level,
            DamageKind damageKind,
            DamageType[] damageTypes,
            EffectInstance[] onHitEffects,
            EffectInstance[] onCastEffects,
            SpellIntent intent
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
            this.onHitEffects = onHitEffects;
            this.onCastEffects = onCastEffects;
            this.intent = intent;
            this.level = level;
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
