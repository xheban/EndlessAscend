using System.Collections.Generic;
using UnityEngine.UIElements;

public static class FooterBinding
{
    // One place to define screen ids
    public const string ScreenCharacter = "character";
    public const string ScreenTower = "tower";
    public const string ScreenCity = "city";

    // One place to define the tile->screen mapping
    private static readonly Dictionary<string, string> Map = new()
    {
        { "Character", ScreenCharacter },
        { "Tower", ScreenTower },
        { "City", ScreenCity },
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
        return footer;
    }
}
