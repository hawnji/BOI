using System;
using System.Collections.Generic;

namespace Hollowcrawl.Rng
{
    /// <summary>
    /// Deterministic PCG32 random number generator. Identical sequences on every platform
    /// (pure integer math, no reliance on System.Random or UnityEngine.Random).
    /// Use <see cref="Fork"/> to derive independent named streams so that adding a roll in
    /// one system (e.g. loot) never shifts the results of another (e.g. layout).
    /// </summary>
    public sealed class SeededRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private readonly ulong _increment;

        public ulong InitialSeed { get; }
        public ulong StreamId { get; }

        public SeededRandom(ulong seed, ulong stream = 0x5851F42D4C957F2DUL)
        {
            InitialSeed = seed;
            StreamId = stream;
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive). Unbiased (rejection sampling).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint bound = (uint)(maxExclusive - minInclusive);
            uint threshold = (uint)(-(int)bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return minInclusive + (int)(r % bound);
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive) => minInclusive + NextFloat() * (maxExclusive - minInclusive);

        public bool Chance(float probability) => probability > 0f && (probability >= 1f || NextFloat() < probability);

        public T Pick<T>(IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(list));
            return list[Range(0, list.Count)];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Derives an independent, reproducible stream identified by a name.</summary>
        public SeededRandom Fork(string streamName)
        {
            ulong h = StableHash.Fnv1a64(streamName);
            return new SeededRandom(InitialSeed ^ h, StreamId ^ (h * 0x9E3779B97F4A7C15UL));
        }

        /// <summary>Derives an independent stream identified by a name and an index (e.g. floor number).</summary>
        public SeededRandom Fork(string streamName, int index)
        {
            return Fork(streamName + "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Platform-stable hashing (string.GetHashCode is randomized per process in .NET Core).</summary>
    public static class StableHash
    {
        public static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                if (text == null) return hash;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    hash ^= (byte)(c & 0xFF);
                    hash *= 1099511628211UL;
                    hash ^= (byte)(c >> 8);
                    hash *= 1099511628211UL;
                }
                return hash;
            }
        }

        public static int Fnv1a32(string text)
        {
            ulong h = Fnv1a64(text);
            return (int)(h ^ (h >> 32));
        }
    }
}
