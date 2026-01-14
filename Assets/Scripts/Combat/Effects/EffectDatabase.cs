using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Effects/Effect Database")]
public sealed class EffectDatabase : ScriptableObject
{
    [SerializeField]
    private List<EffectDefinition> effects = new List<EffectDefinition>();

    private Dictionary<string, EffectDefinition> _byId;

    private void OnEnable() => BuildLookup();

#if UNITY_EDITOR
    private void OnValidate() => BuildLookup();
#endif

    private void BuildLookup()
    {
        _byId = new Dictionary<string, EffectDefinition>();

        foreach (var e in effects)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.effectId))
                continue;

            _byId[e.effectId] = e;
        }
    }

    public EffectDefinition GetById(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return null;

        if (_byId == null)
            BuildLookup();

        return _byId.TryGetValue(effectId, out var def) ? def : null;
    }

    public IReadOnlyList<EffectDefinition> All => effects;
}
