using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Converts attacker power into extra flat damage.
    /// Magical -> magicPower, Physical -> attackPower (you can add more kinds later).
    /// </summary>
    public sealed class SpellBaseDamageBonusRule : IDamageRule
    {
        public void Apply(ActionContext ctx)
        {
            // Attack and magic power after bonuses
            float flatBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.modifiers.magicSpellBaseFlat
                    : ctx.attacker.modifiers.physicalSpellBaseFlat
                        + ctx.attacker.modifiers.spellBaseFlat;

            float multBonus =
                ctx.spell.damageKind == DamageKind.Magical
                    ? ctx.attacker.modifiers.MagicSpellBaseMultFinal
                    : ctx.attacker.modifiers.PhysicalSpellBaseMultFinal
                        * ctx.attacker.modifiers.SpellBaseMultFinal;

            float spellFlatDamge = ctx.baseDamage + flatBonus;
            ctx.baseDamage = Mathf.FloorToInt(spellFlatDamge * multBonus);
        }
    }
}
