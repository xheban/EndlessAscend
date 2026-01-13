using System;

public sealed class ChooseSpellSlotOverlayContext
{
    public string spellId; // the spell we want to activate
    public string spellName; // display name for header, optional
    public int unlockedSlots; // e.g. 4
    public SlotInfo[] slots; // current slot occupancy
    public Action<int> OnSlotChosen;

    public sealed class SlotInfo
    {
        public int slotIndex; // 0..unlockedSlots-1
        public string occupiedSpellId; // null if empty
        public string occupiedSpellName; // "Fireball" etc (optional)
    }
}
