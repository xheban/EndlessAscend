using System;

namespace MyGame.Save
{
    /// <summary>
    /// Holds the currently loaded save for this play session.
    /// Accessible from anywhere.
    /// </summary>
    public static class SaveSession
    {
        public static event Action<SaveData> SessionStarted;
        public static int CurrentSlot { get; private set; } = -1;
        public static SaveData Current { get; private set; }

        public static bool HasSave => Current != null && CurrentSlot > 0;

        public static void SetCurrent(int slot, SaveData data)
        {
            CurrentSlot = slot;
            Current = data;

            SessionStarted?.Invoke(data);
        }

        public static void Clear()
        {
            CurrentSlot = -1;
            Current = null;
        }

        /// <summary>
        /// Writes the current in-memory save to disk into the loaded slot.
        /// </summary>
        public static void SaveNow()
        {
            if (!HasSave)
                return;

            SaveService.SaveToSlot(CurrentSlot, Current);
        }
    }
}
