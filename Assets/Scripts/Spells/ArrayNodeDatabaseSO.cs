using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Array/Array Node Db", fileName = "ArrayNodeDb")]
public sealed class ArrayNodeDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<ArrayNodeDefinitionSO> entries = new();

    private Dictionary<string, ArrayNodeDefinitionSO> _byId;

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
        _byId = new Dictionary<string, ArrayNodeDefinitionSO>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                continue;

            _byId[entry.id] = entry;
        }
    }

    public ArrayNodeDefinitionSO GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (_byId == null)
            BuildLookup();

        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public IReadOnlyList<ArrayNodeDefinitionSO> All => entries;
}
