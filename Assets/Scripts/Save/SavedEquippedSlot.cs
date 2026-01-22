using System;
using MyName.Equipment;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedEquippedSlot
    {
        public EquipmentSlot slot;
        public string equipmentInstanceId; // instanceId (GUID) or empty
    }
}
