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

            // 2) apply flat buffs/debuffs to defense (on defender)
            int flatBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.defender.modifiers.magicDefenseFlat
                    : ctx.defender.modifiers.physicalDefenseFlat
                        + ctx.defender.modifiers.defenceFlat;

            // 3) apply mult buffs/debuffs to defense (on defender)
            float mult =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.defender.modifiers.magicDefenceMult.Final
                    : ctx.defender.modifiers.physicalDefenceMult.Final
                        * ctx.defender.modifiers.DefenceMultFinal;

            if (mult < 0f)
                mult = 0f;

            float defense = (baseDefense + flatBonus) * mult;
            if (defense < 0f)
                defense = 0f;

            return Mathf.RoundToInt(defense);
        }
    }
}
