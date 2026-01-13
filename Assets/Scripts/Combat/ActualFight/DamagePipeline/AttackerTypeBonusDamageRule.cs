using UnityEngine;

namespace MyGame.Combat
{
    public sealed class AttackerTypeBonusDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx)
        {
            ApplyTypes(
                ctx,
                getFlat: t => ctx.attacker.modifiers.GetAttackerBonusFlat(t),
                getMult: t => ctx.attacker.modifiers.GetAttackerBonusMult(t)
            );
        }

        private static void ApplyTypes(
            ActionContext ctx,
            System.Func<DamageType, int> getFlat,
            System.Func<DamageType, float> getMult
        )
        {
            int dmg = ctx.finalDamage;
            if (dmg <= 0)
            {
                ctx.finalDamage = 0;
                return;
            }

            var types = ctx.spell.damageTypes;
            if (types == null || types.Length == 0)
                types = new[] { DamageType.None };

            int flat = 0;
            float mult = 1f;

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                flat += getFlat(t);
                mult *= getMult(t);
            }

            int outDmg = dmg + flat;
            outDmg = Mathf.RoundToInt(outDmg * mult);
            ctx.finalDamage = Mathf.Max(0, outDmg);
        }
    }
}
