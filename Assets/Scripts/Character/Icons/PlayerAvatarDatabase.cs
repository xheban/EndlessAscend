using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerAvatarEntry
{
    public string id; // "knight_m", "mage_f_02", etc.
    public string displayName; // optional
    public Sprite sprite; // the pixel art

    [Tooltip("If empty → allowed for all classes")]
    public List<string> allowedClassIds = new();
}

[CreateAssetMenu(menuName = "MyGame/Player/Avatar Database")]
public sealed class PlayerAvatarDatabase : ScriptableObject
{
    public List<PlayerAvatarEntry> avatars = new();

    private Dictionary<string, Sprite> _cache;

    public void BuildCache()
    {
        _cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in avatars)
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
        for (int i = 0; i < avatars.Count; i++)
            if (
                avatars[i] != null
                && string.Equals(avatars[i].id, id, StringComparison.OrdinalIgnoreCase)
            )
                return i;
        return -1;
    }

    public IReadOnlyList<PlayerAvatarEntry> GetForClass(string classId)
    {
        if (avatars == null || avatars.Count == 0)
            return Array.Empty<PlayerAvatarEntry>();
        if (string.IsNullOrWhiteSpace(classId))
            return Array.Empty<PlayerAvatarEntry>();

        classId = classId.Trim();

        var result = new List<PlayerAvatarEntry>(avatars.Count);
        foreach (var a in avatars)
        {
            if (a == null || a.sprite == null)
                continue;

            if (a.allowedClassIds == null || a.allowedClassIds.Count == 0)
            {
                result.Add(a); // universal
                continue;
            }

            for (int i = 0; i < a.allowedClassIds.Count; i++)
            {
                if (
                    string.Equals(a.allowedClassIds[i], classId, StringComparison.OrdinalIgnoreCase)
                )
                {
                    result.Add(a);
                    break;
                }
            }
        }
        return result;
    }
}
