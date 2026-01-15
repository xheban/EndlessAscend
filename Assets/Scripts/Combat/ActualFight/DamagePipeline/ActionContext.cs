using System.Collections.Generic;

namespace MyGame.Combat
{
    /// <summary>
    /// Mutable "working memory" for resolving one action (one spell use / attack) from attacker to defender.
    /// Phases will read/write fields here.
    /// </summary>
    public sealed class ActionContext
    {
        // Inputs (who, who, what)
        public CombatActorState attacker;
        public CombatActorState defender;
        public ResolvedSpell spell;
        public DamageKind spellDamageKind;
        public int lastDamageDealt;
        public int lastDamageDealtPower;

        // Shared services
        public IRng rng;

        // -------------------
        // Phase 1: Hit
        // -------------------
        public int hitChance; // start from 100%, modifiers reduce/increase
        public bool hit; // final rolled result

        // -------------------
        // Phase 2: Damage
        // -------------------
        public int baseDamage; // e.g. spell.damage
        public int flatDamageBonus; // additive bonuses/penalties
        public float damageMult = 1f; // multiplicative bonuses/penalties
        public int finalDamage; // computed final
        public int effectiveDefense;

        // -------------------
        // Phase 3: Effects
        // -------------------
        public int spellLevel;
        public EffectInstance[] effectInstancesToApply;
    }

    /// <summary>
    /// Represents one effect that we attempt to apply during the Effects phase.
    /// This does not apply the effect yet — it's the working record for rolls & outcomes.
    /// </summary>
    public sealed class EffectAttempt
    {
        public string effectId;

        // common knobs (you can expand later)
        public bool requiresHit = true;
        public float applyChance = 1f; // 0..1
        public float resistChance = 0f; // 0..1 (later: computed from stats)

        // -------------------
        // Phase 2: Damage
        // -------------------
        public int baseDamage; // starts from spell.damage
        public int flatDamageBonus; // additive bonuses/penalties
        public float damageMult = 1f; // multiplicative bonuses/penalties
        public int finalDamage; // computed final

        // results
        public bool attempted;
        public bool applied;
        public bool resisted;

        // optional numeric payload (duration, stacks, magnitude)
        public float magnitude;
    }
}
