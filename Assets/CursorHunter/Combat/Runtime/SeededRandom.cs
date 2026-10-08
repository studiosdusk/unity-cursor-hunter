namespace CursorHunter.Combat
{
    /// <summary>
    /// Small deterministic PRNG used for run-local spawn and behavior
    /// decisions. It avoids UnityEngine.Random's global mutable state so a
    /// RunRequest seed can reproduce a spawn sequence.
    /// </summary>
    public sealed class SeededRandom
    {
        private const ulong NonZeroSeed = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = seed == 0UL ? NonZeroSeed : seed;
        }

        public float NextFloat(float minimumInclusive, float maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                return minimumInclusive;
            }

            double unit = NextUnit();
            return (float)(minimumInclusive +
                ((maximumExclusive - minimumInclusive) * unit));
        }

        public ulong NextUInt64()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return _state;
        }

        private double NextUnit()
        {
            ulong mantissa = NextUInt64() >> 11;
            return mantissa * (1.0 / 9007199254740992.0);
        }
    }
}
