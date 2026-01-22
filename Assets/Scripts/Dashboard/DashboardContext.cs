public sealed class DashboardContext
{
    public string Hero; // or your own data type
    public string Spells; // etc.

    public UiEventHub Events = new UiEventHub();
}
