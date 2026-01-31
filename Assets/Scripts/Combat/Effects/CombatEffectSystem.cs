using System;
using System.Collections.Generic;
using MyGame.Common;
using Unity.VisualScripting;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class CombatEffectSystem
    {
        private readonly EffectDatabase _db;
        private readonly ICombatEffectCallbacks _callbacks;
        private readonly List<BaseStatModifier> _tmpBaseMods = new List<BaseStatModifier>(16);
        private readonly List<DerivedStatModifier> _tmpDerivedMods = new List<DerivedStatModifier>(
            16
        );

        public CombatEffectSystem(EffectDatabase db, ICombatEffectCallbacks callbacks = null)
        {
            _db = db;
            _callbacks = callbacks;
        }

        public readonly struct PeriodicTickResult
        {
            public readonly CombatActorType source;
            public readonly CombatActorType target;
            public readonly EffectKind kind;
            public readonly int amount;
            public readonly string effectId;
            public readonly string effectName;
            public readonly Sprite icon;

            public PeriodicTickResult(
                CombatActorType source,
                CombatActorType target,
                EffectKind kind,
                int amount,
                string effectId,
                string effectName,
                Sprite icon
            )
            {
                this.source = source;
                this.target = target;
                this.kind = kind;
                this.amount = amount;
                this.effectId = effectId;
                this.effectName = effectName;
                this.icon = icon;
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

            // Target is based on instance target (self or enemy)
            CombatActorState target = inst.target == EffectTarget.Self ? attacker : defender;

            // Find existing effect on the target (used by multiple rules)
            ActiveEffect existing = ActiveEffect.FindEffectById(
                target.activeEffects,
                inst.effect.effectId
            );

            switch (inst.reapplyRule)
            {
                case EffectReapplyRule.AddOnTop:
                {
                    // Non-stackable & non-mergeable effects should refresh duration on reapply.
                    if (existing != null && !inst.stackable && !inst.mergeable)
                    {
                        ApplyDurationStacking(
                            existing,
                            inst,
                            spellLevel,
                            null,
                            true,
                            inst.GetScaled(spellLevel).durationTurns
                        );
                        break;
                    }

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
                            bool dirty = UndoAllStatContributorsIfNeeded(target, existing);
                            existing.contributors.Clear();
                            var contributor = BuildContributorWithComponents(
                                attacker,
                                target,
                                spell,
                                spellLevel,
                                inst,
                                lastDamageDealt,
                                finalPower,
                                out bool statsDirty
                            );
                            existing.contributors.Add(contributor);

                            if (dirty || statsDirty)
                                RecalculateDerivedStatsIfNeeded(target);
                            break;
                        }
                    }
                    else if (inst.compareMode == EffectStrengthCompareMode.ByComputedMagnitude)
                    {
                        int incomingStrength = CalculateCompositeTickSum(
                            inst,
                            spellLevel,
                            finalPower,
                            lastDamageDealt,
                            target
                        );
                        bool incomingStronger = existing.TotalDamage < incomingStrength;

                        if (incomingStronger)
                        {
                            bool dirty = UndoAllStatContributorsIfNeeded(target, existing);
                            existing.contributors.Clear();
                            var contributor = BuildContributorWithComponents(
                                attacker,
                                target,
                                spell,
                                spellLevel,
                                inst,
                                lastDamageDealt,
                                finalPower,
                                out bool statsDirty
                            );
                            existing.contributors.Add(contributor);

                            if (dirty || statsDirty)
                                RecalculateDerivedStatsIfNeeded(target);
                            break;
                        }
                    }
                    break;
                }
                case EffectReapplyRule.DoNothingIfPresent:
                {
                    // If present -> do nothing
                    if (existing != null)
                    {
                        ApplyDurationStacking(
                            existing,
                            inst,
                            spellLevel,
                            null,
                            true,
                            inst.GetScaled(spellLevel).durationTurns
                        );
                        break;
                    }

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
            CombatActorState target = inst.target == EffectTarget.Self ? attacker : defender;

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
                        var contributor = BuildContributorWithComponents(
                            attacker,
                            target,
                            spell,
                            spellLevel,
                            inst,
                            lastDamageDealt,
                            finalPower,
                            out bool statsDirty
                        );

                        existing.contributors.Add(contributor);
                        if (statsDirty)
                            RecalculateDerivedStatsIfNeeded(target);

                        return new EffectApplyResult
                        {
                            effect = existing,
                            addedContributor = contributor,
                        };
                    }

                    // At max stacks (or not stackable via scaled rules) -> refresh/prolong duration only.
                    ApplyDurationStacking(
                        existing,
                        inst,
                        spellLevel,
                        null,
                        true,
                        scaledInfo.durationTurns
                    );
                }
                // MERGE
                else
                {
                    if (inst.mergeable)
                    {
                        var contributor = BuildContributorWithComponents(
                            attacker,
                            target,
                            spell,
                            spellLevel,
                            inst,
                            lastDamageDealt,
                            finalPower,
                            out bool statsDirty
                        );

                        existing.contributors.Add(contributor);
                        if (statsDirty)
                            RecalculateDerivedStatsIfNeeded(target);

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
                    finalPower: finalPower,
                    out bool statsDirty
                );
                effects.Add(newEffect);
                if (statsDirty)
                    RecalculateDerivedStatsIfNeeded(target);

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
            EffectContributor newContributor,
            bool noStackJustDuration = false,
            int newDurationTurn = 0
        )
        {
            if (effect == null || inst == null)
                return;

            var scaled = inst.GetScaled(spellLevel);
            if (noStackJustDuration)
            {
                switch (inst.durationStackMode)
                {
                    case DurationStackMode.Refresh:
                        for (int i = 0; i < effect.contributors.Count; i++)
                        {
                            var c = effect.contributors[i];

                            if (inst.refreshOverridesRemaining)
                                c.remainingTurns = newDurationTurn;
                            else
                                c.remainingTurns = Mathf.Max(c.remainingTurns, newDurationTurn);
                        }
                        break;

                    case DurationStackMode.Prolong:
                        for (int i = 0; i < effect.contributors.Count; i++)
                        {
                            if (ReferenceEquals(effect.contributors[i], newContributor))
                            {
                                continue;
                            }
                            effect.contributors[i].remainingTurns += newDurationTurn;
                        }
                        break;

                    case DurationStackMode.None:
                        // intentionally do nothing
                        break;
                }
            }
            else
            {
                switch (inst.durationStackMode)
                {
                    case DurationStackMode.Refresh:
                        for (int i = 0; i < effect.contributors.Count; i++)
                        {
                            var c = effect.contributors[i];

                            if (inst.refreshOverridesRemaining)
                                c.remainingTurns = scaled.durationTurns;
                            else
                                c.remainingTurns = Mathf.Max(
                                    c.remainingTurns,
                                    scaled.durationTurns
                                );
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
        }

        private EffectContributor CreateEffectContributor(
            string sourceSpellId,
            CombatActorType sourceActor,
            int durationTurns,
            int strengthRating,
            RemoveWhenType removedWhenType
        )
        {
            return new EffectContributor
            {
                sourceSpellId = sourceSpellId,
                sourceActor = sourceActor,
                remainingTurns = durationTurns,
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
            int finalPower,
            out bool statsDirty
        )
        {
            var contributor = BuildContributorWithComponents(
                attacker,
                target,
                spell,
                spellLevel,
                inst,
                lastDamageDealt,
                finalPower,
                out statsDirty
            );

            var effect = new ActiveEffect
            {
                effectId = inst.effect.effectId,
                definition = inst.effect,
                source = attacker.actorType,
                displayName = inst.effect.displayName,
                polarity = inst.effect.polarity,
                kind = ResolveActiveEffectKind(inst.effect),
            };

            effect.contributors.Add(contributor);
            return effect;
        }

        private EffectContributor BuildContributorWithComponents(
            CombatActorState attacker,
            CombatActorState target,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance inst,
            int lastDamageDealt,
            int finalPower,
            out bool statsDirty
        )
        {
            var scaledInfo = inst.GetScaled(spellLevel);
            var contributor = CreateEffectContributor(
                sourceSpellId: spell.spellId,
                sourceActor: attacker.actorType,
                durationTurns: scaledInfo.durationTurns,
                strengthRating: inst.strengthRating * spellLevel,
                removedWhenType: inst.removeWhenType
            );

            statsDirty = ApplyComponentsToContributor(
                attacker,
                target,
                spell,
                spellLevel,
                inst,
                lastDamageDealt,
                finalPower,
                contributor
            );

            return contributor;
        }

        private bool ApplyComponentsToContributor(
            CombatActorState attacker,
            CombatActorState target,
            ResolvedSpell spell,
            int spellLevel,
            EffectInstance inst,
            int lastDamageDealt,
            int finalPower,
            EffectContributor contributor
        )
        {
            bool baseDerivedDirty = false;
            int totalPeriodic = 0;

            var def = inst.effect;
            int count = def != null ? def.GetComponentCount() : 0;
            string effectName =
                def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName
                : def != null ? def.effectId
                : string.Empty;
            Sprite icon = def != null ? def.icon : null;

            for (int i = 0; i < count; i++)
            {
                var compDef = def.GetComponent(i);
                if (compDef == null)
                    continue;

                var scaled = inst.GetComponentScaled(spellLevel, i);

                var contrib = new EffectComponentContribution
                {
                    componentIndex = i,
                    kind = compDef.kind,
                    tickTiming = compDef.tickTiming,
                };

                switch (compDef.kind)
                {
                    case EffectKind.StatModifier:
                    {
                        GetSignedStatDeltas(
                            compDef,
                            scaled,
                            finalPower,
                            lastDamageDealt,
                            target,
                            out int flat,
                            out int percent
                        );
                        int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;
                        flat *= sign;
                        percent *= sign;
                        ApplyStatModifierNow(target, compDef, contrib, flat, percent);
                        break;
                    }

                    case EffectKind.BaseStatModifier:
                    {
                        GetSignedStatDeltas(
                            compDef,
                            scaled,
                            finalPower,
                            lastDamageDealt,
                            target,
                            out int flat,
                            out int percent
                        );
                        int value = compDef.statOp == ModOp.Flat ? flat : percent;
                        int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;
                        value *= sign;

                        if (value != 0)
                        {
                            contrib.hasBaseStatApplied = true;
                            contrib.baseStatApplied = new BaseStatModifier
                            {
                                stat = compDef.baseStat,
                                op = compDef.statOp,
                                value = value,
                            };
                            baseDerivedDirty = true;
                        }
                        break;
                    }

                    case EffectKind.DerivedStatModifier:
                    {
                        GetSignedStatDeltas(
                            compDef,
                            scaled,
                            finalPower,
                            lastDamageDealt,
                            target,
                            out int flat,
                            out int percent
                        );

                        int value = compDef.statOp == ModOp.Flat ? flat : percent;
                        int sign = def != null && def.polarity == EffectPolarity.Debuff ? -1 : 1;
                        value *= sign;

                        if (value != 0)
                        {
                            contrib.hasDerivedStatApplied = true;
                            contrib.derivedStatApplied = new DerivedStatModifier
                            {
                                stat = compDef.derivedStat,
                                op = compDef.statOp,
                                value = value,
                            };
                            baseDerivedDirty = true;
                        }
                        break;
                    }

                    case EffectKind.DamageOverTime:
                    case EffectKind.HealOverTime:
                    {
                        int tickValue = CalculateEffectAmount(
                            scaled.magnitudeBasis,
                            scaled.magnitudeFlat,
                            scaled.magnitudePercent,
                            finalPower,
                            lastDamageDealt,
                            target
                        );
                        contrib.tickValue = tickValue;
                        totalPeriodic += Math.Max(0, tickValue);
                        break;
                    }

                    case EffectKind.DirectDamage:
                    {
                        int amount = CalculateEffectAmount(
                            scaled.magnitudeBasis,
                            scaled.magnitudeFlat,
                            scaled.magnitudePercent,
                            finalPower,
                            lastDamageDealt,
                            target
                        );
                        contrib.instantValue = amount;
                        if (amount > 0)
                        {
                            _callbacks?.ApplyDirectDamage(
                                attacker.actorType,
                                target.actorType,
                                amount,
                                effectName,
                                icon
                            );
                        }
                        break;
                    }

                    case EffectKind.DirectHeal:
                    {
                        int amount = CalculateEffectAmount(
                            scaled.magnitudeBasis,
                            scaled.magnitudeFlat,
                            scaled.magnitudePercent,
                            finalPower,
                            lastDamageDealt,
                            target
                        );
                        contrib.instantValue = amount;
                        if (amount > 0)
                        {
                            _callbacks?.ApplyDirectHeal(
                                attacker.actorType,
                                target.actorType,
                                amount,
                                effectName,
                                icon
                            );
                        }
                        break;
                    }
                }

                contributor.componentContributions.Add(contrib);
            }

            contributor.totalTickValue = totalPeriodic;
            return baseDerivedDirty;
        }

        private int CalculateCompositeTickSum(
            EffectInstance inst,
            int spellLevel,
            int finalPower,
            int lastDamageDealt,
            CombatActorState target
        )
        {
            if (inst == null || inst.effect == null)
                return 0;

            int total = 0;
            int count = inst.effect.GetComponentCount();
            for (int i = 0; i < count; i++)
            {
                var compDef = inst.effect.GetComponent(i);
                if (compDef == null)
                    continue;

                if (compDef.kind != EffectKind.DamageOverTime)
                    continue;

                var scaled = inst.GetComponentScaled(spellLevel, i);
                int tickValue = CalculateEffectAmount(
                    scaled.magnitudeBasis,
                    scaled.magnitudeFlat,
                    scaled.magnitudePercent,
                    finalPower,
                    lastDamageDealt,
                    target
                );
                total += Math.Max(0, tickValue);
            }

            return total;
        }

        private static EffectKind ResolveActiveEffectKind(EffectDefinition def)
        {
            if (def == null)
                return EffectKind.StatModifier;

            int count = def.GetComponentCount();
            if (count > 1)
                return EffectKind.Composite;
            if (count == 1)
            {
                var comp = def.GetComponent(0);
                return comp != null ? comp.kind : EffectKind.StatModifier;
            }
            return EffectKind.StatModifier;
        }

        private void RecalculateDerivedStatsIfNeeded(CombatActorState actor)
        {
            if (actor == null)
                return;
            int oldHp = actor.hp;
            int oldMana = actor.mana;
            int oldMaxHp = actor.derived.maxHp;
            int oldMaxMana = actor.derived.maxMana;

            _tmpBaseMods.Clear();
            _tmpDerivedMods.Clear();

            if (actor.activeEffects != null && actor.activeEffects.Count > 0)
            {
                for (int i = 0; i < actor.activeEffects.Count; i++)
                {
                    var e = actor.activeEffects[i];
                    if (e == null || e.contributors == null || e.contributors.Count == 0)
                        continue;

                    for (int c = 0; c < e.contributors.Count; c++)
                    {
                        var contrib = e.contributors[c];
                        if (contrib == null || contrib.componentContributions == null)
                            continue;

                        for (int j = 0; j < contrib.componentContributions.Count; j++)
                        {
                            var comp = contrib.componentContributions[j];
                            if (comp == null)
                                continue;

                            if (comp.hasBaseStatApplied)
                                _tmpBaseMods.Add(comp.baseStatApplied);

                            if (comp.hasDerivedStatApplied)
                                _tmpDerivedMods.Add(comp.derivedStatApplied);
                        }
                    }
                }
            }

            var baseStats = actor.baseStatsBase;
            if (_tmpBaseMods.Count > 0)
                baseStats = BaseStatModifierApplier.ApplyAll(baseStats, _tmpBaseMods);
            actor.baseStats = baseStats;

            var derived = CombatStatCalculator.CalculateAll(baseStats, actor.level, actor.tier);
            if (actor.baseDerivedStatMods != null && actor.baseDerivedStatMods.Count > 0)
                DerivedModifierApplier.ApplyAll(ref derived, actor.baseDerivedStatMods);
            if (_tmpDerivedMods.Count > 0)
                DerivedModifierApplier.ApplyAll(ref derived, _tmpDerivedMods);

            actor.derived = derived;

            float hpPct = oldMaxHp > 0 ? (float)oldHp / oldMaxHp : 1f;
            float manaPct = oldMaxMana > 0 ? (float)oldMana / oldMaxMana : 1f;

            int newHp = Mathf.Clamp(
                Mathf.RoundToInt(derived.maxHp * hpPct),
                0,
                Math.Max(1, derived.maxHp)
            );
            int newMana = Mathf.Clamp(
                Mathf.RoundToInt(derived.maxMana * manaPct),
                0,
                Math.Max(0, derived.maxMana)
            );

            actor.hp = newHp;
            actor.mana = newMana;

            if (
                _callbacks != null
                && (
                    oldHp != newHp
                    || oldMana != newMana
                    || oldMaxHp != derived.maxHp
                    || oldMaxMana != derived.maxMana
                )
            )
            {
                _callbacks.NotifyDerivedStatsChanged(actor, oldHp, oldMaxHp, oldMana, oldMaxMana);
            }
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

                int totalDamage = 0;
                int totalHeal = 0;
                bool statsDirty = false;

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
                            contrib.componentContributions != null
                            && contrib.componentContributions.Count > 0
                        )
                        {
                            for (int j = 0; j < contrib.componentContributions.Count; j++)
                            {
                                var comp = contrib.componentContributions[j];
                                if (comp == null)
                                    continue;

                                if (comp.tickTiming != EffectTickTiming.OnOwnerAction)
                                    continue;

                                if (comp.kind == EffectKind.DamageOverTime)
                                    totalDamage += Math.Max(0, comp.tickValue);
                                else if (comp.kind == EffectKind.HealOverTime)
                                    totalHeal += Math.Max(0, comp.tickValue);
                            }
                        }
                    }

                    // 2) Decrement duration
                    contrib.remainingTurns -= 1;

                    // 3) Expire contributor -> undo stat mods if needed
                    if (contrib.remainingTurns <= 0)
                    {
                        if (owner.modifiers != null && effect.definition != null)
                        {
                            if (
                                contrib.componentContributions != null
                                && contrib.componentContributions.Count > 0
                            )
                            {
                                for (int j = 0; j < contrib.componentContributions.Count; j++)
                                {
                                    var comp = contrib.componentContributions[j];
                                    if (comp == null)
                                        continue;

                                    if (comp.kind == EffectKind.StatModifier)
                                    {
                                        var compDef = effect.definition.GetComponent(
                                            comp.componentIndex
                                        );
                                        if (compDef != null)
                                        {
                                            UndoModifierDelta(
                                                owner.modifiers,
                                                compDef,
                                                comp.statFlatApplied,
                                                comp.statPercentApplied
                                            );
                                        }
                                    }

                                    if (comp.hasBaseStatApplied || comp.hasDerivedStatApplied)
                                        statsDirty = true;
                                }
                            }
                        }

                        effect.contributors.RemoveAt(c);
                    }
                }

                if (statsDirty)
                    RecalculateDerivedStatsIfNeeded(owner);

                // Emit tick results for DOT/HOT (can be both)
                if (totalDamage > 0)
                {
                    results.Add(
                        new PeriodicTickResult(
                            source: effect.source,
                            target: owner.actorType,
                            kind: EffectKind.DamageOverTime,
                            amount: totalDamage,
                            effectId: effect.effectId,
                            effectName: effect.displayName,
                            icon: effect.definition != null ? effect.definition.icon : null
                        )
                    );
                }
                if (totalHeal > 0)
                {
                    results.Add(
                        new PeriodicTickResult(
                            source: effect.source,
                            target: owner.actorType,
                            kind: EffectKind.HealOverTime,
                            amount: totalHeal,
                            effectId: effect.effectId,
                            effectName: effect.displayName,
                            icon: effect.definition != null ? effect.definition.icon : null
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
            EffectComponentDefinition comp,
            EffectComponentScaledIntValues scaled,
            int finalPower,
            int lastDamageDealt,
            CombatActorState target,
            out int flat,
            out int percent
        )
        {
            flat = scaled.magnitudeFlat;
            percent = scaled.magnitudePercent;

            // If basis is set, materialize percent into flat.
            if (scaled.magnitudeBasis != EffectMagnitudeBasis.None)
            {
                int basisValue = ResolveBasisValue(
                    scaled.magnitudeBasis,
                    target,
                    finalPower,
                    lastDamageDealt
                );
                flat += (basisValue * percent) / 100;
                percent = 0;
            }
        }

        private static void ApplyStatModifierNow(
            CombatActorState target,
            EffectComponentDefinition comp,
            EffectComponentContribution contribution,
            int flat,
            int percent
        )
        {
            if (target?.modifiers == null || comp == null || contribution == null)
                return;

            // Store what we applied so it can be undone per-component
            contribution.statFlatApplied = flat;
            contribution.statPercentApplied = percent;

            ApplyModifierDelta(target.modifiers, comp, flat, percent);
        }

        private static bool UndoAllStatContributorsIfNeeded(
            CombatActorState target,
            ActiveEffect effect
        )
        {
            if (target?.modifiers == null || effect?.definition == null)
                return false;

            bool baseDerivedDirty = false;

            for (int i = 0; i < effect.contributors.Count; i++)
            {
                var c = effect.contributors[i];
                if (c == null)
                    continue;

                if (c.componentContributions != null && c.componentContributions.Count > 0)
                {
                    for (int j = 0; j < c.componentContributions.Count; j++)
                    {
                        var comp = c.componentContributions[j];
                        if (comp == null)
                            continue;

                        if (comp.kind == EffectKind.StatModifier)
                        {
                            var compDef = effect.definition.GetComponent(comp.componentIndex);
                            if (compDef != null)
                            {
                                UndoModifierDelta(
                                    target.modifiers,
                                    compDef,
                                    comp.statFlatApplied,
                                    comp.statPercentApplied
                                );
                            }
                        }

                        if (comp.hasBaseStatApplied || comp.hasDerivedStatApplied)
                            baseDerivedDirty = true;
                    }
                }
            }

            return baseDerivedDirty;
        }

        private static void ApplyModifierDelta(
            StatModifiers mods,
            EffectComponentDefinition comp,
            int addFlat,
            int addPercent
        )
        {
            if (mods == null || comp == null)
                return;

            float pct = addPercent / 100f; // allow negative for debuffs

            switch (comp.stat)
            {
                case EffectStat.None:
                    return;

                // -------------------------
                // Damage
                // -------------------------
                case EffectStat.DamageAll:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.damageFlat += v,
                        addPercentAction: p => mods.AddDamagePercent(p)
                    );
                    return;

                case EffectStat.DamagePhysical:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.attackDamageFlat += v,
                        addPercentAction: p => mods.AddPhysicalDamagePercent(p)
                    );
                    return;

                case EffectStat.DamageMagic:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.magicDamageFlat += v,
                        addPercentAction: p => mods.AddMagicDamagePercent(p)
                    );
                    return;

                // -------------------------
                // Power
                // -------------------------
                case EffectStat.PowerScalingAll:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddPowerScalingFlat(v / 100f),
                        addPercentAction: p => mods.AddPowerScalingPercent(p)
                    );
                    return;

                case EffectStat.PowerScalingPhysical:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddAttackPowerScalingFlat(v / 100f),
                        addPercentAction: p => mods.AddAttackPowerScalingPercent(p)
                    );
                    return;

                case EffectStat.PowerScalingMagic:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddMagicPowerScalingFlat(v / 100f),
                        addPercentAction: p => mods.AddMagicPowerScalingPercent(p)
                    );
                    return;
                // -------------------------
                // Spell base
                // -------------------------
                case EffectStat.SpellBaseAll:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddSpellBaseFlat(v),
                        addPercentAction: p => mods.AddSpellBasePercent(p)
                    );
                    return;

                case EffectStat.SpellBasePhysical:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddPhysicalSpellBaseFlat(v),
                        addPercentAction: p => mods.AddPhysicalSpellBasePercent(p)
                    );
                    return;

                case EffectStat.SpellBaseMagic:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddMagicSpellBaseFlat(v),
                        addPercentAction: p => mods.AddMagicSpellBasePercent(p)
                    );
                    return;

                // -------------------------
                // Hit / speeds
                // -------------------------
                // -------------------------
                // Defence
                // -------------------------
                // -------------------------
                // Type layers (uses comp.damageType[])
                // -------------------------
                case EffectStat.AttackerBonusByType:
                    ApplyTypeBased(comp, mods, addFlat, pct, TypeMode.AttackerBonus);
                    return;

                case EffectStat.DefenderVulnerabilityByType:
                    ApplyTypeBased(comp, mods, addFlat, pct, TypeMode.DefenderVuln);
                    return;

                case EffectStat.AttackerWeakenByType:
                    ApplyTypeBased(comp, mods, addFlat, pct, TypeMode.AttackerWeaken);
                    return;

                case EffectStat.DefenderResistByType:
                    ApplyTypeBased(comp, mods, addFlat, pct, TypeMode.DefenderResist);
                    return;

                case EffectStat.MeleeDamageBonus:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddMeleeDamageBonusFlat(v),
                        addPercentAction: p => mods.AddMeleeDamageBonusPercent(p)
                    );
                    return;

                case EffectStat.RangedDamageBonus:
                    ApplyFlatOrPercent(
                        comp.op,
                        addFlat,
                        pct,
                        addFlatAction: v => mods.AddRangedDamageBonusFlat(v),
                        addPercentAction: p => mods.AddRangedDamageBonusPercent(p)
                    );
                    return;
            }
        }

        public static void ApplyCombatStatModifier(
            StatModifiers mods,
            EffectStat stat,
            EffectOp op,
            int value,
            DamageType damageType = DamageType.None
        )
        {
            if (mods == null)
                return;

            var comp = new EffectComponentDefinition
            {
                kind = EffectKind.StatModifier,
                stat = stat,
                op = op,
                damageType = damageType == DamageType.None ? null : new[] { damageType },
            };

            int flat = op == EffectOp.Flat ? value : 0;
            int percent = op == EffectOp.Percent ? value : 0;
            ApplyModifierDelta(mods, comp, flat, percent);
        }

        private static void UndoModifierDelta(
            StatModifiers mods,
            EffectComponentDefinition comp,
            int oldFlat,
            int oldPercent
        )
        {
            if (mods == null || comp == null)
                return;

            float pct = oldPercent / 100f; // allow negative

            switch (comp.stat)
            {
                case EffectStat.None:
                    return;

                // -------------------------
                // Damage
                // -------------------------
                case EffectStat.DamageAll:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.damageFlat -= v,
                        removePercentAction: p => mods.RemoveDamagePercent(p)
                    );
                    return;

                case EffectStat.DamagePhysical:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.attackDamageFlat -= v,
                        removePercentAction: p => mods.RemovePhysicalDamagePercent(p)
                    );
                    return;

                case EffectStat.DamageMagic:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.magicDamageFlat -= v,
                        removePercentAction: p => mods.RemoveMagicDamagePercent(p)
                    );
                    return;

                // -------------------------
                // Power
                // -------------------------
                case EffectStat.PowerScalingAll:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddPowerScalingFlat(-(v / 100f)),
                        removePercentAction: p => mods.RemovePowerScalingPercent(p)
                    );
                    return;

                case EffectStat.PowerScalingPhysical:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddAttackPowerScalingFlat(-(v / 100f)),
                        removePercentAction: p => mods.RemoveAttackPowerScalingPercent(p)
                    );
                    return;

                case EffectStat.PowerScalingMagic:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddMagicPowerScalingFlat(-(v / 100f)),
                        removePercentAction: p => mods.RemoveMagicPowerScalingPercent(p)
                    );
                    return;
                // -------------------------
                // Spell base
                // -------------------------
                case EffectStat.SpellBaseAll:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddSpellBaseFlat(-v),
                        removePercentAction: p => mods.RemoveSpellBasePercent(p)
                    );
                    return;

                case EffectStat.SpellBasePhysical:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddPhysicalSpellBaseFlat(-v),
                        removePercentAction: p => mods.RemovePhysicalSpellBasePercent(p)
                    );
                    return;

                case EffectStat.SpellBaseMagic:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.AddMagicSpellBaseFlat(-v),
                        removePercentAction: p => mods.RemoveMagicSpellBasePercent(p)
                    );
                    return;

                // -------------------------
                // Hit / speeds
                // -------------------------
                // -------------------------
                // Defence
                // -------------------------
                // -------------------------
                // Type layers (uses comp.damageType[])
                // -------------------------
                case EffectStat.AttackerBonusByType:
                    UndoTypeBased(comp, mods, oldFlat, pct, TypeMode.AttackerBonus);
                    return;

                case EffectStat.DefenderVulnerabilityByType:
                    UndoTypeBased(comp, mods, oldFlat, pct, TypeMode.DefenderVuln);
                    return;

                case EffectStat.AttackerWeakenByType:
                    UndoTypeBased(comp, mods, oldFlat, pct, TypeMode.AttackerWeaken);
                    return;

                case EffectStat.DefenderResistByType:
                    UndoTypeBased(comp, mods, oldFlat, pct, TypeMode.DefenderResist);
                    return;

                case EffectStat.MeleeDamageBonus:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.RemoveMeleeDamageBonusFlat(v),
                        removePercentAction: p => mods.RemoveMeleeDamageBonusPercent(p)
                    );
                    return;

                case EffectStat.RangedDamageBonus:
                    UndoFlatOrPercent(
                        comp.op,
                        oldFlat,
                        pct,
                        removeFlatAction: v => mods.RemoveRangedDamageBonusFlat(v),
                        removePercentAction: p => mods.RemoveRangedDamageBonusPercent(p)
                    );
                    return;
            }
        }

        private static void ApplyFlatOrPercent(
            EffectOp op,
            int addFlat,
            float pct,
            System.Action<int> addFlatAction,
            System.Action<float> addPercentAction
        )
        {
            switch (op)
            {
                case EffectOp.Flat:
                    addFlatAction?.Invoke(addFlat);
                    return;
                case EffectOp.Percent:
                    addPercentAction?.Invoke(pct);
                    return;
            }
        }

        private static void UndoFlatOrPercent(
            EffectOp op,
            int oldFlat,
            float pct,
            System.Action<int> removeFlatAction,
            System.Action<float> removePercentAction
        )
        {
            switch (op)
            {
                case EffectOp.Flat:
                    removeFlatAction?.Invoke(oldFlat);
                    return;
                case EffectOp.Percent:
                    removePercentAction?.Invoke(pct);
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
            EffectComponentDefinition comp,
            StatModifiers mods,
            int addFlat,
            float pct,
            TypeMode mode
        )
        {
            if (comp == null || comp.damageType == null || comp.damageType.Length == 0)
                return;

            for (int i = 0; i < comp.damageType.Length; i++)
            {
                var t = comp.damageType[i];

                switch (mode)
                {
                    case TypeMode.AttackerBonus:
                        if (comp.op == EffectOp.Flat)
                            mods.AddAttackerBonusFlat(t, addFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.AddAttackerBonusPercent(t, pct);
                        break;

                    case TypeMode.DefenderVuln:
                        if (comp.op == EffectOp.Flat)
                            mods.AddDefenderVulnFlat(t, addFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.AddDefenderVulnPercent(t, pct);
                        break;

                    case TypeMode.DefenderResist:
                        if (comp.op == EffectOp.Flat)
                            mods.AddDefenderResistFlat(t, addFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.AddDefenderResistPercent(t, pct);
                        break;

                    case TypeMode.AttackerWeaken:
                        if (comp.op == EffectOp.Flat)
                            mods.AddAttackerWeakenFlat(t, addFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.AddAttackerWeakenPercent(t, pct);
                        break;
                }
            }
        }

        private static void UndoTypeBased(
            EffectComponentDefinition comp,
            StatModifiers mods,
            int oldFlat,
            float pct,
            TypeMode mode
        )
        {
            if (comp == null || comp.damageType == null || comp.damageType.Length == 0)
                return;

            for (int i = 0; i < comp.damageType.Length; i++)
            {
                var t = comp.damageType[i];

                switch (mode)
                {
                    case TypeMode.AttackerBonus:
                        if (comp.op == EffectOp.Flat)
                            mods.RemoveAttackerBonusFlat(t, oldFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.RemoveAttackerBonusPercent(t, pct);
                        break;

                    case TypeMode.DefenderVuln:
                        if (comp.op == EffectOp.Flat)
                            mods.RemoveDefenderVulnFlat(t, oldFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.RemoveDefenderVulnPercent(t, pct);
                        break;

                    case TypeMode.DefenderResist:
                        if (comp.op == EffectOp.Flat)
                            mods.RemoveDefenderResistFlat(t, oldFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.RemoveDefenderResistPercent(t, pct);
                        break;

                    case TypeMode.AttackerWeaken:
                        if (comp.op == EffectOp.Flat)
                            mods.RemoveAttackerWeakenFlat(t, oldFlat);
                        else if (comp.op == EffectOp.Percent)
                            mods.RemoveAttackerWeakenPercent(t, pct);
                        break;
                }
            }
        }

        private static int ResolveBasisValue(
            EffectMagnitudeBasis basis,
            CombatActorState target,
            int finalPower,
            int lastDamageDealt
        )
        {
            switch (basis)
            {
                case EffectMagnitudeBasis.Power:
                    return Math.Max(0, finalPower);
                case EffectMagnitudeBasis.DamageDealt:
                    return Math.Max(0, lastDamageDealt);
                case EffectMagnitudeBasis.MaxHealth:
                    return target != null ? Math.Max(0, target.derived.maxHp) : 0;
                case EffectMagnitudeBasis.MaxMana:
                    return target != null ? Math.Max(0, target.derived.maxMana) : 0;
                case EffectMagnitudeBasis.LastDamageTaken:
                    return target != null ? Math.Max(0, target.lastDamageTaken) : 0;
                default:
                    return 0;
            }
        }

        private int CalculateEffectAmount(
            EffectMagnitudeBasis basis,
            int baseFlat,
            int basePercent,
            int finalPower,
            int lastDamageDealt,
            CombatActorState target
        )
        {
            int magnitude = baseFlat;
            if (basis != EffectMagnitudeBasis.None)
            {
                int basisValue = ResolveBasisValue(basis, target, finalPower, lastDamageDealt);
                magnitude += (basisValue * basePercent) / 100;
            }
            return Math.Max(0, magnitude);
        }
    }
}
