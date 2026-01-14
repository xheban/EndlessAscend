using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class CombatEffectSystem
    {
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

            Debug.Log("aplying instances of effects");
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

            // Choose target based on instance setting
            CombatActorState target = inst.target == EffectTarget.Self ? attacker : defender;
            target.activeEffects ??= new List<ActiveEffectState>();

            var scaled = inst.GetScaled(spellLevel);

            if (scaled.chancePercent <= 0)
                return;

            int roll = rng.RangeInt(1, 101);
            Debug.Log("effect rolled for hit:" + roll);
            if (roll > scaled.chancePercent)
                return;

            // Cache caster power once per call
            int casterFinalAP = ComputeFinalAttackPower(attacker);
            int casterFinalMP = ComputeFinalMagicPower(attacker);

            // Guard: basis-based stat modifiers must be Flat (we materialize percent-of-basis into flat)
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

            // Periodic tick contribution only for DOT/HOT
            int computedPeriodicContribution = 0;
            if (def.kind == EffectKind.DamageOverTime || def.kind == EffectKind.HealOverTime)
            {
                computedPeriodicContribution = ComputeInitialPeriodicTick(
                    scaled: scaled,
                    spell: spell,
                    casterFinalAP: casterFinalAP,
                    casterFinalMP: casterFinalMP,
                    lastDamageDealt: lastDamageDealt
                );

                Debug.Log("computed periodic damge is: " + computedPeriodicContribution);
            }

            // Materialize basis for stat modifiers: percent-of-basis becomes flat ON APPLY
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

            // Apply to runtime state, receive the delta that must be applied to StatModifiers NOW
            ApplyDelta delta = AddOrRefreshEffect(
                list: target.activeEffects,
                def: def,
                inst: inst,
                sourceActor: attacker.actorType,
                sourceSpellId: spell.spellId,
                scaled: scaled,
                computedPeriodicContribution: computedPeriodicContribution,
                materializedFlatFromBasis: materializedFlatFromBasis
            );

            // Apply StatModifier delta to target modifiers
            if (def.kind == EffectKind.StatModifier && target.modifiers != null)
            {
                if (delta.deltaFlat != 0 || delta.deltaPercent != 0)
                {
                    ApplyModifierDelta(
                        mods: target.modifiers,
                        def: def,
                        addFlat: delta.deltaFlat,
                        addPercent: delta.deltaPercent
                    );
                }
            }
        }

        // =========================
        // INTERNALS: MERGE + STACK + DELTAS
        // =========================

        private readonly struct ApplyDelta
        {
            public readonly int deltaFlat;
            public readonly int deltaPercent;

            public ApplyDelta(int deltaFlat, int deltaPercent)
            {
                this.deltaFlat = deltaFlat;
                this.deltaPercent = deltaPercent;
            }
        }

        private ApplyDelta AddOrRefreshEffect(
            List<ActiveEffectState> list,
            EffectDefinition def,
            EffectInstance inst,
            CombatActorType sourceActor,
            string sourceSpellId,
            EffectInstanceScaledIntValues scaled,
            int computedPeriodicContribution,
            int materializedFlatFromBasis
        )
        {
            if (list == null || def == null || inst == null)
                return default;
            Debug.Log("adding or refreshing effect");
            // 1) Find/create bucket:
            // - mergeable => one bucket per effectId
            // - non-mergeable => separate bucket per source
            ActiveEffectState bucket = FindOrCreateBucket(
                list: list,
                effectId: def.effectId,
                kind: def.kind,
                mergeable: scaled.mergeable, // ✅ FINAL PATCH: use scaled snapshot
                sourceActor: sourceActor,
                sourceSpellId: sourceSpellId,
                scaled: scaled
            );

            Debug.Log("bucket is : " + bucket.effectId);

            bucket.EnsureContributionBacked();

            // 2) Find contribution in bucket for this source
            var contrib = FindContribution(bucket, sourceActor, sourceSpellId);

            bool createdNewContribution = false;
            if (contrib == null)
            {
                contrib = new ActiveEffectState.EffectContributionState
                {
                    sourceActor = sourceActor,
                    sourceSpellId = sourceSpellId,
                    remainingTurns = Mathf.Max(1, scaled.durationTurns),
                    stacks = 1,
                    stackable = scaled.stackable,
                    maxStacks = Mathf.Max(1, scaled.maxStacks),

                    totalMagnitudeFlat = 0,
                    totalMagnitudePercent = 0,
                    magnitudeBasis = scaled.magnitudeBasis,

                    periodicTickContributions = new List<int>(),
                };

                bucket.contributions.Add(contrib);
                createdNewContribution = true;
            }

            // 3) Apply duration rule per contribution (even if DoNothingIfPresent)
            if (!createdNewContribution)
            {
                ApplyDurationStacking(contrib, scaled.durationTurns, def);
            }
            // 4) Compute base magnitudes for THIS application (one stack contribution)
            int addFlat = Mathf.Max(0, scaled.magnitudeFlat);
            int addPct = Mathf.Max(0, scaled.magnitudePercent);

            // StatModifier + basis: consume percent into flat once
            if (
                def.kind == EffectKind.StatModifier
                && scaled.magnitudeBasis != EffectMagnitudeBasis.None
            )
            {
                addFlat += Mathf.Max(0, materializedFlatFromBasis);
                addPct = 0;
            }

            // 5) Creation: always apply first contribution
            if (createdNewContribution)
            {
                contrib.totalMagnitudeFlat = addFlat;
                contrib.totalMagnitudePercent = addPct;
                contrib.magnitudeBasis = scaled.magnitudeBasis;

                if (def.kind == EffectKind.DamageOverTime || def.kind == EffectKind.HealOverTime)
                {
                    int tick = Mathf.Max(0, computedPeriodicContribution);
                    contrib.periodicTickContributions.Add(tick);
                    contrib.stacks = Mathf.Max(1, contrib.periodicTickContributions.Count);
                }
                else
                {
                    contrib.stacks = 1;
                }

                bucket.RebuildLegacyFromContributions();
                return new ApplyDelta(addFlat, addPct);
            }

            // 6) Reapply rules (apply to THIS source contribution)
            switch (def.reapplyRule)
            {
                case EffectReapplyRule.DoNothingIfPresent:
                {
                    bucket.RebuildLegacyFromContributions();
                    return default;
                }

                case EffectReapplyRule.AddOnTop:
                {
                    if (!scaled.stackable || !contrib.stackable)
                    {
                        bucket.RebuildLegacyFromContributions();
                        return default;
                    }

                    int maxStacks = Mathf.Max(1, Mathf.Min(contrib.maxStacks, scaled.maxStacks));
                    int beforeStacks = contrib.stacks;

                    int nextStacks = Mathf.Min(beforeStacks + 1, maxStacks);
                    int gained = nextStacks - beforeStacks;

                    if (gained <= 0)
                    {
                        bucket.RebuildLegacyFromContributions();
                        return default;
                    }

                    contrib.stacks = nextStacks;
                    contrib.maxStacks = maxStacks;

                    // StatModifier totals scale with stacks (store exact totals)
                    contrib.totalMagnitudeFlat += addFlat;
                    contrib.totalMagnitudePercent += addPct;

                    // DOT/HOT: add tick contribution for new stack
                    if (
                        def.kind == EffectKind.DamageOverTime
                        || def.kind == EffectKind.HealOverTime
                    )
                    {
                        int tick = Mathf.Max(0, computedPeriodicContribution);
                        contrib.periodicTickContributions.Add(tick);
                        contrib.stacks = Mathf.Max(1, contrib.periodicTickContributions.Count);
                    }

                    bucket.RebuildLegacyFromContributions();
                    return new ApplyDelta(addFlat, addPct);
                }

                case EffectReapplyRule.OverwriteIfStronger:
                {
                    bool stronger = IsNewStronger_Contribution(contrib, def, scaled);
                    if (!stronger)
                    {
                        bucket.RebuildLegacyFromContributions();
                        return default;
                    }

                    int oldFlat = contrib.totalMagnitudeFlat;
                    int oldPct = contrib.totalMagnitudePercent;

                    contrib.stacks = 1;
                    contrib.stackable = scaled.stackable;
                    contrib.maxStacks = Mathf.Max(1, scaled.maxStacks);

                    contrib.totalMagnitudeFlat = addFlat;
                    contrib.totalMagnitudePercent = addPct;
                    contrib.magnitudeBasis = scaled.magnitudeBasis;

                    contrib.periodicTickContributions ??= new List<int>();
                    contrib.periodicTickContributions.Clear();

                    if (
                        def.kind == EffectKind.DamageOverTime
                        || def.kind == EffectKind.HealOverTime
                    )
                    {
                        int tick = Mathf.Max(0, computedPeriodicContribution);
                        contrib.periodicTickContributions.Add(tick);
                        contrib.stacks = Mathf.Max(1, contrib.periodicTickContributions.Count);
                    }

                    bucket.RebuildLegacyFromContributions();
                    return new ApplyDelta(
                        contrib.totalMagnitudeFlat - oldFlat,
                        contrib.totalMagnitudePercent - oldPct
                    );
                }
            }

            bucket.RebuildLegacyFromContributions();
            return default;
        }

        private static ActiveEffectState FindOrCreateBucket(
            List<ActiveEffectState> list,
            string effectId,
            EffectKind kind,
            bool mergeable,
            CombatActorType sourceActor,
            string sourceSpellId,
            EffectInstanceScaledIntValues scaled
        )
        {
            ActiveEffectState found = null;

            if (mergeable)
            {
                // one bucket per effectId
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e != null && e.effectId == effectId)
                    {
                        found = e;
                        break;
                    }
                }
            }
            else
            {
                // separate bucket per source
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null || e.effectId != effectId)
                        continue;

                    e.EnsureContributionBacked();
                    var c = FindContribution(e, sourceActor, sourceSpellId);
                    if (c != null)
                    {
                        found = e;
                        break;
                    }
                }
            }

            if (found != null)
                return found;

            var created = new ActiveEffectState(
                effectId: effectId,
                durationTurns: Mathf.Max(1, scaled.durationTurns),
                stackable: scaled.stackable,
                maxStacks: Mathf.Max(1, scaled.maxStacks),
                sourceActor: sourceActor,
                sourceSpellId: sourceSpellId,
                kind: kind
            );

            created.EnsureContributionBacked();
            list.Add(created);
            return created;
        }

        private static ActiveEffectState.EffectContributionState FindContribution(
            ActiveEffectState bucket,
            CombatActorType sourceActor,
            string sourceSpellId
        )
        {
            if (bucket == null || bucket.contributions == null)
                return null;

            for (int i = 0; i < bucket.contributions.Count; i++)
            {
                var c = bucket.contributions[i];
                if (c != null && c.sourceActor == sourceActor && c.sourceSpellId == sourceSpellId)
                    return c;
            }

            return null;
        }

        private static void ApplyDurationStacking(
            ActiveEffectState.EffectContributionState c,
            int newDuration,
            EffectDefinition def
        )
        {
            if (c == null || def == null)
                return;

            int d = Mathf.Max(1, newDuration);

            switch (def.durationStackMode)
            {
                case DurationStackMode.None:
                    return;

                case DurationStackMode.Prolong:
                    c.remainingTurns += d;
                    return;

                case DurationStackMode.Refresh:
                default:
                    if (def.refreshOverridesRemaining)
                        c.remainingTurns = d;
                    else
                        c.remainingTurns = Mathf.Max(c.remainingTurns, d);
                    return;
            }
        }

        private bool IsNewStronger_Contribution(
            ActiveEffectState.EffectContributionState existingContribution,
            EffectDefinition newDef,
            EffectInstanceScaledIntValues scaled
        )
        {
            if (existingContribution == null || newDef == null)
                return true;

            var oldDef = _db != null ? _db.GetById(newDef.effectId) : null;
            if (oldDef == null)
            {
                int oldScoreFallback =
                    existingContribution.totalMagnitudeFlat
                    + existingContribution.totalMagnitudePercent;
                int newScoreFallback =
                    Mathf.Max(0, scaled.magnitudeFlat) + Mathf.Max(0, scaled.magnitudePercent);
                return newScoreFallback > oldScoreFallback;
            }

            switch (newDef.compareMode)
            {
                case EffectStrengthCompareMode.ByStrengthRating:
                    return newDef.strengthRating > oldDef.strengthRating;

                case EffectStrengthCompareMode.ByComputedMagnitude:
                default:
                    int oldScore =
                        existingContribution.totalMagnitudeFlat
                        + existingContribution.totalMagnitudePercent;
                    int newScore =
                        Mathf.Max(0, scaled.magnitudeFlat) + Mathf.Max(0, scaled.magnitudePercent);
                    return newScore > oldScore;
            }
        }

        // =========================
        // TICK + EXPIRE (per contribution)
        // =========================

        private void TickAndExpireEffects(CombatActorState owner, List<PeriodicTickResult> results)
        {
            if (owner == null || owner.activeEffects == null)
                return;

            for (int i = owner.activeEffects.Count - 1; i >= 0; i--)
            {
                var bucket = owner.activeEffects[i];
                if (bucket == null)
                {
                    owner.activeEffects.RemoveAt(i);
                    continue;
                }

                bucket.EnsureContributionBacked();

                int bucketTickTotal = 0;

                for (int cIndex = bucket.contributions.Count - 1; cIndex >= 0; cIndex--)
                {
                    var c = bucket.contributions[cIndex];
                    if (c == null)
                    {
                        bucket.contributions.RemoveAt(cIndex);
                        continue;
                    }

                    // Tick periodic for this contribution
                    if (
                        bucket.kind == EffectKind.DamageOverTime
                        || bucket.kind == EffectKind.HealOverTime
                    )
                    {
                        if (c.periodicTickContributions != null)
                        {
                            for (int k = 0; k < c.periodicTickContributions.Count; k++)
                                bucketTickTotal += Mathf.Max(0, c.periodicTickContributions[k]);
                        }
                    }

                    // Decrement duration
                    c.remainingTurns--;

                    // Expire contribution
                    if (c.remainingTurns <= 0)
                    {
                        // Undo stat modifiers for THIS contribution only
                        if (
                            bucket.kind == EffectKind.StatModifier
                            && owner.modifiers != null
                            && _db != null
                        )
                        {
                            var def = _db.GetById(bucket.effectId);
                            if (def != null)
                            {
                                UndoModifierDelta(
                                    mods: owner.modifiers,
                                    def: def,
                                    oldFlat: c.totalMagnitudeFlat,
                                    oldPercent: c.totalMagnitudePercent
                                );
                            }
                        }

                        bucket.contributions.RemoveAt(cIndex);
                    }
                }

                // Remove empty bucket
                if (bucket.contributions.Count == 0)
                {
                    owner.activeEffects.RemoveAt(i);
                    continue;
                }

                // Rebuild summary fields
                bucket.RebuildLegacyFromContributions();

                // Emit merged tick once per bucket
                if (
                    bucketTickTotal > 0
                    && (
                        bucket.kind == EffectKind.DamageOverTime
                        || bucket.kind == EffectKind.HealOverTime
                    )
                )
                {
                    results?.Add(
                        new PeriodicTickResult(
                            source: bucket.sourceActor, // summary source (first contribution)
                            target: owner.actorType,
                            kind: bucket.kind,
                            amount: bucketTickTotal,
                            effectId: bucket.effectId
                        )
                    );
                }
            }
        }

        // =========================
        // Basis + periodic math
        // =========================

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

        private static int ComputeInitialPeriodicTick(
            EffectInstanceScaledIntValues scaled,
            ResolvedSpell spell,
            int casterFinalAP,
            int casterFinalMP,
            int lastDamageDealt
        )
        {
            int flat = Mathf.Max(0, scaled.magnitudeFlat);
            float pct = Mathf.Clamp(scaled.magnitudePercent, 0, 100) / 100f;

            int basisValue = scaled.magnitudeBasis switch
            {
                EffectMagnitudeBasis.Power => (spell.damageKind == DamageKind.Magical)
                    ? casterFinalMP
                    : casterFinalAP,
                EffectMagnitudeBasis.DamageDealt => Mathf.Max(0, lastDamageDealt),
                _ => 0,
            };

            int percentPart = Mathf.RoundToInt(basisValue * pct);
            return Mathf.Max(0, flat + percentPart);
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
        // Stat modifier application/undo (mapping to StatModifiers)
        // =========================

        private static void ApplyModifierDelta(
            StatModifiers mods,
            EffectDefinition def,
            int addFlat,
            int addPercent
        )
        {
            if (mods == null || def == null)
                return;

            float pct = Mathf.Clamp(addPercent, 0, 100) / 100f;

            switch (def.stat)
            {
                case EffectStat.None:
                    return;

                // Damage
                case EffectStat.DamageAll:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.damageFlat += v,
                        addMoreAction: p => mods.AddDamageMorePercent(p),
                        addLessAction: p => mods.AddDamageLessPercent(p)
                    );
                    return;

                case EffectStat.DamagePhysical:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.attackDamageFlat += v,
                        addMoreAction: p => mods.AddPhysicalDamageMorePercent(p),
                        addLessAction: p => mods.AddPhysicalDamageLessPercent(p)
                    );
                    return;

                case EffectStat.DamageMagic:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.magicDamageFlat += v,
                        addMoreAction: p => mods.AddMagicDamageMorePercent(p),
                        addLessAction: p => mods.AddMagicDamageLessPercent(p)
                    );
                    return;

                // Power
                case EffectStat.PowerAll:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.powerFlat += v,
                        addMoreAction: p => mods.AddPowerMorePercent(p),
                        addLessAction: p => mods.AddPowerLessPercent(p)
                    );
                    return;

                case EffectStat.PowerAttack:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.attackPowerFlat += v,
                        addMoreAction: p => mods.AddAttackPowerMorePercent(p),
                        addLessAction: p => mods.AddAttackPowerLessPercent(p)
                    );
                    return;

                case EffectStat.PowerMagic:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.magicPowerFlat += v,
                        addMoreAction: p => mods.AddMagicPowerMorePercent(p),
                        addLessAction: p => mods.AddMagicPowerLessPercent(p)
                    );
                    return;

                // Spell base damage
                case EffectStat.SpellBaseAll:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddSpellBaseFlat(v),
                        addMoreAction: p => mods.AddSpellBaseMorePercent(p),
                        addLessAction: p => mods.AddSpellBaseLessPercent(p)
                    );
                    return;

                case EffectStat.SpellBasePhysical:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddPhysicalSpellBaseFlat(v),
                        addMoreAction: p => mods.AddPhysicalSpellBaseMorePercent(p),
                        addLessAction: p => mods.AddPhysicalSpellBaseLessPercent(p)
                    );
                    return;

                case EffectStat.SpellBaseMagic:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddMagicSpellBaseFlat(v),
                        addMoreAction: p => mods.AddMagicSpellBaseMorePercent(p),
                        addLessAction: p => mods.AddMagicSpellBaseLessPercent(p)
                    );
                    return;

                // Hit / speeds
                case EffectStat.HitChance:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: null,
                        addMoreAction: p => mods.AddHitChanceMorePercent(p),
                        addLessAction: p => mods.AddHitChanceLessPercent(p)
                    );
                    return;

                case EffectStat.AttackSpeed:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.attackSpeedFlat += v,
                        addMoreAction: p => mods.AddAttackSpeedMorePercent(p),
                        addLessAction: p => mods.AddAttackSpeedLessPercent(p)
                    );
                    return;

                case EffectStat.CastingSpeed:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.castingSpeedFlat += v,
                        addMoreAction: p => mods.AddCastingSpeedMorePercent(p),
                        addLessAction: p => mods.AddCastingSpeedLessPercent(p)
                    );
                    return;

                // Defence
                case EffectStat.DefenceAll:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.defenceFlat += v,
                        addMoreAction: p => mods.AddDefenceMorePercent(p),
                        addLessAction: p => mods.AddDefenceLessPercent(p)
                    );
                    return;

                case EffectStat.DefencePhysical:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.physicalDefenseFlat += v,
                        addMoreAction: p => mods.AddPhysicalDefenceMorePercent(p),
                        addLessAction: p => mods.AddPhysicalDefenceLessPercent(p)
                    );
                    return;

                case EffectStat.DefenceMagic:
                    ApplyFlatOrMult(
                        def.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.magicDefenseFlat += v,
                        addMoreAction: p => mods.AddMagicDefenceMorePercent(p),
                        addLessAction: p => mods.AddMagicDefenceLessPercent(p)
                    );
                    return;

                // Type-based layers (uses def.damageType[])
                case EffectStat.AttackerBonusByType:
                    ApplyTypeBased(def, mods, addFlat, pct, TypeMode.AttackerBonus);
                    return;

                case EffectStat.DefenderVulnerabilityByType:
                    ApplyTypeBased(def, mods, addFlat, pct, TypeMode.DefenderVuln);
                    return;

                case EffectStat.AttackerWeakenByType:
                    ApplyTypeBased(def, mods, addFlat, pct, TypeMode.AttackerWeaken);
                    return;

                case EffectStat.DefenderResistByType:
                    ApplyTypeBased(def, mods, addFlat, pct, TypeMode.DefenderResist);
                    return;
            }
        }

        private static void UndoModifierDelta(
            StatModifiers mods,
            EffectDefinition def,
            int oldFlat,
            int oldPercent
        )
        {
            if (mods == null || def == null)
                return;

            float pct = Mathf.Clamp(oldPercent, 0, 100) / 100f;

            switch (def.stat)
            {
                case EffectStat.None:
                    return;

                case EffectStat.DamageAll:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.damageFlat -= v,
                        removeMoreAction: p => mods.RemoveDamageMorePercent(p),
                        removeLessAction: p => mods.RemoveDamageLessPercent(p)
                    );
                    return;

                case EffectStat.DamagePhysical:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.attackDamageFlat -= v,
                        removeMoreAction: p => mods.RemovePhysicalDamageMorePercent(p),
                        removeLessAction: p => mods.RemovePhysicalDamageLessPercent(p)
                    );
                    return;

                case EffectStat.DamageMagic:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.magicDamageFlat -= v,
                        removeMoreAction: p => mods.RemoveMagicDamageMorePercent(p),
                        removeLessAction: p => mods.RemoveMagicDamageLessPercent(p)
                    );
                    return;

                case EffectStat.PowerAll:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.powerFlat -= v,
                        removeMoreAction: p => mods.RemovePowerMorePercent(p),
                        removeLessAction: p => mods.RemovePowerLessPercent(p)
                    );
                    return;

                case EffectStat.PowerAttack:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.attackPowerFlat -= v,
                        removeMoreAction: p => mods.RemoveAttackPowerMorePercent(p),
                        removeLessAction: p => mods.RemoveAttackPowerLessPercent(p)
                    );
                    return;

                case EffectStat.PowerMagic:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.magicPowerFlat -= v,
                        removeMoreAction: p => mods.RemoveMagicPowerMorePercent(p),
                        removeLessAction: p => mods.RemoveMagicPowerLessPercent(p)
                    );
                    return;

                case EffectStat.SpellBaseAll:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddSpellBaseFlat(-v),
                        removeMoreAction: p => mods.spellBaseMult.RemoveMore(p),
                        removeLessAction: p => mods.spellBaseMult.RemoveLess(p)
                    );
                    return;

                case EffectStat.SpellBasePhysical:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddPhysicalSpellBaseFlat(-v),
                        removeMoreAction: p => mods.physicalSpellBaseMult.RemoveMore(p),
                        removeLessAction: p => mods.physicalSpellBaseMult.RemoveLess(p)
                    );
                    return;

                case EffectStat.SpellBaseMagic:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddMagicSpellBaseFlat(-v),
                        removeMoreAction: p => mods.magicSpellBaseMult.RemoveMore(p),
                        removeLessAction: p => mods.magicSpellBaseMult.RemoveLess(p)
                    );
                    return;

                case EffectStat.HitChance:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: null,
                        removeMoreAction: p => mods.RemoveHitChanceMorePercent(p),
                        removeLessAction: p => mods.RemoveHitChanceLessPercent(p)
                    );
                    return;

                case EffectStat.AttackSpeed:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.attackSpeedFlat -= v,
                        removeMoreAction: p => mods.RemoveAttackSpeedMorePercent(p),
                        removeLessAction: p => mods.RemoveAttackSpeedLessPercent(p)
                    );
                    return;

                case EffectStat.CastingSpeed:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.castingSpeedFlat -= v,
                        removeMoreAction: p => mods.RemoveCastingSpeedMorePercent(p),
                        removeLessAction: p => mods.RemoveCastingSpeedLessPercent(p)
                    );
                    return;

                case EffectStat.DefenceAll:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.defenceFlat -= v,
                        removeMoreAction: p => mods.RemoveDefenceMorePercent(p),
                        removeLessAction: p => mods.RemoveDefenceLessPercent(p)
                    );
                    return;

                case EffectStat.DefencePhysical:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.physicalDefenseFlat -= v,
                        removeMoreAction: p => mods.RemovePhysicalDefenceMorePercent(p),
                        removeLessAction: p => mods.RemovePhysicalDefenceLessPercent(p)
                    );
                    return;

                case EffectStat.DefenceMagic:
                    UndoFlatOrMult(
                        def.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.magicDefenseFlat -= v,
                        removeMoreAction: p => mods.RemoveMagicDefenceMorePercent(p),
                        removeLessAction: p => mods.RemoveMagicDefenceLessPercent(p)
                    );
                    return;

                case EffectStat.AttackerBonusByType:
                    UndoTypeBased(def, mods, oldFlat, pct, TypeMode.AttackerBonus);
                    return;

                case EffectStat.DefenderVulnerabilityByType:
                    UndoTypeBased(def, mods, oldFlat, pct, TypeMode.DefenderVuln);
                    return;

                case EffectStat.AttackerWeakenByType:
                    UndoTypeBased(def, mods, oldFlat, pct, TypeMode.AttackerWeaken);
                    return;

                case EffectStat.DefenderResistByType:
                    UndoTypeBased(def, mods, oldFlat, pct, TypeMode.DefenderResist);
                    return;
            }
        }

        private static void ApplyFlatOrMult(
            EffectOp op,
            int addFlat,
            float pct,
            System.Action<int> addFlatAction,
            System.Action<float> addMoreAction,
            System.Action<float> addLessAction
        )
        {
            switch (op)
            {
                case EffectOp.Flat:
                    addFlatAction?.Invoke(addFlat);
                    return;
                case EffectOp.MorePercent:
                    addMoreAction?.Invoke(pct);
                    return;
                case EffectOp.LessPercent:
                    addLessAction?.Invoke(pct);
                    return;
            }
        }

        private static void UndoFlatOrMult(
            EffectOp op,
            int oldFlat,
            float pct,
            System.Action<int> removeFlatAction,
            System.Action<float> removeMoreAction,
            System.Action<float> removeLessAction
        )
        {
            switch (op)
            {
                case EffectOp.Flat:
                    removeFlatAction?.Invoke(oldFlat);
                    return;
                case EffectOp.MorePercent:
                    removeMoreAction?.Invoke(pct);
                    return;
                case EffectOp.LessPercent:
                    removeLessAction?.Invoke(pct);
                    return;
            }
        }

        private enum TypeMode
        {
            AttackerBonus,
            DefenderVuln,
            DefenderResist,
            AttackerWeaken,
        }

        private static void ApplyTypeBased(
            EffectDefinition def,
            StatModifiers mods,
            int addFlat,
            float pct,
            TypeMode mode
        )
        {
            if (def.damageType == null || def.damageType.Length == 0)
                return;

            for (int i = 0; i < def.damageType.Length; i++)
            {
                var t = def.damageType[i];

                switch (mode)
                {
                    case TypeMode.AttackerBonus:
                        if (def.op == EffectOp.Flat)
                            mods.AddAttackerBonusFlat(t, addFlat);
                        else if (def.op == EffectOp.MorePercent)
                            mods.AddAttackerBonusMorePercent(t, pct);
                        break;

                    case TypeMode.DefenderVuln:
                        if (def.op == EffectOp.Flat)
                            mods.AddDefenderVulnFlat(t, addFlat);
                        else if (def.op == EffectOp.MorePercent)
                            mods.AddDefenderVulnMorePercent(t, pct);
                        break;

                    case TypeMode.DefenderResist:
                        if (def.op == EffectOp.Flat)
                            mods.AddDefenderResistFlat(t, addFlat);
                        else if (def.op == EffectOp.LessPercent)
                            mods.AddDefenderResistLessPercent(t, pct);
                        break;

                    case TypeMode.AttackerWeaken:
                        if (def.op == EffectOp.Flat)
                            mods.AddAttackerWeakenFlat(t, addFlat);
                        else if (def.op == EffectOp.LessPercent)
                            mods.AddAttackerWeakenLessPercent(t, pct);
                        break;
                }
            }
        }

        private static void UndoTypeBased(
            EffectDefinition def,
            StatModifiers mods,
            int oldFlat,
            float pct,
            TypeMode mode
        )
        {
            if (def.damageType == null || def.damageType.Length == 0)
                return;

            for (int i = 0; i < def.damageType.Length; i++)
            {
                var t = def.damageType[i];

                switch (mode)
                {
                    case TypeMode.AttackerBonus:
                        if (def.op == EffectOp.Flat)
                            mods.RemoveAttackerBonusFlat(t, oldFlat);
                        else if (def.op == EffectOp.MorePercent)
                            mods.RemoveAttackerBonusMorePercent(t, pct);
                        break;

                    case TypeMode.DefenderVuln:
                        if (def.op == EffectOp.Flat)
                            mods.RemoveDefenderVulnFlat(t, oldFlat);
                        else if (def.op == EffectOp.MorePercent)
                            mods.RemoveDefenderVulnMorePercent(t, pct);
                        break;

                    case TypeMode.DefenderResist:
                        if (def.op == EffectOp.Flat)
                            mods.RemoveDefenderResistFlat(t, oldFlat);
                        else if (def.op == EffectOp.LessPercent)
                            mods.RemoveDefenderResistLessPercent(t, pct);
                        break;

                    case TypeMode.AttackerWeaken:
                        if (def.op == EffectOp.Flat)
                            mods.RemoveAttackerWeakenFlat(t, oldFlat);
                        else if (def.op == EffectOp.LessPercent)
                            mods.RemoveAttackerWeakenLessPercent(t, pct);
                        break;
                }
            }
        }
    }
}
