namespace MyGame.Combat
{
    public interface IHitRule
    {
        void Apply(ActionContext ctx);
    }
}
