using System;
using System.Collections.Generic;

namespace MyGame.Combat
{
    /// <summary>
    /// Per-encounter runtime cooldown tracker for combat items.
    /// Ticks down by 1 each time the PLAYER performs an action.
    /// Not persisted to disk.
    /// </summary>
    [Serializable]
    public sealed class PlayerItemCooldownsRuntime
    {
        private readonly Dictionary<string, int> _remainingByItemId = new();

        public IEnumerable<KeyValuePair<string, int>> GetAllCooldowns() => _remainingByItemId;

        public int GetRemaining(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            return _remainingByItemId.TryGetValue(itemId, out var v) ? Math.Max(0, v) : 0;
        }

        public bool IsOnCooldown(string itemId) => GetRemaining(itemId) > 0;

        public void StartCooldown(string itemId, int turns)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            int t = Math.Max(0, turns);
            if (t <= 0)
            {
                _remainingByItemId.Remove(itemId);
                return;
            }

            _remainingByItemId[itemId] = t;
        }

        public void TickCooldowns()
        {
            if (_remainingByItemId.Count == 0)
                return;

            // Iterate over a copy to allow removals.
            var keys = new List<string>(_remainingByItemId.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string id = keys[i];
                if (!_remainingByItemId.TryGetValue(id, out var v))
                    continue;

                int next = Math.Max(0, v - 1);
                if (next <= 0)
                    _remainingByItemId.Remove(id);
                else
                    _remainingByItemId[id] = next;
            }
        }
    }
}
