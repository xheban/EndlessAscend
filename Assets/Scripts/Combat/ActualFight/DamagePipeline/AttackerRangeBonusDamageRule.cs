namespace MyGame.Combat
{
    public sealed class AttackerRangeBonusDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            if (ctx == null || ctx.spell == null || attackerModifiers == null)
                return;

            var range = ctx.spell.damageRangeType;

            switch (range)
            {
                case DamageRangeType.Melee:
                    ctx.flatDamageBonus += attackerModifiers.GetMeleeDamageBonusFlat();
                    ctx.damageMult *= attackerModifiers.GetMeleeDamageBonusMult();
                    break;
                case DamageRangeType.Ranged:
                    ctx.flatDamageBonus += attackerModifiers.GetRangedDamageBonusFlat();
                    ctx.damageMult *= attackerModifiers.GetRangedDamageBonusMult();
                    break;
            }
        }
    }
}
