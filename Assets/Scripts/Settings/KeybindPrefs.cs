using UnityEngine;

public static class KeybindPrefs
{
    public const int SpellSlotCount = 12;
    public const int ItemSlotCount = 4;

    private const string SpellKeyPrefix = "Keybind.SpellSlot.";
    private const string ItemKeyPrefix = "Keybind.ItemSlot.";

    // Kept for backward compatibility (older builds may have written this key)
    private const string KeybindVersionKey = "Keybind.SpellSlot.Version";

    public static int Version => PlayerPrefs.GetInt(KeybindVersionKey, 0);

    public static bool HasAnySpellBindings()
    {
        return PlayerPrefs.HasKey(SpellKeyPrefix + "0");
    }

    public static bool HasAnyItemBindings()
    {
        return PlayerPrefs.HasKey(ItemKeyPrefix + "0");
    }

    public static void EnsureSpellDefaults()
    {
        bool wroteAny = false;

        for (int i = 0; i < SpellSlotCount; i++)
        {
            string key = SpellKeyPrefix + i;
            if (PlayerPrefs.HasKey(key))
                continue;

            PlayerPrefs.SetInt(key, (int)DefaultKeyForSlot(i));
            wroteAny = true;
        }

        if (wroteAny)
        {
            PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
            PlayerPrefs.Save();
        }
    }

    public static void EnsureItemDefaults()
    {
        bool wroteAny = false;

        for (int i = 0; i < ItemSlotCount; i++)
        {
            string key = ItemKeyPrefix + i;
            if (PlayerPrefs.HasKey(key))
                continue;

            PlayerPrefs.SetInt(key, (int)DefaultKeyForItemSlot(i));
            wroteAny = true;
        }

        if (wroteAny)
        {
            PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
            PlayerPrefs.Save();
        }
    }

    public static KeyCode GetSpellSlotKey(int slotIndex)
    {
        EnsureSpellDefaults();
        return (KeyCode)
            PlayerPrefs.GetInt(SpellKeyPrefix + slotIndex, (int)DefaultKeyForSlot(slotIndex));
    }

    public static KeyCode GetItemSlotKey(int slotIndex)
    {
        EnsureItemDefaults();
        return (KeyCode)
            PlayerPrefs.GetInt(ItemKeyPrefix + slotIndex, (int)DefaultKeyForItemSlot(slotIndex));
    }

    public static void SetSpellSlotKey(int slotIndex, KeyCode key)
    {
        EnsureSpellDefaults();

        PlayerPrefs.SetInt(SpellKeyPrefix + slotIndex, (int)key);
        PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
        PlayerPrefs.Save();
    }

    public static void SetItemSlotKey(int slotIndex, KeyCode key)
    {
        EnsureItemDefaults();

        PlayerPrefs.SetInt(ItemKeyPrefix + slotIndex, (int)key);
        PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Replaces the key for a spell slot and clears the same key from ALL other spell/item slots.
    /// This ensures a key is unique across all bindings.
    /// </summary>
    public static void ReplaceSpellSlotKey(int slotIndex, KeyCode key)
    {
        EnsureSpellDefaults();
        EnsureItemDefaults();

        ReplaceKeyInternal(isSpellTarget: true, slotIndex: slotIndex, key: key);
    }

    /// <summary>
    /// Replaces the key for an item slot and clears the same key from ALL other spell/item slots.
    /// This ensures a key is unique across all bindings.
    /// </summary>
    public static void ReplaceItemSlotKey(int slotIndex, KeyCode key)
    {
        EnsureSpellDefaults();
        EnsureItemDefaults();

        ReplaceKeyInternal(isSpellTarget: false, slotIndex: slotIndex, key: key);
    }

    private static void ReplaceKeyInternal(bool isSpellTarget, int slotIndex, KeyCode key)
    {
        // Always allow clearing a binding without touching others.
        if (key == KeyCode.None)
        {
            if (isSpellTarget)
                PlayerPrefs.SetInt(SpellKeyPrefix + slotIndex, (int)KeyCode.None);
            else
                PlayerPrefs.SetInt(ItemKeyPrefix + slotIndex, (int)KeyCode.None);

            PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
            PlayerPrefs.Save();
            return;
        }

        // Clear duplicates from OTHER spell slots
        for (int i = 0; i < SpellSlotCount; i++)
        {
            if (isSpellTarget && i == slotIndex)
                continue;

            var existing = GetSpellSlotKey(i);
            if (existing == key)
                PlayerPrefs.SetInt(SpellKeyPrefix + i, (int)KeyCode.None);
        }

        // Clear duplicates from OTHER item slots
        for (int i = 0; i < ItemSlotCount; i++)
        {
            if (!isSpellTarget && i == slotIndex)
                continue;

            var existing = GetItemSlotKey(i);
            if (existing == key)
                PlayerPrefs.SetInt(ItemKeyPrefix + i, (int)KeyCode.None);
        }

        // Apply new binding
        if (isSpellTarget)
            PlayerPrefs.SetInt(SpellKeyPrefix + slotIndex, (int)key);
        else
            PlayerPrefs.SetInt(ItemKeyPrefix + slotIndex, (int)key);

        PlayerPrefs.SetInt(KeybindVersionKey, Version + 1);
        PlayerPrefs.Save();
    }

    private static KeyCode DefaultKeyForSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => KeyCode.Alpha1,
            1 => KeyCode.Alpha2,
            2 => KeyCode.Alpha3,
            3 => KeyCode.Alpha4,
            4 => KeyCode.Alpha5,
            5 => KeyCode.Alpha6,
            6 => KeyCode.Alpha7,
            7 => KeyCode.Alpha8,
            8 => KeyCode.Alpha9,
            9 => KeyCode.Alpha0,
            10 => KeyCode.Minus,
            11 => KeyCode.Equals,
            _ => KeyCode.None,
        };
    }

    private static KeyCode DefaultKeyForItemSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => KeyCode.Z,
            1 => KeyCode.X,
            2 => KeyCode.C,
            3 => KeyCode.V,
            _ => KeyCode.None,
        };
    }
}
