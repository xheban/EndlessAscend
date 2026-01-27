using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Unlocks/Unlock Database", fileName = "UnlockDatabase")]
public sealed class UnlockDatabase : ScriptableObject
{
    [SerializeField]
    private List<UnlockDefinitionSO> unlocks = new List<UnlockDefinitionSO>();

    private Dictionary<string, UnlockDefinitionSO> _byId;

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
        _byId = new Dictionary<string, UnlockDefinitionSO>();

        foreach (var unlock in unlocks)
        {
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.unlockId))
                continue;

            _byId[unlock.unlockId] = unlock;
        }
    }

    public UnlockDefinitionSO GetById(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
            return null;

        if (_byId == null)
            BuildLookup();

        return _byId.TryGetValue(unlockId, out var def) ? def : null;
    }

    public string GetDisplayName(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
            return string.Empty;

        var def = GetById(unlockId);
        return def != null ? def.displayName : unlockId;
    }

    public Sprite GetIcon(string unlockId)
    {
        var def = GetById(unlockId);
        return def != null ? def.icon : null;
    }

    public IReadOnlyList<UnlockDefinitionSO> All => unlocks;
}
