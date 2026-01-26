using System;

namespace MyGame.Save
{
    /// <summary>
    /// Represents one of the fixed 4 active combat item slots.
    /// Only one of itemId / equipmentInstanceId should be set.
    /// </summary>
    [Serializable]
    public sealed class SavedCombatActiveSlotEntry
    {
        public string itemId;
        public string equipmentInstanceId;
    }
}
