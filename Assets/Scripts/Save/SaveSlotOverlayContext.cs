using System;
using MyGame.Save;

public enum SaveSlotOverlayMode
{
    Load,
    Overwrite,
}

public sealed class SaveSlotOverlayContext
{
    public SaveSlotOverlayMode mode = SaveSlotOverlayMode.Load;

    // Only used when mode == Overwrite
    public SaveData pendingSave;

    // Optional: if you want the caller to react when a slot is chosen
    public Action<int, bool> onSlotChosen;
}
