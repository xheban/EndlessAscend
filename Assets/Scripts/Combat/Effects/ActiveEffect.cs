using System;
using System.Collections.Generic;
using MyGame.Common;

namespace MyGame.Combat
{
    [Serializable]
    public sealed class ActiveEffect
    {
        public string effectId;
        public string displayName;
        public EffectDefinition definition;
        public CombatActorType source; // (optional legacy) who applied it first
        public EffectPolarity polarity;
        public EffectKind kind;

        // Contributors (each application has its own duration)
        public readonly List<EffectContributor> contributors = new List<EffectContributor>();

        // ---- Convenience for UI / logic ----

        public int TotalStacks
        {
            get { return contributors.Count; }
        }

        public int TotalDamage
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < contributors.Count; i++)
                    sum += Math.Max(1, contributors[i].totalTickValue);
                return sum;
            }
        }

        public int MaxRemainingTurns
        {
            get
            {
                int max = 0;
                for (int i = 0; i < contributors.Count; i++)
                {
                    int t = contributors[i].remainingTurns;
                    if (t > max)
                        max = t;
                }
                return max;
            }
        }

        // ---- Static helpers ----
        public static ActiveEffect FindEffectById(List<ActiveEffect> effects, string effectId)
        {
            if (effects == null || string.IsNullOrWhiteSpace(effectId))
                return null;

            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (e != null && e.effectId == effectId)
                    return e;
            }

            return null;
        }

        public static int CountContributorsFromSpellId(ActiveEffect effect, string spellId)
        {
            if (effect == null || string.IsNullOrWhiteSpace(spellId))
                return 0;

            var contributors = effect.contributors;
            if (contributors == null)
                return 0;

            int count = 0;

            for (int i = 0; i < contributors.Count; i++)
            {
                var c = contributors[i];
                if (c != null && c.sourceSpellId == spellId)
                    count++;
            }

            return count;
        }

        public static int ReturnTotalStrength(ActiveEffect effect)
        {
            {
                int sum = 0;
                for (int i = 0; i < effect.contributors.Count; i++)
                    sum += Math.Max(1, effect.contributors[i].strengthRating);
                return sum;
            }
        }

        public static bool HasContributorFromSpellId(ActiveEffect effect, string spellId)
        {
            if (effect == null || string.IsNullOrWhiteSpace(spellId))
                return false;

            var contributors = effect.contributors;
            if (contributors == null)
                return false;

            for (int i = 0; i < contributors.Count; i++)
            {
                var c = contributors[i];
                if (c != null && c.sourceSpellId == spellId)
                    return true;
            }

            return false;
        }
    }
}
