using System;
using System.Globalization;
using System.Text;

namespace Hollowcrawl.Rng
{
    /// <summary>
    /// A run seed. Displayed to players as an 8-character code ("K7QX-M2PA") using an
    /// alphabet without easily confused characters (no 0/O, 1/I/L).
    /// </summary>
    public readonly struct RunSeed : IEquatable<RunSeed>
    {
        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // 31 symbols
        public const int CodeLength = 8;

        public uint Value { get; }

        public RunSeed(uint value) { Value = value; }

        public static RunSeed FromEntropy()
        {
            // Not used for gameplay determinism; only to choose a fresh seed.
            var g = Guid.NewGuid().ToByteArray();
            return new RunSeed(BitConverter.ToUInt32(g, 0) ^ (uint)Environment.TickCount);
        }

        /// <summary>Deterministic seed for a calendar date (daily mode works offline).</summary>
        public static RunSeed ForDate(DateTime dateUtc)
        {
            string key = "daily:" + dateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new RunSeed((uint)StableHash.Fnv1a32(key));
        }

        public string ToCode()
        {
            var sb = new StringBuilder(CodeLength + 1);
            ulong v = Value;
            for (int i = 0; i < CodeLength; i++)
            {
                if (i == 4) sb.Append('-');
                sb.Append(Alphabet[(int)(v % (ulong)Alphabet.Length)]);
                v /= (ulong)Alphabet.Length;
            }
            return sb.ToString();
        }

        public static bool TryParse(string code, out RunSeed seed)
        {
            seed = default;
            if (string.IsNullOrWhiteSpace(code)) return false;
            string cleaned = code.Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
            if (cleaned.Length != CodeLength) return false;
            ulong v = 0;
            for (int i = CodeLength - 1; i >= 0; i--)
            {
                int idx = Alphabet.IndexOf(cleaned[i]);
                if (idx < 0) return false;
                v = v * (ulong)Alphabet.Length + (ulong)idx;
            }
            if (v > uint.MaxValue) return false;
            seed = new RunSeed((uint)v);
            return true;
        }

        public SeededRandom CreateRandom() => new SeededRandom(Value);

        public bool Equals(RunSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RunSeed s && Equals(s);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => ToCode();
    }
}
