namespace MyGame.Combat
{
    /// <summary>
    /// Abstraction over randomness so combat can be deterministic in tests / replays.
    /// </summary>
    public interface IRng
    {
        /// <returns>Random float in [0, 1)</returns>
        float Value01();

        /// <returns>Random float in [min, max)</returns>
        float Range(float min, float max);

        /// <returns>Random int in [minInclusive, maxExclusive)</returns>
        int RangeInt(int minInclusive, int maxExclusive);
    }
}
