using UnityEngine;

namespace MyGame.Combat
{
    /// <summary>
    /// Merge of all damage rules
    /// </summary>
    public sealed class MergeAllDamageRule : IDamageRule
    {
        private readonly float _pct;

        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            int dmg = ctx.finalDamage;
            Debug.Log($"[MergeAllDamageRule] Final Damage: {ctx.finalDamage}");
            Debug.Log($"[MergeAllDamageRule] Damage Multiplier: {ctx.damageMult}");
            Debug.Log($"[MergeAllDamageRule] Flat Damage Bonus: {ctx.flatDamageBonus}");
        }
    }
}
