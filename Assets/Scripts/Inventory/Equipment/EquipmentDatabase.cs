using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Inventory/Equipment Database")]
public sealed class EquipmentDatabase : ScriptableObject
{
    [SerializeField]
    private List<EquipmentDefinitionSO> equipment = new List<EquipmentDefinitionSO>();

    private Dictionary<string, EquipmentDefinitionSO> _byId;

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
        _byId = new Dictionary<string, EquipmentDefinitionSO>();

        foreach (var e in equipment)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id))
                continue;

            _byId[e.id] = e;
        }
    }

    public EquipmentDefinitionSO GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (_byId == null)
            BuildLookup();

        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public string GetDisplayName(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        var def = GetById(id);
        return def != null ? def.displayName : id;
    }

    public Sprite GetIcon(string id)
    {
        var def = GetById(id);
        return def != null ? def.icon : null;
    }

    public IReadOnlyList<EquipmentDefinitionSO> All => equipment;
}
