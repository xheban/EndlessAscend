using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/City/Trainer Database", fileName = "TrainerDatabase")]
public sealed class TrainerDatabaseSO : ScriptableObject
{
    [Tooltip("All trainer definitions that can be looked up by trainerId.")]
    [SerializeField]
    private List<TrainerDefinitionSO> trainers = new();

    // trainerId -> trainer
    private Dictionary<string, TrainerDefinitionSO> _byId;

    private void OnEnable() => Rebuild();

#if UNITY_EDITOR
    private void OnValidate() => Rebuild();
#endif

    /// <summary>
    /// The only lookup you need: exact ID match (mage_trainer, warrior_trainer, ranger_trainer).
    /// </summary>
    public TrainerDefinitionSO GetById(string trainerId)
    {
        if (string.IsNullOrWhiteSpace(trainerId))
            return null;

        RebuildIfNeeded();

        _byId.TryGetValue(trainerId, out var found);
        return found;
    }

    private void RebuildIfNeeded()
    {
        if (_byId == null)
            Rebuild();
    }

    private void Rebuild()
    {
        _byId = new Dictionary<string, TrainerDefinitionSO>();

        if (trainers == null)
            return;

        for (int i = 0; i < trainers.Count; i++)
        {
            var t = trainers[i];
            if (t == null)
                continue;

            if (string.IsNullOrWhiteSpace(t.trainerId))
                continue;

            // First one wins (prevents random overrides if duplicates exist)
            if (!_byId.ContainsKey(t.trainerId))
                _byId.Add(t.trainerId, t);
        }
    }
}
