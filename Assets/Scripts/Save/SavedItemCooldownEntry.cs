using System;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedItemCooldownEntry
    {
        public string itemId;
        public int remainingTurns;
    }
}
