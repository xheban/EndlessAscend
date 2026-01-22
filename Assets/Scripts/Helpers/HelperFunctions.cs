using MyGame.Common;

namespace MyGame.Helpers
{
    public static class HelperFunctions
    {
        public static int TierToFlatBonusMultiplier(Tier tier)
        {
            int tierIndex = tier switch
            {
                Tier.Tier1 => 1,
                Tier.Tier2 => 2,
                Tier.Tier3 => 3,
                Tier.Tier4 => 4,
                Tier.Tier5 => 5,
                Tier.Tier6 => 6,
                _ => 1,
            };

            return tierIndex;
        }

        public static string ToTierRoman(Tier tier)
        {
            return tier switch
            {
                Tier.Tier1 => "Tier I",
                Tier.Tier2 => "Tier II",
                Tier.Tier3 => "Tier III",
                Tier.Tier4 => "Tier IV",
                Tier.Tier5 => "Tier V",
                Tier.Tier6 => "Tier VI",
                _ => "Tier ?",
            };
        }
    }
}
