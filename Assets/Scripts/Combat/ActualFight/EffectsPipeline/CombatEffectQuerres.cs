using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    public static class CombatEffectQueries
    {
        public readonly struct MergedEffectView
        {
            public readonly string effectId;
            public readonly EffectKind kind;

            public readonly int remainingTurnsMax;
            public readonly int totalStacks;

            public readonly int totalMagnitudeFlat;
            public readonly int totalMagnitudePercent;
            public readonly EffectMagnitudeBasis magnitudeBasis;

            public readonly int totalPeriodicTick;
            public readonly int sourceCount;

            public MergedEffectView(
                string effectId,
                EffectKind kind,
                int remainingTurnsMax,
                int totalStacks,
                int totalMagnitudeFlat,
                int totalMagnitudePercent,
                EffectMagnitudeBasis magnitudeBasis,
                int totalPeriodicTick,
                int sourceCount
            )
            {
                this.effectId = effectId;
                this.kind = kind;
                this.remainingTurnsMax = remainingTurnsMax;
                this.totalStacks = totalStacks;
                this.totalMagnitudeFlat = totalMagnitudeFlat;
                this.totalMagnitudePercent = totalMagnitudePercent;
                this.magnitudeBasis = magnitudeBasis;
                this.totalPeriodicTick = totalPeriodicTick;
                this.sourceCount = sourceCount;
            }
        }

        public readonly struct EffectSourceView
        {
            public readonly CombatActorType sourceActor;
            public readonly string sourceSpellId;

            public readonly int remainingTurns;
            public readonly int stacks;

            public readonly int magnitudeFlat;
            public readonly int magnitudePercent;
            public readonly EffectMagnitudeBasis magnitudeBasis;

            public readonly int periodicTickTotal;

            public EffectSourceView(
                CombatActorType sourceActor,
                string sourceSpellId,
                int remainingTurns,
                int stacks,
                int magnitudeFlat,
                int magnitudePercent,
                EffectMagnitudeBasis magnitudeBasis,
                int periodicTickTotal
            )
            {
                this.sourceActor = sourceActor;
                this.sourceSpellId = sourceSpellId;
                this.remainingTurns = remainingTurns;
                this.stacks = stacks;
                this.magnitudeFlat = magnitudeFlat;
                this.magnitudePercent = magnitudePercent;
                this.magnitudeBasis = magnitudeBasis;
                this.periodicTickTotal = periodicTickTotal;
            }
        }

        public static List<ActiveEffectState> GetActiveEffects(CombatActorState actor)
        {
            if (actor == null || actor.activeEffects == null)
                return new List<ActiveEffectState>();
            return actor.activeEffects;
        }

        public static bool TryGetMergedEffectView(
            CombatActorState actor,
            string effectId,
            out MergedEffectView view
        )
        {
            view = default;

            if (actor == null || actor.activeEffects == null || string.IsNullOrWhiteSpace(effectId))
                return false;

            ActiveEffectState bucket = null;

            for (int i = 0; i < actor.activeEffects.Count; i++)
            {
                var e = actor.activeEffects[i];
                if (e != null && e.effectId == effectId)
                {
                    bucket = e;
                    break;
                }
            }

            if (bucket == null)
                return false;

            bucket.EnsureContributionBacked();

            int remainingMax = 0;
            int stacksSum = 0;
            int flatSum = 0;
            int pctSum = 0;
            int tickSum = 0;

            for (int i = 0; i < bucket.contributions.Count; i++)
            {
                var c = bucket.contributions[i];
                if (c == null)
                    continue;

                remainingMax = Mathf.Max(remainingMax, c.remainingTurns);
                stacksSum += Mathf.Max(1, c.stacks);

                flatSum += Mathf.Max(0, c.totalMagnitudeFlat);
                pctSum += Mathf.Max(0, c.totalMagnitudePercent);

                if (c.periodicTickContributions != null)
                {
                    for (int k = 0; k < c.periodicTickContributions.Count; k++)
                        tickSum += Mathf.Max(0, c.periodicTickContributions[k]);
                }
            }

            view = new MergedEffectView(
                effectId: bucket.effectId,
                kind: bucket.kind,
                remainingTurnsMax: remainingMax,
                totalStacks: Mathf.Max(1, stacksSum),
                totalMagnitudeFlat: flatSum,
                totalMagnitudePercent: pctSum,
                magnitudeBasis: bucket.magnitudeBasis,
                totalPeriodicTick: tickSum,
                sourceCount: bucket.contributions.Count
            );

            return true;
        }

        public static List<EffectSourceView> GetEffectSources(
            CombatActorState actor,
            string effectId
        )
        {
            var list = new List<EffectSourceView>();

            if (actor == null || actor.activeEffects == null || string.IsNullOrWhiteSpace(effectId))
                return list;

            ActiveEffectState bucket = null;

            for (int i = 0; i < actor.activeEffects.Count; i++)
            {
                var e = actor.activeEffects[i];
                if (e != null && e.effectId == effectId)
                {
                    bucket = e;
                    break;
                }
            }

            if (bucket == null)
                return list;

            bucket.EnsureContributionBacked();

            for (int i = 0; i < bucket.contributions.Count; i++)
            {
                var c = bucket.contributions[i];
                if (c == null)
                    continue;

                int tickTotal = 0;
                if (c.periodicTickContributions != null)
                {
                    for (int k = 0; k < c.periodicTickContributions.Count; k++)
                        tickTotal += Mathf.Max(0, c.periodicTickContributions[k]);
                }

                list.Add(
                    new EffectSourceView(
                        sourceActor: c.sourceActor,
                        sourceSpellId: c.sourceSpellId,
                        remainingTurns: c.remainingTurns,
                        stacks: Mathf.Max(1, c.stacks),
                        magnitudeFlat: Mathf.Max(0, c.totalMagnitudeFlat),
                        magnitudePercent: Mathf.Max(0, c.totalMagnitudePercent),
                        magnitudeBasis: c.magnitudeBasis,
                        periodicTickTotal: tickTotal
                    )
                );
            }

            return list;
        }
    }
}
