using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Towers/Tower Database", fileName = "TowerDatabase")]
public class TowerDatabase : ScriptableObject
{
    [SerializeField]
    private List<TowerDefinition> towers = new();

    private Dictionary<string, TowerDefinition> _byId;

    public TowerDefinition GetById(string towerId)
    {
        BuildCacheIfNeeded();
        _byId.TryGetValue(towerId, out var tower);
        return tower;
    }

    private void BuildCacheIfNeeded()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, TowerDefinition>(towers.Count);

        foreach (var tower in towers)
        {
            if (tower == null || string.IsNullOrWhiteSpace(tower.TowerId))
                continue;

            if (_byId.ContainsKey(tower.TowerId))
            {
                Debug.LogError($"Duplicate TowerId '{tower.TowerId}' in TowerDatabase.", tower);
                continue;
            }

            _byId.Add(tower.TowerId, tower);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _byId = null;
    }
#endif
}
