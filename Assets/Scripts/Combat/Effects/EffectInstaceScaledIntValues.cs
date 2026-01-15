using System;
using MyGame.Common;

namespace MyGame.Common
{
    /// <summary>
    /// Runtime-ready snapshot produced by EffectInstance.GetScaled(spellLevel).
    /// Contains final integer magnitudes PLUS per-instance behavior settings
    /// (merge/stack/reapply/duration/strength compare).
    /// </summary>
    [Serializable]
    public struct EffectInstanceScaledIntValues
    {
        // -------------------------
        // Final numeric values
        // -------------------------
        public int chancePercent; // 0..100
        public int durationTurns; // >= 1
        public int magnitudeFlat; // >= 0
        public int magnitudePercent; // 0..100

        // -------------------------
        // Magnitude basis
        // -------------------------
        public EffectMagnitudeBasis magnitudeBasis;

        // -------------------------
        // Target selection (per instance)
        // -------------------------
        public EffectTarget target;

        // -------------------------
        // Stack / merge flags (per instance)
        // -------------------------
        public bool stackable;
        public int maxStacks; // >= 1
        public bool mergeable;

        // -------------------------
        // Reapply behavior (per instance)
        // -------------------------
        public EffectReapplyRule reapplyRule;

        // -------------------------
        // Duration behavior (per instance)
        // -------------------------
        public DurationStackMode durationStackMode;

        // -------------------------
        // Strength compare (per instance)
        // -------------------------
        public EffectStrengthCompareMode compareMode;

        /// ------------------------
        /// If true, when reapplying this effect,the remaining duration will be overridden to the new application duration.
        /// ------------------------
        public bool refreshOverridesRemaining;

        /// <summary>
        /// Used when compareMode == ByStrengthRating.
        /// If <= 0, you can fall back to definition strength if you keep one,
        /// otherwise treat as 0.
        /// </summary>
        public int strengthRating;
    }
}
