using System.Collections.Generic;
using MyGame.Save;
using UnityEngine.UIElements;

public static class FooterBinding
{
    // One place to define screen ids
    public const string ScreenCharacter = "character";
    public const string ScreenTower = "tower";
    public const string ScreenCity = "city";
    public const string ScreenHomestead = "homestead";

    // One place to define the tile->screen mapping
    private static readonly Dictionary<string, string> Map = new()
    {
        { "Character", ScreenCharacter },
        { "Tower", ScreenTower },
        { "City", ScreenCity },
        { "Homestead", ScreenHomestead },
    };

    public static FooterSectionController BindFooter(
        VisualElement root,
        ScreenSwapper swapper,
        string activeTileName,
        string footerRootName = "Footer"
    )
    {
        var footer = new FooterSectionController();
        footer.Bind(
            root,
            swapper,
            Map,
            footerRootName: footerRootName,
            activeTileName: activeTileName
        );
        ApplyUnlockRestrictions(footer);
        return footer;
    }

    private static void ApplyUnlockRestrictions(FooterSectionController footer)
    {
        bool hasSave = SaveSession.HasSave && SaveSession.Current != null;
        var unlocked = hasSave ? SaveSession.Current.unlockedIds : null;

        bool cityUnlocked = HasUnlock(unlocked, "unlock_city");
        bool homesteadUnlocked = HasUnlock(unlocked, "unlock_homestead");

        footer.SetEnabled("City", cityUnlocked);
        footer.SetEnabled("Homestead", homesteadUnlocked);

        int cityLevel = GetRequiredLevel("unlock_city");
        int homesteadLevel = GetRequiredLevel("unlock_homestead");

        footer.SetLockInfo(
            "City",
            visible: !cityUnlocked,
            titleText: "Unlocks at",
            requirementText: FormatLevelRequirement(cityLevel)
        );
        footer.SetLockInfo(
            "Homestead",
            visible: !homesteadUnlocked,
            titleText: "Unlocks at",
            requirementText: FormatLevelRequirement(homesteadLevel)
        );
    }

    private static bool HasUnlock(List<string> unlockedIds, string unlockId)
    {
        if (unlockedIds == null || string.IsNullOrWhiteSpace(unlockId))
            return false;

        for (int i = 0; i < unlockedIds.Count; i++)
        {
            if (string.Equals(unlockedIds[i], unlockId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static int GetRequiredLevel(string unlockId)
    {
        var db = MyGame.Run.GameConfigProvider.Instance?.UnlockDatabase;
        var def = db != null ? db.GetById(unlockId) : null;
        return def != null ? def.requiredLevel : -1;
    }

    private static string FormatLevelRequirement(int level)
    {
        if (level < 0)
            return string.Empty;
        return $"Level {level}";
    }
}
