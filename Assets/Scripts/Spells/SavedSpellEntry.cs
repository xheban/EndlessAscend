using System;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedSpellEntry
    {
        public string spellId;
        public int level = 1;
        public int experience = 0;
        public int cooldownRemainingTurns = 0;
        public int activeSlotIndex = -1;
    }
}
