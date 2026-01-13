using UnityEngine;

namespace MyGame.Combat
{
    public sealed class DefenderResistanceMitigationDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx)
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

            int flatResist = 0;
            float multResist = 1f;

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                flatResist += ctx.defender.modifiers.GetDefenderResistFlat(t);
                multResist *= ctx.defender.modifiers.GetDefenderResistMult(t); // e.g. *0.8
            }

            int outDmg = dmg - Mathf.Max(0, flatResist);
            outDmg = Mathf.RoundToInt(outDmg * multResist);

            ctx.finalDamage = Mathf.Max(0, outDmg);
        }
    }
}
