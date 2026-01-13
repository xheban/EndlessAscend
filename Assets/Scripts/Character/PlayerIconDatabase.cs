using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Player/Icon Database")]
public class PlayerIconDatabase : ScriptableObject
{
    [SerializeField]
    private List<PlayerIconDefinition> icons;

    private Dictionary<string, Sprite> _map;

    private void OnEnable()
    {
        _map = new Dictionary<string, Sprite>();
        foreach (var def in icons)
        {
            if (def != null && !string.IsNullOrEmpty(def.id))
                _map[def.id] = def.icon;
        }
    }

    public Sprite GetIcon(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return _map.TryGetValue(id, out var sprite) ? sprite : null;
    }
}
