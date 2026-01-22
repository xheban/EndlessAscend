using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerIconEntry
{
    public string id; // "knight_m", "mage_f_02", etc.
    public string displayName; // optional
    public Sprite sprite; // the pixel art
}

[CreateAssetMenu(menuName = "MyGame/Player/Icon Database")]
public sealed class PlayerIconDatabase : ScriptableObject
{
    public List<PlayerIconEntry> icons = new();

    private Dictionary<string, Sprite> _cache;

    public void BuildCache()
    {
        _cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in icons)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id) || e.sprite == null)
                continue;
            _cache[e.id.Trim()] = e.sprite;
        }
    }

    public Sprite GetSpriteOrNull(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        _cache ??= null;
        if (_cache == null)
            BuildCache();
        return _cache.TryGetValue(id.Trim(), out var s) ? s : null;
    }

    public int IndexOfId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return -1;
        for (int i = 0; i < icons.Count; i++)
            if (
                icons[i] != null
                && string.Equals(icons[i].id, id, StringComparison.OrdinalIgnoreCase)
            )
                return i;
        return -1;
    }
}
