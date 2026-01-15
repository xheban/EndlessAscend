using System;
using System.Collections.Generic;
using MyGame.Common;
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
            public readonly string effectName;

            public PeriodicTickResult(
                CombatActorType source,
                CombatActorType target,
                EffectKind kind,
                int amount,
                string effectId,
                string effectName
            )
            {
                this.source = source;
                this.target = target;
                this.kind = kind;
                this.amount = amount;
                this.effectId = effectId;
                this.effectName = effectName;
            }
        }

        private struct EffectApplyResult
        {
            public ActiveEffect effect;
            public EffectContributor addedContributor; // null if none added
        }

        public void ApplyEffects(
            CombatActorState attacker,
            CombatActorState defender,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance[] instances,
            int lastDamageDealt,
            int finalPower
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
                    lastDamageDealt: lastDamageDealt,
                    finalPower: finalPower
                );
            }
        }

        public void ApplyEffectInstance(
            CombatActorState attacker,
            CombatActorState defender,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance inst,
            int lastDamageDealt,
            int finalPower
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
            {
                return;
            }

            // Target is based on polarity (buffs go on attacker, debuffs on defender)
            CombatActorState target =
                inst.effect.polarity == EffectPolarity.Buff ? attacker : defender;

            // Find existing effect on the target (used by multiple rules)
            ActiveEffect existing = ActiveEffect.FindEffectById(
                target.activeEffects,
                inst.effect.effectId
            );

            switch (inst.reapplyRule)
            {
                case EffectReapplyRule.AddOnTop:
                {
                    var result = AddStackMergeEffect(
                        attacker,
                        defender,
                        spell,
                        spellLevel,
                        inst,
                        lastDamageDealt,
                        finalPower
                    );
                    // Apply duration stacking ONLY if this is NOT the first contributor
                    if (result.effect != null && result.effect.contributors.Count > 1)
                        ApplyDurationStacking(
                            result.effect,
                            inst,
                            spellLevel,
                            result.addedContributor
                        );

                    break;
                }

                case EffectReapplyRule.OverwriteIfStronger:
                {
                    // If not present, just apply normally
                    if (existing == null)
                    {
                        AddStackMergeEffect(
                            attacker,
                            defender,
                            spell,
                            spellLevel,
                            inst,
                            lastDamageDealt,
                            finalPower
                        );
                        break;
                    }
                    if (inst.compareMode == EffectStrengthCompareMode.ByStrengthRating)
                    {
                        bool incomingStronger =
                            ActiveEffect.ReturnTotalStrength(existing)
                            < inst.strengthRating * spellLevel;

                        if (incomingStronger)
                        {
                            UndoAllStatContributorsIfNeeded(target, existing);
                            existing.contributors.Clear();
                            var result = CreateEffectContributor(
                                sourceSpellId: spell.spellId,
                                sourceActor: attacker.actorType,
                                durationTurns: inst.GetScaled(spellLevel).durationTurns,
                                totalTickValue: CalculateEffectTick(
                                    inst.magnitudeBasis,
                                    inst.GetScaled(spellLevel).magnitudeFlat,
                                    inst.GetScaled(spellLevel).magnitudePercent,
                                    finalPower,
                                    lastDamageDealt
                                ),
                                strengthRating: inst.strengthRating * spellLevel,
                                inst.removeWhenType
                            );
                            existing.contributors.Add(result);
                            break;
                        }
                    }
                    else if (inst.compareMode == EffectStrengthCompareMode.ByComputedMagnitude)
                    {
                        bool incomingStronger =
                            existing.TotalDamage
                            < CalculateEffectTick(
                                inst.magnitudeBasis,
                                inst.GetScaled(spellLevel).magnitudeFlat,
                                inst.GetScaled(spellLevel).magnitudePercent,
                                finalPower,
                                lastDamageDealt
                            );

                        if (incomingStronger)
                        {
                            UndoAllStatContributorsIfNeeded(target, existing);
                            existing.contributors.Clear();
                            var result = CreateEffectContributor(
                                sourceSpellId: spell.spellId,
                                sourceActor: attacker.actorType,
                                durationTurns: inst.GetScaled(spellLevel).durationTurns,
                                totalTickValue: CalculateEffectTick(
                                    inst.magnitudeBasis,
                                    inst.GetScaled(spellLevel).magnitudeFlat,
                                    inst.GetScaled(spellLevel).magnitudePercent,
                                    finalPower,
                                    lastDamageDealt
                                ),
                                strengthRating: inst.strengthRating * spellLevel,
                                inst.removeWhenType
                            );
                            existing.contributors.Add(result);
                            break;
                        }
                    }
                    break;
                }
                case EffectReapplyRule.DoNothingIfPresent:
                {
                    // If present -> do nothing
                    if (existing != null)
                        break;
                    // Otherwise apply (first contributor)
                    AddStackMergeEffect(
                        attacker,
                        defender,
                        spell,
                        spellLevel,
                        inst,
                        lastDamageDealt,
                        finalPower
                    );
                    break;
                }
            }
        }

        private EffectApplyResult AddStackMergeEffect(
            CombatActorState attacker,
            CombatActorState defender,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance inst,
            int lastDamageDealt,
            int finalPower
        )
        {
            CombatActorState target =
                inst.effect.polarity == EffectPolarity.Buff ? attacker : defender;

            var effects = target.activeEffects;
            var existing = ActiveEffect.FindEffectById(effects, inst.effect.effectId);
            var scaledInfo = inst.GetScaled(spellLevel);

            if (existing != null)
            {
                int contributorCount = ActiveEffect.CountContributorsFromSpellId(
                    existing,
                    spell.spellId
                );
                // STACK has priority
                if (contributorCount > 0)
                {
                    if (inst.stackable && contributorCount < scaledInfo.maxStacks)
                    {
                        var contributor = CreateEffectContributor(
                            sourceSpellId: spell.spellId,
                            sourceActor: attacker.actorType,
                            durationTurns: scaledInfo.durationTurns,
                            strengthRating: inst.strengthRating * spellLevel,
                            totalTickValue: CalculateEffectTick(
                                inst.magnitudeBasis,
                                inst.GetScaled(spellLevel).magnitudeFlat,
                                inst.GetScaled(spellLevel).magnitudePercent,
                                finalPower,
                                lastDamageDealt
                            ),
                            removedWhenType: inst.removeWhenType
                        );

                        if (IsStatModifier(inst.effect))
                        {
                            GetSignedStatDeltas(
                                inst.effect,
                                inst,
                                spellLevel,
                                finalPower,
                                lastDamageDealt,
                                out int flat,
                                out int pct
                            );
                            ApplyStatModifierNow(target, inst.effect, contributor, flat, pct);
                        }

                        existing.contributors.Add(contributor);

                        return new EffectApplyResult
                        {
                            effect = existing,
                            addedContributor = contributor,
                        };
                    }
                }
                // MERGE
                else
                {
                    if (inst.mergeable)
                    {
                        var contributor = CreateEffectContributor(
                            sourceSpellId: spell.spellId,
                            sourceActor: attacker.actorType,
                            durationTurns: scaledInfo.durationTurns,
                            strengthRating: inst.strengthRating * spellLevel,
                            totalTickValue: CalculateEffectTick(
                                inst.magnitudeBasis,
                                inst.GetScaled(spellLevel).magnitudeFlat,
                                inst.GetScaled(spellLevel).magnitudePercent,
                                finalPower,
                                lastDamageDealt
                            ),
                            removedWhenType: inst.removeWhenType
                        );
                        if (IsStatModifier(inst.effect))
                        {
                            GetSignedStatDeltas(
                                inst.effect,
                                inst,
                                spellLevel,
                                finalPower,
                                lastDamageDealt,
                                out int flat,
                                out int pct
                            );
                            ApplyStatModifierNow(target, inst.effect, contributor, flat, pct);
                        }

                        existing.contributors.Add(contributor);

                        return new EffectApplyResult
                        {
                            effect = existing,
                            addedContributor = contributor,
                        };
                    }
                }

                return new EffectApplyResult { effect = existing };
            }
            else
            {
                var newEffect = CreateActiveEffect(
                    spellLevel: spellLevel,
                    spell: spell,
                    inst: inst,
                    attacker: attacker,
                    target: target,
                    lastDamageDealt: lastDamageDealt,
                    finalPower: finalPower
                );

                effects.Add(newEffect);

                return new EffectApplyResult
                {
                    effect = newEffect,
                    addedContributor = newEffect.contributors[0],
                };
            }
        }

        private static void ApplyDurationStacking(
            ActiveEffect effect,
            EffectInstance inst,
            int spellLevel,
            EffectContributor newContributor
        )
        {
            if (effect == null || inst == null)
                return;

            var scaled = inst.GetScaled(spellLevel);
            switch (inst.durationStackMode)
            {
                case DurationStackMode.Refresh:
                    for (int i = 0; i < effect.contributors.Count; i++)
                    {
                        var c = effect.contributors[i];

                        if (inst.refreshOverridesRemaining)
                            c.remainingTurns = scaled.durationTurns;
                        else
                            c.remainingTurns = Mathf.Max(c.remainingTurns, scaled.durationTurns);
                    }
                    break;

                case DurationStackMode.Prolong:
                    var max = 0;
                    for (int i = 0; i < effect.contributors.Count; i++)
                    {
                        if (ReferenceEquals(effect.contributors[i], newContributor))
                        {
                            continue;
                        }
                        effect.contributors[i].remainingTurns += scaled.durationTurns;
                        max = Math.Max(max, effect.contributors[i].remainingTurns);
                    }
                    if (newContributor != null)
                        newContributor.remainingTurns = max;
                    break;

                case DurationStackMode.None:
                    // intentionally do nothing
                    break;
            }
        }

        public EffectContributor CreateEffectContributor(
            string sourceSpellId,
            CombatActorType sourceActor,
            int durationTurns,
            int totalTickValue,
            int strengthRating,
            RemoveWhenType removedWhenType
        )
        {
            return new EffectContributor
            {
                sourceSpellId = sourceSpellId,
                sourceActor = sourceActor,
                remainingTurns = durationTurns,
                totalTickValue = totalTickValue,
                strengthRating = strengthRating,
                removedWhenType = removedWhenType,
            };
        }

        public ActiveEffect CreateActiveEffect(
            int spellLevel,
            ResolvedSpell spell,
            EffectInstance inst,
            CombatActorState attacker,
            CombatActorState target,
            int lastDamageDealt,
            int finalPower
        )
        {
            var contributor = new EffectContributor
            {
                sourceSpellId = spell.spellId,
                sourceActor = attacker.actorType,
                remainingTurns = inst.GetScaled(spellLevel).durationTurns,
                totalTickValue = CalculateEffectTick(
                    inst.magnitudeBasis,
                    inst.GetScaled(spellLevel).magnitudeFlat,
                    inst.GetScaled(spellLevel).magnitudePercent,
                    finalPower,
                    lastDamageDealt
                ),
                strengthRating = inst.strengthRating * spellLevel,
                removedWhenType = inst.removeWhenType,
            };

            if (IsStatModifier(inst.effect))
            {
                GetSignedStatDeltas(
                    inst.effect,
                    inst,
                    spellLevel,
                    finalPower,
                    lastDamageDealt,
                    out int flat,
                    out int pct
                );
                ApplyStatModifierNow(target, inst.effect, contributor, flat, pct);
            }

            var effect = new ActiveEffect
            {
                effectId = inst.effect.effectId,
                definition = inst.effect,
                source = attacker.actorType,
                displayName = inst.effect.displayName,
                polarity = inst.effect.polarity,
                kind = inst.effect.kind,
            };

            effect.contributors.Add(contributor);
            return effect;

            // return new ActiveEffect
            // {
            //     effectId = inst.effect.effectId,
            //     definition = inst.effect,
            //     source = attacker.actorType,
            //     displayName = inst.effect.displayName,
            //     polarity = inst.effect.polarity,
            //     kind = inst.effect.kind,
            //     contributors =
            //     {
            //         new EffectContributor
            //         {
            //             sourceSpellId = spell.spellId,
            //             sourceActor = attacker.actorType,
            //             remainingTurns = inst.GetScaled(spellLevel).durationTurns,
            //             totalTickValue = CalculateEffectTick(
            //                 inst.magnitudeBasis,
            //                 inst.GetScaled(spellLevel).magnitudeFlat,
            //                 inst.GetScaled(spellLevel).magnitudePercent,
            //                 finalPower,
            //                 lastDamageDealt
            //             ),
            //             strengthRating = inst.strengthRating * spellLevel,
            //             removedWhenType = inst.removeWhenType,
            //         },
            //     },
        }

        private static bool IsStatModifier(EffectDefinition def)
        {
            return def != null && def.kind == EffectKind.StatModifier;
        }

        public List<PeriodicTickResult> TickOnActionChosen(
            CombatState state,
            CombatActorType actorType
        )
        {
            var results = new List<PeriodicTickResult>();

            if (state == null || state.isFinished)
                return results;

            var owner = state.Get(actorType);
            if (owner == null || owner.activeEffects == null || owner.activeEffects.Count == 0)
                return results;

            // Iterate backwards (removals)
            for (int i = owner.activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = owner.activeEffects[i];
                if (effect == null || effect.contributors == null || effect.contributors.Count == 0)
                {
                    owner.activeEffects.RemoveAt(i);
                    continue;
                }

                int total = 0;

                // Tick contributors
                for (int c = effect.contributors.Count - 1; c >= 0; c--)
                {
                    var contrib = effect.contributors[c];
                    if (contrib == null)
                    {
                        effect.contributors.RemoveAt(c);
                        continue;
                    }

                    // 1) Tick BEFORE decrement (so last turn still ticks)
                    if (contrib.remainingTurns > 0)
                    {
                        if (
                            effect.kind == EffectKind.DamageOverTime
                            || effect.kind == EffectKind.HealOverTime
                        )
                        {
                            total += Math.Max(0, contrib.totalTickValue);
                        }
                    }

                    // 2) Decrement duration
                    contrib.remainingTurns -= 1;

                    // 3) Expire contributor -> undo stat mods if needed
                    if (contrib.remainingTurns <= 0)
                    {
                        if (
                            effect.kind == EffectKind.StatModifier
                            && owner.modifiers != null
                            && effect.definition != null
                        )
                        {
                            // Undo exactly what was applied for THIS contributor
                            UndoModifierDelta(
                                owner.modifiers,
                                effect.definition,
                                contrib.statFlatApplied,
                                contrib.statPercentApplied
                            );
                        }

                        effect.contributors.RemoveAt(c);
                    }
                }

                // Emit tick result for DOT/HOT
                if (
                    total > 0
                    && (
                        effect.kind == EffectKind.DamageOverTime
                        || effect.kind == EffectKind.HealOverTime
                    )
                )
                {
                    results.Add(
                        new PeriodicTickResult(
                            source: effect.source, // who applied originally (good enough for logging)
                            target: owner.actorType,
                            kind: effect.kind,
                            amount: total,
                            effectId: effect.effectId,
                            effectName: effect.displayName
                        )
                    );
                }

                // Remove effect if empty
                if (effect.contributors.Count == 0)
                    owner.activeEffects.RemoveAt(i);
            }

            return results;
        }

        private static void GetSignedStatDeltas(
            EffectDefinition def,
            EffectInstance inst,
            int spellLevel,
            int finalPower,
            int lastDamageDealt,
            out int flat,
            out int percent
        )
        {
            var scaled = inst.GetScaled(spellLevel);

            // Start from raw config
            flat = scaled.magnitudeFlat;
            percent = scaled.magnitudePercent;

            // ✅ If this is a StatModifier and it uses a basis, materialize into FLAT.
            // Example: 50% of Power -> flat += finalPower * 50 / 100, percent = 0
            if (
                def != null
                && def.kind == EffectKind.StatModifier
                && scaled.magnitudeBasis != EffectMagnitudeBasis.None
            )
            {
                int basisValue = 0;
                switch (scaled.magnitudeBasis)
                {
                    case EffectMagnitudeBasis.Power:
                        basisValue = finalPower;
                        break;

                    case EffectMagnitudeBasis.DamageDealt:
                        basisValue = lastDamageDealt;
                        break;
                }

                flat += (basisValue * percent) / 100;
                percent = 0; // consumed into flat
            }

            // Buff = +, Debuff = -
            int sign = (def != null && def.polarity == EffectPolarity.Debuff) ? -1 : 1;
            flat *= sign;
            percent *= sign;
        }

        private static void ApplyStatModifierNow(
            CombatActorState target,
            EffectDefinition def,
            EffectContributor contributor,
            int flat,
            int percent
        )
        {
            if (target?.modifiers == null || def == null || contributor == null)
                return;

            // Store what we applied so it can be undone per-contributor
            contributor.statFlatApplied = flat;
            contributor.statPercentApplied = percent;

            ApplyModifierDelta(target.modifiers, def, flat, percent);
        }

        private static void UndoAllStatContributorsIfNeeded(
            CombatActorState target,
            ActiveEffect effect
        )
        {
            if (target?.modifiers == null || effect?.definition == null)
                return;

            if (!IsStatModifier(effect.definition))
                return;

            for (int i = 0; i < effect.contributors.Count; i++)
            {
                var c = effect.contributors[i];
                if (c == null)
                    continue;

                UndoModifierDelta(
                    target.modifiers,
                    effect.definition,
                    c.statFlatApplied,
                    c.statPercentApplied
                );
            }
        }

        private static void ApplyModifierDelta(
            StatModifiers mods,
            EffectDefinition def,
            int addFlat,
            int addPercent
        )
        {
            if (mods == null || def == null)
                return;

            float pct = addPercent / 100f; // allow negative for debuffs

            switch (def.stat)
            {
                case EffectStat.None:
                    return;

                // -------------------------
                // Damage
                // -------------------------
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

                // -------------------------
                // Power
                // -------------------------
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

                // -------------------------
                // Spell base
                // -------------------------
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

                // -------------------------
                // Hit / speeds
                // -------------------------
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

                // -------------------------
                // Defence
                // -------------------------
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

                // -------------------------
                // Type layers (uses def.damageType[])
                // -------------------------
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

            float pct = oldPercent / 100f; // allow negative

            switch (def.stat)
            {
                case EffectStat.None:
                    return;

                // -------------------------
                // Damage
                // -------------------------
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

                // -------------------------
                // Power
                // -------------------------
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

                // -------------------------
                // Spell base
                // -------------------------
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

                // -------------------------
                // Hit / speeds
                // -------------------------
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

                // -------------------------
                // Defence
                // -------------------------
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

                // -------------------------
                // Type layers (uses def.damageType[])
                // -------------------------
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

        // -------------------------
        // Type-based helper mapping
        // -------------------------
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

        public int CalculateEffectTick(
            EffectMagnitudeBasis basis,
            int baseFlat,
            int basePercent,
            int power,
            int lastDamageDealt
        )
        {
            int magnitude = 0;
            switch (basis)
            {
                case EffectMagnitudeBasis.None:
                    magnitude += baseFlat;
                    break;
                case EffectMagnitudeBasis.Power:
                    magnitude += power * basePercent / 100;
                    break;
                case EffectMagnitudeBasis.DamageDealt:
                    magnitude += lastDamageDealt * basePercent / 100;
                    break;
            }
            return Math.Max(0, magnitude);
        }
    }
}
