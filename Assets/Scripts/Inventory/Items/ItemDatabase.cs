using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Inventory/Item Database")]
public sealed class ItemDatabase : ScriptableObject
{
    [SerializeField]
    private List<ItemDefinitionSO> items = new List<ItemDefinitionSO>();

    private Dictionary<string, ItemDefinitionSO> _byId;

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
        _byId = new Dictionary<string, ItemDefinitionSO>();

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;

            _byId[item.id] = item;
        }
    }

    public ItemDefinitionSO GetById(string id)
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

    public IReadOnlyList<ItemDefinitionSO> All => items;
}
