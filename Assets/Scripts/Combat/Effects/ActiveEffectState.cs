using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class ActiveEffectState
    {
        // -------------------------
        // Contribution model
        // -------------------------

        [Serializable]
        public sealed class EffectContributionState
        {
            [Header("Source")]
            public CombatActorType sourceActor;
            public string sourceSpellId;

            [Header("Duration")]
            [Min(0)]
            public int remainingTurns;

            [Header("Stacks")]
            [Min(1)]
            public int stacks = 1;

            [Header("Stack config snapshot")]
            public bool stackable;

            [Min(1)]
            public int maxStacks = 1;

            [Header("Totals for this contribution (exact applied totals)")]
            public int totalMagnitudeFlat;

            [Range(0, 1000)]
            public int totalMagnitudePercent;

            public EffectMagnitudeBasis magnitudeBasis;

            [Header("Periodic (DOT/HOT) per-stack contributions")]
            [Min(0)]
            public List<int> periodicTickContributions = new();
        }

        [Header("Contributions (new model)")]
        public List<EffectContributionState> contributions = new();

        // -------------------------
        // Legacy / UI-friendly summary fields
        // (kept so your other systems/UI don't break)
        // These are rebuilt from contributions.
        // -------------------------

        [Header("Identity")]
        public string effectId;

        [Header("Duration (summary)")]
        [Min(0)]
        public int remainingTurns;

        [Header("Stacks (summary)")]
        [Min(1)]
        public int stacks = 1;

        [Header("Stack config snapshot (summary)")]
        public bool stackable;

        [Min(1)]
        public int maxStacks = 1;

        [Header("Accumulated totals (summary)")]
        public int totalMagnitudeFlat;

        [Range(0, 1000)]
        public int totalMagnitudePercent;

        public EffectMagnitudeBasis magnitudeBasis;

        [Header("Periodic (summary)")]
        [Min(0)]
        public List<int> periodicTickContributions = new();

        [Header("Source (summary)")]
        public CombatActorType sourceActor;

        [Header("Kind snapshot")]
        public EffectKind kind;

        public string sourceSpellId;

        public ActiveEffectState() { }

        public ActiveEffectState(
            string effectId,
            int durationTurns,
            bool stackable,
            int maxStacks,
            CombatActorType sourceActor,
            string sourceSpellId,
            EffectKind kind
        )
        {
            this.effectId = effectId;
            this.kind = kind;

            // summary defaults (rebuilt later)
            this.remainingTurns = Mathf.Max(1, durationTurns);
            this.stacks = 1;
            this.stackable = stackable;
            this.maxStacks = Mathf.Max(1, maxStacks);

            this.totalMagnitudeFlat = 0;
            this.totalMagnitudePercent = 0;
            this.magnitudeBasis = EffectMagnitudeBasis.None;

            this.periodicTickContributions = new List<int>();

            this.sourceActor = sourceActor;
            this.sourceSpellId = sourceSpellId;

            // ensure contribution list exists (even if older objects)
            contributions ??= new List<EffectContributionState>();
        }

        /// <summary>
        /// Ensures we have a contribution list even for older saves/instances.
        /// If contributions are empty but legacy fields exist, we create a single contribution from legacy snapshot.
        /// </summary>
        public void EnsureContributionBacked()
        {
            contributions ??= new List<EffectContributionState>();

            if (contributions.Count > 0)
                return;

            // If this is a newly created bucket (no totals yet), we still create an empty contribution only when needed by caller.
            // However, if legacy data contains values, migrate it.
            bool hasLegacy =
                !string.IsNullOrWhiteSpace(sourceSpellId)
                || totalMagnitudeFlat != 0
                || totalMagnitudePercent != 0
                || (periodicTickContributions != null && periodicTickContributions.Count > 0);

            if (!hasLegacy)
                return;

            var migrated = new EffectContributionState
            {
                sourceActor = sourceActor,
                sourceSpellId = sourceSpellId,
                remainingTurns = remainingTurns,
                stacks = Mathf.Max(1, stacks),
                stackable = stackable,
                maxStacks = Mathf.Max(1, maxStacks),

                totalMagnitudeFlat = Mathf.Max(0, totalMagnitudeFlat),
                totalMagnitudePercent = Mathf.Max(0, totalMagnitudePercent),
                magnitudeBasis = magnitudeBasis,

                periodicTickContributions =
                    periodicTickContributions != null
                        ? new List<int>(periodicTickContributions)
                        : new List<int>(),
            };

            contributions.Add(migrated);
        }

        /// <summary>
        /// Rebuilds legacy/summary fields from all contributions.
        /// Keeps your existing UI and any legacy reads correct.
        /// </summary>
        public void RebuildLegacyFromContributions()
        {
            EnsureContributionBacked();

            int remainingMax = 0;
            int stacksSum = 0;
            int flatSum = 0;
            int pctSum = 0;

            periodicTickContributions ??= new List<int>();
            periodicTickContributions.Clear();

            // Keep first contribution as the "summary source"
            if (contributions.Count > 0 && contributions[0] != null)
            {
                sourceActor = contributions[0].sourceActor;
                sourceSpellId = contributions[0].sourceSpellId;
            }

            for (int i = 0; i < contributions.Count; i++)
            {
                var c = contributions[i];
                if (c == null)
                    continue;

                remainingMax = Mathf.Max(remainingMax, c.remainingTurns);
                stacksSum += Mathf.Max(1, c.stacks);

                flatSum += Mathf.Max(0, c.totalMagnitudeFlat);
                pctSum += Mathf.Max(0, c.totalMagnitudePercent);

                if (c.periodicTickContributions != null)
                {
                    for (int k = 0; k < c.periodicTickContributions.Count; k++)
                        periodicTickContributions.Add(Mathf.Max(0, c.periodicTickContributions[k]));
                }

                // If any contribution says stackable, keep it stackable (summary)
                stackable |= c.stackable;
                maxStacks = Mathf.Max(maxStacks, Mathf.Max(1, c.maxStacks));
                if (magnitudeBasis == EffectMagnitudeBasis.None)
                    magnitudeBasis = c.magnitudeBasis;
            }

            remainingTurns = Mathf.Max(0, remainingMax);
            stacks = Mathf.Max(1, stacksSum);
            totalMagnitudeFlat = flatSum;
            totalMagnitudePercent = pctSum;
        }
    }
}
