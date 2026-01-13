public sealed class CombatTowerContext
{
    public readonly string towerId;
    public readonly int floor;

    public CombatTowerContext(string towerId, int floor)
    {
        this.towerId = towerId;
        this.floor = floor;
    }
}
