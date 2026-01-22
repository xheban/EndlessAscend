// File: Assets/Scripts/UI/KeyBindOverlayContext.cs (path can be whatever you prefer)
// Type: context DTO used when opening the overlay

using System;

public sealed class KeyBindOverlayContext
{
    /// <summary>Title shown at the top (e.g. "Bind", "Rebind", "Set Key").</summary>
    public string Title;

    /// <summary>Main instructions text.</summary>
    public string Message;

    /// <summary>A human-friendly label like "Slot 3" / "Jump" / "Open Inventory".</summary>
    public string TargetName;

    /// <summary>Currently assigned key (optional), shown initially.</summary>
    public UnityEngine.KeyCode CurrentKey;

    /// <summary>Called when user confirms binding.</summary>
    public Action<UnityEngine.KeyCode> OnBound;

    /// <summary>Called when user cancels or closes.</summary>
    public Action OnCancelled;

    /// <summary>If true, ignore mouse buttons for binding.</summary>
    public bool IgnoreMouseButtons = true;
}
