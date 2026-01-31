using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Array/Array Core Db", fileName = "ArrayCoreDb")]
public sealed class ArrayCoreDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<ArrayCoreDefinitionSO> entries = new();

    private Dictionary<string, ArrayCoreDefinitionSO> _byId;

    private void OnEnable()
    {
        BuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildLookup();
    }
#endif

    private void BuildLookup()
    {
        _byId = new Dictionary<string, ArrayCoreDefinitionSO>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                continue;

            _byId[entry.id] = entry;
        }
    }

    public ArrayCoreDefinitionSO GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (_byId == null)
            BuildLookup();

        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public IReadOnlyList<ArrayCoreDefinitionSO> All => entries;
}
