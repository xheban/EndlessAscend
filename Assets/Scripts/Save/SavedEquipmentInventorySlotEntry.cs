using System;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedEquipmentInventorySlotEntry
    {
        public int slotIndex;
        public string equipmentInstanceId;
    }
}
