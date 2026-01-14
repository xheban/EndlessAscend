using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class ActiveEffectState
    {
        [Header("Identity")]
        public string effectId;

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

        [Header("Accumulated totals (for UI/debug)")]
        public int totalMagnitudeFlat;

        [Range(0, 1000)]
        public int totalMagnitudePercent;
        public EffectMagnitudeBasis magnitudeBasis;

        [Header("Periodic (DOT/HOT)")]
        [Min(0)]
        public List<int> periodicTickContributions = new();

        [Header("Source")]
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
            this.remainingTurns = Mathf.Max(1, durationTurns);
            this.stacks = 1;
            this.stackable = stackable;
            this.maxStacks = Mathf.Max(1, maxStacks);

            this.totalMagnitudeFlat = 0;
            this.totalMagnitudePercent = 0;
            this.magnitudeBasis = EffectMagnitudeBasis.None;

            this.sourceActor = sourceActor;
            this.sourceSpellId = sourceSpellId;
            this.kind = kind;
        }
    }
}
