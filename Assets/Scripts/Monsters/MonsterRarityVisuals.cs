using UnityEngine;

public static class MonsterRarityVisuals
{
    public static Color GetColor(MonsterRarity rarity)
    {
        return rarity switch
        {
            MonsterRarity.Common => new Color(0.75f, 0.75f, 0.75f),
            MonsterRarity.Uncommon => new Color(0.4f, 0.85f, 0.4f),
            MonsterRarity.Rare => new Color(0.35f, 0.55f, 1f),
            MonsterRarity.Elite => new Color(0.85f, 0.55f, 0.15f),

            MonsterRarity.SpecialElite => new Color(0.85f, 0.35f, 0.85f),
            MonsterRarity.Lord => new Color(0.9f, 0.25f, 0.25f),
            MonsterRarity.HighLord => new Color(0.75f, 0.15f, 0.15f),
            MonsterRarity.GrandLord => new Color(0.6f, 0.1f, 0.1f),

            MonsterRarity.Mythical => new Color(0.3f, 1f, 0.9f),
            MonsterRarity.Primal => new Color(1f, 0.4f, 0.1f),
            MonsterRarity.God => new Color(1f, 0.85f, 0.25f),

            _ => Color.white,
        };
    }

    public static string GetDisplayName(MonsterRarity rarity)
    {
        return rarity switch
        {
            MonsterRarity.SpecialElite => "Special Elite",
            MonsterRarity.HighLord => "High Lord",
            MonsterRarity.GrandLord => "Grand Lord",
            _ => rarity.ToString(),
        };
    }
}
