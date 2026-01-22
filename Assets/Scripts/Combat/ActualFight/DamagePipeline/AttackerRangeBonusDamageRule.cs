namespace MyGame.Combat
{
    public sealed class AttackerRangeBonusDamageRule : IDamageRule
    {
        public void Apply(ActionContext ctx, StatModifiers attackerModifiers)
        {
            if (ctx == null || ctx.spell == null || attackerModifiers == null)
                return;

            var range = ctx.spell.damageRangeType;

            ctx.flatDamageBonus += attackerModifiers.GetAttackerRangeBonusFlat(range);
            ctx.damageMult *= attackerModifiers.GetAttackerRangeBonusMult(range);
        }
    }
}
