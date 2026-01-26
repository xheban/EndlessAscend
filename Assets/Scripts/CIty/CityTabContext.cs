using UnityEngine;

public sealed class CityTabContext
{
    public ScreenSwapper Swapper;

    /// <summary>
    /// Context passed into City via ScreenSwapper.ShowScreen(..., context).
    /// </summary>
    public object ScreenContext;

    [Header("City Data")]
    public TrainerDatabaseSO TrainerDatabase;
}
