namespace MyGame.Combat
{
    /// <summary>
    /// A rule that modifies damage-related values in the ActionContext.
    /// </summary>
    public interface IDamageRule
    {
        void Apply(ActionContext ctx, StatModifiers attackerModifiers);
    }
}
