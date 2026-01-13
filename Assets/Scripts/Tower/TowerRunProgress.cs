using System.Collections.Generic;
using System.Diagnostics;

namespace MyGame.Run
{
    public sealed class TowerRunProgress
    {
        // towerId -> currentFloor
        public readonly Dictionary<string, int> floors = new Dictionary<string, int>();

        // towerId -> unlocked
        public readonly Dictionary<string, bool> unlocked = new Dictionary<string, bool>();

        public int GetFloor(string towerId)
        {
            return floors.TryGetValue(towerId, out var f) ? f : 1;
        }

        public void SetFloor(string towerId, int floor)
        {
            floors[towerId] = floor < 1 ? 1 : floor;
        }

        public bool IsUnlocked(string towerId)
        {
            return unlocked.TryGetValue(towerId, out var u) && u;
        }

        public void SetUnlocked(string towerId, bool value)
        {
            unlocked[towerId] = value;
        }

        public void CompleteFloor(string towerId, TowerFloorEntry floorEntry)
        {
            if (string.IsNullOrWhiteSpace(towerId))
                return;
            int current = GetFloor(towerId);
            int cleared = floorEntry.floor;
            if (current == cleared)
                SetFloor(towerId, current + 1);
        }
    }
}
