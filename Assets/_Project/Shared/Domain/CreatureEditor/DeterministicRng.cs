namespace Leeway.Creature.Domain
{
    /// <summary>
    /// An xorshift32 generator. Deliberately <b>not</b> <c>UnityEngine.Random</c> — the
    /// starter genome has to be reproducible from a seed and testable outside the Unity runtime.
    /// </summary>
    public struct DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            // Zero is a fixed point of xorshift, so we substitute a constant for it.
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>A number in [0, maxExclusive). Returns 0 when maxExclusive &lt;= 0.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>A floating-point number in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float NextRange(float min, float max) => min + (max - min) * NextFloat();
    }
}
