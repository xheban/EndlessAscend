using UnityEngine;

namespace MyGame.Combat
{
    public sealed class DefenseMitigationDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            int effectiveDefense = ResolveEffectiveDefense(ctx);
            ctx.effectiveDefense = effectiveDefense;
        }

        private static int ResolveEffectiveDefense(ActionContext ctx)
        {
            // 1) pick defense stat based on spell kind
            int baseDefense =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.defender.derived.magicalDefense
                    : ctx.defender.derived.physicalDefense;

            float defense = baseDefense;
            if (defense < 0f)
                defense = 0f;

            return Mathf.RoundToInt(defense);
        }
    }
}
