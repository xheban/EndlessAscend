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
    }
}
