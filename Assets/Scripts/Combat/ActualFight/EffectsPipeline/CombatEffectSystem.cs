using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class CombatEffectSystem
    {
        // You already created an EffectDatabase. Inject it here.
        private readonly EffectDatabase _db;

        public CombatEffectSystem(EffectDatabase db)
        {
            _db = db;
        }

        public readonly struct PeriodicTickResult
        {
            public readonly CombatActorType source;
            public readonly CombatActorType target;
            public readonly EffectKind kind;
            public readonly int amount;
            public readonly string effectId;

            public PeriodicTickResult(
                CombatActorType source,
                CombatActorType target,
                EffectKind kind,
                int amount,
                string effectId
            )
            {
                this.source = source;
                this.target = target;
                this.kind = kind;
                this.amount = amount;
                this.effectId = effectId;
            }
        }

        // =========================
        // PUBLIC API
        // =========================

        /// <summary>
        /// Called when an actor CHOOSES an action (your definition of "new turn").
        /// Ticks periodic effects on that actor and decrements durations.
        /// </summary>
        public List<PeriodicTickResult> OnActionChosen(CombatState state, CombatActorType actorType)
        {
            var results = new List<PeriodicTickResult>();

            if (state == null || state.isFinished)
                return results;

            var owner = state.Get(actorType);
            if (owner == null)
                return results;

            TickAndExpireEffects(owner, results);
            return results;
        }

        public void ApplyEffectInstance(
            CombatActorState attacker,
            CombatActorState defender,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance inst,
            IRng rng,
            int lastDamageDealt
        )
        {
            if (
                _db == null
                || attacker == null
                || defender == null
                || spell == null
                || inst == null
                || inst.effect == null
            )
                return;

            var def = inst.effect;
            if (string.IsNullOrWhiteSpace(def.effectId))
                return;

            // choose target based on instance setting
            CombatActorState target = inst.target == EffectTarget.Self ? attacker : defender;

            target.activeEffects ??= new List<ActiveEffectState>();

            // Cache caster power once per call (you can optimize later)
            int casterFinalAP = ComputeFinalAttackPower(attacker);
            int casterFinalMP = ComputeFinalMagicPower(attacker);

            var scaled = inst.GetScaled(spellLevel);

            // ✅ guard: basis-based stat modifiers must be Flat
            if (
                def.kind == EffectKind.StatModifier
                && scaled.magnitudeBasis != EffectMagnitudeBasis.None
                && def.op != EffectOp.Flat
            )
            {
                Debug.LogWarning(
                    $"[Effects] Invalid config: StatModifier '{def.effectId}' uses basis '{scaled.magnitudeBasis}' but op is '{def.op}'. Basis-based must be Flat."
                );
                return;
            }

            if (scaled.chancePercent <= 0)
                return;

            int roll = rng.RangeInt(1, 101);
            if (roll > scaled.chancePercent)
                return;

            // periodic contribution only for DOT/HOT
            int computedPeriodicContribution = 0;
            if (def.kind == EffectKind.DamageOverTime || def.kind == EffectKind.HealOverTime)
            {
                computedPeriodicContribution = ComputeInitialPeriodicTick(
                    def: def,
                    scaled: scaled,
                    spell: spell,
                    casterFinalAP: casterFinalAP,
                    casterFinalMP: casterFinalMP,
                    lastDamageDealt: lastDamageDealt
                );
            }

            // materialize basis for stat modifiers (if you implemented that earlier)
            int materializedFlatFromBasis = 0;
            if (
                def.kind == EffectKind.StatModifier
                && scaled.magnitudeBasis != EffectMagnitudeBasis.None
            )
            {
                int basisValue = ComputeBasisValue(
                    basis: scaled.magnitudeBasis,
                    spell: spell,
                    casterFinalAP: casterFinalAP,
                    casterFinalMP: casterFinalMP,
                    lastDamageDealt: lastDamageDealt
                );

                float pct = Mathf.Clamp(scaled.magnitudePercent, 0, 100) / 100f;
                materializedFlatFromBasis = Mathf.RoundToInt(basisValue * pct);
            }

            ApplyResult apply = AddOrRefreshEffect(
                list: target.activeEffects,
                def: def,
                sourceActor: attacker.actorType,
                sourceSpellId: spell.spellId,
                scaled: scaled,
                computedPeriodicContribution: computedPeriodicContribution,
                materializedFlatFromBasis: materializedFlatFromBasis
            );

            // StatModifier apply/undo against TARGET modifiers
            if (def.kind == EffectKind.StatModifier)
            {
                if (apply.overwritten)
                {
                    UndoModifierDelta(
                        target.modifiers,
                        def,
                        apply.oldTotalsFlat,
                        apply.oldTotalsPercent,
                        apply.oldBasis
                    );
                }

                if (apply.addedFlat != 0 || apply.addedPercent != 0)
                {
                    ApplyModifierDelta(
                        target.modifiers,
                        def,
                        apply.addedFlat,
                        apply.addedPercent,
                        scaled.magnitudeBasis
                    );
                }
            }
        }

        /// <summary>
        /// Apply effects produced by a spell to the defender (or attacker if your spell targets self).
        /// This implementation assumes you pass the correct target (attacker/defender) from the phase.
        /// </summary>
        public void ApplyEffects(
            CombatActorState attacker,
            CombatActorState defender,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance[] instances,
            IRng rng,
            int lastDamageDealt
        )
        {
            if (instances == null || instances.Length == 0)
                return;

            for (int i = 0; i < instances.Length; i++)
            {
                ApplyEffectInstance(
                    attacker: attacker,
                    defender: defender,
                    spell: spell,
                    spellLevel: spellLevel,
                    inst: instances[i],
                    rng: rng,
                    lastDamageDealt: lastDamageDealt
                );
            }
        }

        // =========================
        // INTERNALS
        // =========================

        private struct ApplyResult
        {
            public bool createdNew;
            public bool overwritten;

            // what we added THIS application (delta)
            public int addedFlat;
            public int addedPercent;

            // for overwrite undo
            public int oldTotalsFlat;
            public int oldTotalsPercent;
            public EffectMagnitudeBasis oldBasis;
        }

        private ApplyResult AddOrRefreshEffect(
            List<ActiveEffectState> list,
            EffectDefinition def,
            CombatActorType sourceActor,
            string sourceSpellId,
            EffectInstanceScaledIntValues scaled,
            int computedPeriodicContribution,
            int materializedFlatFromBasis
        )
        {
            ApplyResult result = default;

            ActiveEffectState existing = FindById(list, def.effectId);

            // ---------
            // CREATE NEW
            // ---------
            if (existing == null)
            {
                var state = new ActiveEffectState(
                    effectId: def.effectId,
                    durationTurns: scaled.durationTurns,
                    stackable: scaled.stackable,
                    maxStacks: scaled.maxStacks,
                    sourceActor: sourceActor,
                    sourceSpellId: sourceSpellId,
                    kind: def.kind
                );

                // totals start at first contribution
                int baseFlat = Mathf.Max(0, scaled.magnitudeFlat);

                // ✅ If basis is used for StatModifier, we convert percent-of-basis into flat once.
                // We store it in totalMagnitudeFlat so undo is exact later.
                int appliedFlat = baseFlat;
                int appliedPercent = Mathf.Max(0, scaled.magnitudePercent);

                if (
                    def.kind == EffectKind.StatModifier
                    && scaled.magnitudeBasis != EffectMagnitudeBasis.None
                )
                {
                    appliedFlat += Mathf.Max(0, materializedFlatFromBasis);
                    appliedPercent = 0; // ✅ percent was consumed into flat
                }

                state.totalMagnitudeFlat = appliedFlat;
                state.totalMagnitudePercent = appliedPercent;
                state.magnitudeBasis = scaled.magnitudeBasis;

                state.periodicTickContributions ??= new List<int>();

                if (def.kind == EffectKind.DamageOverTime || def.kind == EffectKind.HealOverTime)
                {
                    int tick = Mathf.Max(0, computedPeriodicContribution);
                    state.periodicTickContributions.Add(tick);
                    state.stacks =
                        state.periodicTickContributions != null
                            ? Mathf.Max(1, state.periodicTickContributions.Count)
                            : 1;
                }

                list.Add(state);
                result.createdNew = true;
                result.addedFlat = state.totalMagnitudeFlat;
                result.addedPercent = state.totalMagnitudePercent;
                return result;
            }

            // Always apply duration rule first
            ApplyDurationStacking(existing, scaled.durationTurns, def);

            // Then apply reapply rule
            switch (def.reapplyRule)
            {
                case EffectReapplyRule.DoNothingIfPresent:
                {
                    // duration may have changed, but no magnitude changes
                    return result;
                }

                case EffectReapplyRule.AddOnTop:
                {
                    // If not stackable, treat like "refresh-only"
                    if (!scaled.stackable)
                        return result;

                    int beforeStacks = existing.stacks;
                    int newStacks = Mathf.Min(existing.stacks + 1, Mathf.Max(1, scaled.maxStacks));
                    existing.stacks = newStacks;

                    int gained = existing.stacks - beforeStacks;
                    if (gained <= 0)
                        return result; // at max stacks, no growth

                    // ✅ Accumulate totals (NO overwrite)
                    int addFlat = Mathf.Max(0, scaled.magnitudeFlat);
                    int addPct = Mathf.Max(0, scaled.magnitudePercent);

                    if (
                        def.kind == EffectKind.StatModifier
                        && scaled.magnitudeBasis != EffectMagnitudeBasis.None
                    )
                    {
                        addFlat += Mathf.Max(0, materializedFlatFromBasis);
                        addPct = 0; // ✅ consumed into flat
                    }

                    existing.totalMagnitudeFlat += addFlat;
                    existing.totalMagnitudePercent += addPct;

                    result.addedFlat = addFlat;
                    result.addedPercent = addPct;

                    // basis handling
                    if (existing.magnitudeBasis == EffectMagnitudeBasis.None)
                        existing.magnitudeBasis = scaled.magnitudeBasis;

                    // periodic: add the computed contribution
                    existing.periodicTickContributions ??= new List<int>();

                    int tick = Mathf.Max(0, computedPeriodicContribution);
                    existing.periodicTickContributions.Add(tick);
                    existing.stacks = Mathf.Max(1, existing.periodicTickContributions.Count);

                    // update snapshot config
                    existing.stackable = true;
                    existing.maxStacks = Mathf.Max(existing.maxStacks, scaled.maxStacks);
                    existing.sourceActor = sourceActor;
                    existing.sourceSpellId = sourceSpellId;

                    result.addedFlat = addFlat;
                    result.addedPercent = addPct;
                    return result;
                }

                case EffectReapplyRule.OverwriteIfStronger:
                {
                    bool stronger = IsNewStronger(existing, def, scaled);
                    if (!stronger)
                        return result;

                    // capture old totals for undo if needed (StatModifier)
                    result.overwritten = true;
                    result.oldTotalsFlat = existing.totalMagnitudeFlat;
                    result.oldTotalsPercent = existing.totalMagnitudePercent;
                    result.oldBasis = existing.magnitudeBasis;

                    // overwrite everything
                    existing.stacks = 1;
                    existing.totalMagnitudeFlat = Mathf.Max(0, scaled.magnitudeFlat);
                    existing.totalMagnitudePercent = Mathf.Max(0, scaled.magnitudePercent);
                    existing.magnitudeBasis = scaled.magnitudeBasis;

                    existing.periodicTickContributions ??= new List<int>();
                    existing.periodicTickContributions.Clear();

                    int tick = Mathf.Max(0, computedPeriodicContribution);
                    existing.periodicTickContributions.Add(tick);
                    existing.stacks = Mathf.Max(1, existing.periodicTickContributions.Count);

                    existing.stackable = scaled.stackable;
                    existing.maxStacks = Mathf.Max(1, scaled.maxStacks);

                    existing.sourceActor = sourceActor;
                    existing.sourceSpellId = sourceSpellId;

                    result.addedFlat = existing.totalMagnitudeFlat;
                    result.addedPercent = existing.totalMagnitudePercent;
                    return result;
                }
            }

            return result;
        }

        private static ActiveEffectState FindById(List<ActiveEffectState> list, string effectId)
        {
            if (list == null || string.IsNullOrWhiteSpace(effectId))
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && e.effectId == effectId)
                    return e;
            }
            return null;
        }

        private static void ApplyDurationStacking(
            ActiveEffectState existing,
            int newDuration,
            EffectDefinition def
        )
        {
            if (existing == null || def == null)
                return;

            int d = Mathf.Max(1, newDuration);

            switch (def.durationStackMode)
            {
                case DurationStackMode.None:
                    return;

                case DurationStackMode.Prolong:
                    existing.remainingTurns += d;
                    return;

                case DurationStackMode.Refresh:
                default:
                    if (def.refreshOverridesRemaining)
                        existing.remainingTurns = d;
                    else
                        existing.remainingTurns = Mathf.Max(existing.remainingTurns, d);
                    return;
            }
        }

        private bool IsNewStronger(
            ActiveEffectState existing,
            EffectDefinition newDef,
            EffectInstanceScaledIntValues scaled
        )
        {
            if (existing == null || newDef == null)
                return true;

            var oldDef = _db.GetById(existing.effectId);
            if (oldDef == null)
                return true;

            switch (newDef.compareMode)
            {
                case EffectStrengthCompareMode.ByStrengthRating:
                    return newDef.strengthRating > oldDef.strengthRating;

                case EffectStrengthCompareMode.ByComputedMagnitude:
                default:
                    int oldScore = existing.totalMagnitudeFlat + existing.totalMagnitudePercent;

                    int newScore =
                        Mathf.Max(0, scaled.magnitudeFlat) + Mathf.Max(0, scaled.magnitudePercent);

                    return newScore > oldScore;
            }
        }

        private static int ComputeBasisValue(
            EffectMagnitudeBasis basis,
            ResolvedSpell spell,
            int casterFinalAP,
            int casterFinalMP,
            int lastDamageDealt
        )
        {
            return basis switch
            {
                EffectMagnitudeBasis.Power => (spell.damageKind == DamageKind.Magical)
                    ? casterFinalMP
                    : casterFinalAP,

                EffectMagnitudeBasis.DamageDealt => Mathf.Max(0, lastDamageDealt),

                _ => 0,
            };
        }

        /// <summary>
        /// Compute initial periodic tick amount ONCE and store it into ActiveEffectState.
        /// </summary>
        private static int ComputeInitialPeriodicTick(
            EffectDefinition def,
            EffectInstanceScaledIntValues scaled,
            ResolvedSpell spell,
            int casterFinalAP,
            int casterFinalMP,
            int lastDamageDealt
        )
        {
            int flat = Mathf.Max(0, scaled.magnitudeFlat);
            float pct = Mathf.Clamp(scaled.magnitudePercent, 0, 100) / 100f;

            int basisValue;

            switch (scaled.magnitudeBasis)
            {
                case EffectMagnitudeBasis.Power:
                    basisValue =
                        (spell.damageKind == DamageKind.Magical) ? casterFinalMP : casterFinalAP;
                    break;

                case EffectMagnitudeBasis.DamageDealt:
                    basisValue = Mathf.Max(0, lastDamageDealt);
                    break;

                case EffectMagnitudeBasis.None:
                default:
                    basisValue = 0;
                    break;
            }

            int percentPart = Mathf.RoundToInt(basisValue * pct);
            int total = flat + percentPart;

            return Mathf.Max(0, total);
        }

        private void TickAndExpireEffects(CombatActorState owner, List<PeriodicTickResult> results)
        {
            if (owner == null || owner.activeEffects == null)
                return;

            for (int i = owner.activeEffects.Count - 1; i >= 0; i--)
            {
                var e = owner.activeEffects[i];
                if (e == null)
                {
                    owner.activeEffects.RemoveAt(i);
                    continue;
                }

                // 1) Periodic tick -> produce a result (do NOT change HP here)
                int tickTotal = 0;
                if (e.periodicTickContributions != null)
                {
                    for (int k = 0; k < e.periodicTickContributions.Count; k++)
                        tickTotal += Mathf.Max(0, e.periodicTickContributions[k]);
                }

                if (
                    tickTotal > 0
                    && (e.kind == EffectKind.DamageOverTime || e.kind == EffectKind.HealOverTime)
                )
                {
                    results?.Add(
                        new PeriodicTickResult(
                            source: e.sourceActor,
                            target: owner.actorType,
                            kind: e.kind,
                            amount: tickTotal,
                            effectId: e.effectId
                        )
                    );
                }

                // 2) Duration decrement
                e.remainingTurns--;

                // 3) Expire
                if (e.remainingTurns <= 0)
                {
                    owner.activeEffects.RemoveAt(i);
                    if (e.kind == EffectKind.StatModifier && owner.modifiers != null && _db != null)
                    {
                        var def = _db.GetById(e.effectId);
                        if (def != null)
                        {
                            UndoModifierDelta(
                                owner.modifiers,
                                def,
                                e.totalMagnitudeFlat,
                                e.totalMagnitudePercent,
                                e.magnitudeBasis
                            );
                        }
                    }

                    // StatModifier undo fix comes later (next bug)
                }
            }
        }

        // =========================
        // Power computation (caster stats)
        // =========================

        private static int ComputeFinalAttackPower(CombatActorState actor)
        {
            if (actor == null)
                return 0;

            var d = actor.derived;
            var m = actor.modifiers;

            float value = d.attackPower + m.attackPowerFlat + m.powerFlat;
            value *= m.PowerMultFinal;
            value *= m.AttackPowerMultFinal;

            return Mathf.Max(0, Mathf.RoundToInt(value));
        }

        private static int ComputeFinalMagicPower(CombatActorState actor)
        {
            if (actor == null)
                return 0;

            var d = actor.derived;
            var m = actor.modifiers;

            float value = d.magicPower + m.magicPowerFlat + m.powerFlat;
            value *= m.PowerMultFinal;
            value *= m.MagicPowerMultFinal;

            return Mathf.Max(0, Mathf.RoundToInt(value));
        }

        // =========================
        // Stat modifier application/undo (minimal placeholders)
        // You already have StatModifiers class; wire your exact mapping here.
        // =========================

        private static void ApplyModifierDelta(
            StatModifiers mods,
            EffectDefinition def,
            int addFlat,
            int addPercent,
            EffectMagnitudeBasis basis
        )
        {
            if (mods == null || def == null)
                return;

            // Example: only implementing "damageFlat" as a demo
            // Extend this to your full stat/op system.
            if (def.stat == EffectStat.DamageAll && def.op == EffectOp.Flat)
            {
                mods.damageFlat += addFlat;
            }
            if (def.stat == EffectStat.DamageAll && def.op == EffectOp.MorePercent)
            {
                mods.AddDamageMorePercent(addPercent / 100f);
            }
        }

        private static void UndoModifierDelta(
            StatModifiers mods,
            EffectDefinition def,
            int oldFlat,
            int oldPercent,
            EffectMagnitudeBasis oldBasis
        )
        {
            if (mods == null || def == null)
                return;

            // Example undo matching ApplyModifierDelta demo above
            if (def.stat == EffectStat.DamageAll && def.op == EffectOp.Flat)
            {
                mods.damageFlat -= oldFlat;
            }
            if (def.stat == EffectStat.DamageAll && def.op == EffectOp.MorePercent)
            {
                mods.RemoveDamageMorePercent(oldPercent / 100f);
            }
        }
    }
}
