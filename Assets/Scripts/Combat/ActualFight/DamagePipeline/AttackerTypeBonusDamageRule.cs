using UnityEngine;

namespace MyGame.Combat
{
    public sealed class AttackerTypeBonusDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            ApplyTypes(
                ctx,
                getFlat: t => attackerModifiers.GetAttackerBonusFlat(t),
                getMult: t => attackerModifiers.GetAttackerBonusMult(t)
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
            Debug.Log("Flat je :" + flat);

            ctx.flatDamageBonus += flat;
            ctx.damageMult *= mult;
        }
    }
}
