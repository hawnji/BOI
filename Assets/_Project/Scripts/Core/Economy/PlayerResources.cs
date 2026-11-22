using System;

namespace Hollowcrawl.Economy
{
    public enum ResourceKind { Coins = 0, Bombs = 1, Keys = 2, Embers = 3 }

    /// <summary>In-run countable resources. Embers are a rare special currency used by altars.</summary>
    public sealed class PlayerResources
    {
        public const int Cap = 99;
        private readonly int[] _values = new int[4];

        public event Action<ResourceKind, int, int> Changed; // kind, old, new

        public int Get(ResourceKind kind) => _values[(int)kind];

        public void Add(ResourceKind kind, int amount)
        {
            if (amount == 0) return;
            int old = _values[(int)kind];
            int v = old + amount;
            if (v < 0) v = 0;
            if (v > Cap) v = Cap;
            _values[(int)kind] = v;
            if (v != old) Changed?.Invoke(kind, old, v);
        }

        public bool TrySpend(ResourceKind kind, int amount)
        {
            if (amount < 0) return false;
            if (_values[(int)kind] < amount) return false;
            Add(kind, -amount);
            return true;
        }

        public void Set(ResourceKind kind, int value) => Add(kind, value - _values[(int)kind]);
    }
}
