using System.Collections.Generic;

namespace MyGame.Inventory
{
    public sealed class PlayerItems
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int GetCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;
            return _counts.TryGetValue(itemId, out var count) ? count : 0;
        }

        public void Add(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return;

            _counts.TryGetValue(itemId, out var existing);
            _counts[itemId] = existing + amount;
        }

        public bool Remove(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return false;

            if (!_counts.TryGetValue(itemId, out var existing))
                return false;

            if (existing < amount)
                return false;

            int next = existing - amount;
            if (next <= 0)
                _counts.Remove(itemId);
            else
                _counts[itemId] = next;

            return true;
        }

        public void Clear() => _counts.Clear();
    }
}
