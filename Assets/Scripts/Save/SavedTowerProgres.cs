using System;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedTowerProgress
    {
        public string towerId; // e.g. "TowerOfBeginnings"
        public int currentFloor = 1;
        public bool unlocked = false;
    }
}
