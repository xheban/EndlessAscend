using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Towers/Tower Definition", fileName = "Tower_")]
public class TowerDefinition : ScriptableObject
{
    [SerializeField]
    private string towerId;

    [Min(1)]
    [SerializeField]
    private int maxFloor = 100;

    [Header("Exact monster per floor")]
    [SerializeField]
    private List<TowerFloorEntry> floors = new();

    private Dictionary<int, TowerFloorEntry> _byFloor;

    public string TowerId => towerId;
    public int MaxFloor => maxFloor;

    public TowerFloorEntry GetFloor(int floor)
    {
        BuildCacheIfNeeded();
        _byFloor.TryGetValue(floor, out var entry);
        return entry;
    }

    private void BuildCacheIfNeeded()
    {
        if (_byFloor != null)
            return;

        _byFloor = new Dictionary<int, TowerFloorEntry>(floors.Count);
        foreach (var f in floors)
        {
            if (f == null)
                continue;

            if (_byFloor.ContainsKey(f.floor))
            {
                Debug.LogError(
                    $"[TowerDefinition] Duplicate floor {f.floor} in tower '{towerId}' ({name}).",
                    this
                );
                continue;
            }

            _byFloor.Add(f.floor, f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _byFloor = null;

        if (maxFloor < 1)
            maxFloor = 1;

        // Optional: warn if out-of-range floors exist
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i] == null)
                continue;
            if (floors[i].floor < 1 || floors[i].floor > maxFloor)
                Debug.LogWarning(
                    $"[TowerDefinition] Floor entry {floors[i].floor} is outside 1..{maxFloor} in '{towerId}'.",
                    this
                );
        }
    }
#endif
}
