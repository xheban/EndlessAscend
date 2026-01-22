using System;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SavedItemStackEntry
    {
        public string itemId;
        public int quantity;
    }
}
