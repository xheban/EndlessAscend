using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Player/Class Database")]
public class PlayerClassDatabase : ScriptableObject
{
    [SerializeField]
    private List<ClassSO> classes = new();

    private Dictionary<string, ClassSO> _byId;
    private Dictionary<string, SpecSO> _specById;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _byId = new Dictionary<string, ClassSO>(StringComparer.OrdinalIgnoreCase);
        _specById = new Dictionary<string, SpecSO>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in classes)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.id))
                continue;

            _byId[c.id] = c;

            // index specs too
            if (c.spec == null)
                continue;

            foreach (var s in c.spec)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.id))
                    continue;

                // Spec IDs should be globally unique OR you need (classId, specId) lookup.
                // We'll assume unique for now.
                _specById[s.id] = s;
            }
        }
    }

    public IReadOnlyList<ClassSO> AllClasses => classes;

    public ClassSO GetClass(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
            return null;

        return _byId.TryGetValue(classId, out var c) ? c : null;
    }

    public string GetClassName(string classId)
    {
        return GetClass(classId)?.displayName ?? string.Empty;
    }

    public SpecSO GetSpec(string specId)
    {
        if (string.IsNullOrWhiteSpace(specId))
            return null;

        return _specById.TryGetValue(specId, out var s) ? s : null;
    }

    public string GetSpecName(string specId)
    {
        return GetSpec(specId)?.displayName ?? string.Empty;
    }
}
