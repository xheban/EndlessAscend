using UnityEngine;

namespace MyGame.Combat
{
    public sealed class AttackerWeakenMitigationDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
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

            int flatWeaken = 0;
            float multWeaken = 1f;

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                flatWeaken += attackerModifiers.GetAttackerWeakenFlat(t);
                multWeaken *= attackerModifiers.GetAttackerWeakenMult(t); // e.g. *0.8
            }
            ctx.flatDamageBonus -= flatWeaken;
            ctx.damageMult *= multWeaken;
        }
    }
}
