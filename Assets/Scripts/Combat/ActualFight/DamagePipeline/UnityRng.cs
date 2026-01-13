using UnityEngine;

namespace MyGame.Combat
{
    public sealed class UnityRng : IRng
    {
        public float Value01() => Random.value;

        public float Range(float min, float max) => Random.Range(min, max);

        public int RangeInt(int minInclusive, int maxExclusive) =>
            Random.Range(minInclusive, maxExclusive);
    }
}
