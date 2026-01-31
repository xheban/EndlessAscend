public sealed class HomesteadTabContext
{
    public ScreenSwapper Swapper;

    /// <summary>
    /// Context passed into Homestead via ScreenSwapper.ShowScreen(..., context).
    /// </summary>
    public object ScreenContext;
}
