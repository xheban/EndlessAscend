using UnityEngine;

public static class KeybindPrefs
{
    public const int SpellSlotCount = 12;

    private const string SpellKeyPrefix = "Keybind.SpellSlot.";
    private const string SpellKeyVersion = "Keybind.SpellSlot.Version";

    public static int Version => PlayerPrefs.GetInt(SpellKeyVersion, 0);

    public static bool HasAnySpellBindings()
    {
        return PlayerPrefs.HasKey(SpellKeyPrefix + "0");
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
            PlayerPrefs.SetInt(SpellKeyVersion, Version + 1);
            PlayerPrefs.Save();
        }
    }

    public static KeyCode GetSpellSlotKey(int slotIndex)
    {
        EnsureSpellDefaults();
        return (KeyCode)
            PlayerPrefs.GetInt(SpellKeyPrefix + slotIndex, (int)DefaultKeyForSlot(slotIndex));
    }

    public static void SetSpellSlotKey(int slotIndex, KeyCode key)
    {
        EnsureSpellDefaults();

        PlayerPrefs.SetInt(SpellKeyPrefix + slotIndex, (int)key);
        PlayerPrefs.SetInt(SpellKeyVersion, Version + 1);
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
}
