using UnityEngine;

namespace MyGame.Combat
{
    public sealed class DefenseMitigationDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx)
        {
            int dmg = ctx.finalDamage;
            if (dmg <= 0)
            {
                ctx.finalDamage = 0;
                return;
            }

            int effectiveDefense = ResolveEffectiveDefense(ctx);

            int outDmg = dmg - effectiveDefense;
            if (outDmg < 0)
                outDmg = 0;

            ctx.finalDamage = outDmg;
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

            // 4) apply spell ignores (order you wanted: percent first, then flat)
            int ignorePct = ctx.spell.ignoreDefensePercent;
            if (ignorePct < 0)
                ignorePct = 0;
            if (ignorePct > 100)
                ignorePct = 100;

            int ignoreFlat = Mathf.Max(0, ctx.spell.ignoreDefenseFlat);

            defense = defense * (1f - ignorePct / 100f);
            defense = defense - ignoreFlat;
            return Mathf.RoundToInt(defense);
        }
    }
}
