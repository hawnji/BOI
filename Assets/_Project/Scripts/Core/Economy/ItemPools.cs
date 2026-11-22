using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Economy
{
    /// <summary>
    /// Run-scoped item pools. Each pool ("treasure", "shop", "boss", "secret", "curse", "trinket", ...)
    /// draws seeded, weighted by rarity and quality, never offers locked items, and removes what it
    /// offers so an item appears at most once per run. When a pool is exhausted Draw returns null and
    /// the caller substitutes a pickup reward. Held uniques are skipped via the interaction engine.
    /// </summary>
    public sealed class ItemPools
    {
        private readonly Dictionary<string, List<ItemDefinition>> _pools = new Dictionary<string, List<ItemDefinition>>(StringComparer.Ordinal);
        private readonly HashSet<string> _offered = new HashSet<string>(StringComparer.Ordinal);
        private readonly SeededRandom _rng;
        private readonly WeightedPicker<ItemDefinition> _picker = new WeightedPicker<ItemDefinition>();

        /// <summary>Relative weights per rarity (Common..Cursed).</summary>
        public float[] RarityWeights = { 1f, 0.6f, 0.3f, 0.1f, 0.25f };

        public ItemPools(IEnumerable<ItemDefinition> items, Func<string, bool> isUnlocked, SeededRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            var sorted = new List<ItemDefinition>(items);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id)); // deterministic regardless of load order
            foreach (var item in sorted)
            {
                if (item.Pools == null) continue;
                if (!string.IsNullOrEmpty(item.UnlockId) && (isUnlocked == null || !isUnlocked(item.UnlockId))) continue;
                foreach (var pool in item.Pools)
                {
                    if (!_pools.TryGetValue(pool, out var list)) _pools[pool] = list = new List<ItemDefinition>();
                    list.Add(item);
                }
            }
        }

        public int Remaining(string pool)
        {
            if (!_pools.TryGetValue(pool, out var list)) return 0;
            int n = 0;
            foreach (var i in list) if (!_offered.Contains(i.Id)) n++;
            return n;
        }

        public bool WasOffered(string itemId) => _offered.Contains(itemId);

        /// <summary>
        /// Draws an item. <paramref name="minQuality"/> / <paramref name="maxQuality"/> gate quality (e.g. boss rewards &gt;= 2).
        /// <paramref name="luck"/> shifts weight toward higher quality. Returns null only if nothing at all can be offered.
        /// </summary>
        public ItemDefinition Draw(string pool, InteractionEngine owner, float luck = 0f, int minQuality = 0, int maxQuality = 4, bool consume = true)
        {
            _picker.Clear();
            if (_pools.TryGetValue(pool, out var list))
            {
                foreach (var item in list)
                {
                    if (_offered.Contains(item.Id)) continue;
                    if (item.Quality < minQuality || item.Quality > maxQuality) continue;
                    if (owner != null && !owner.CanAccept(item)) continue;
                    float w = RarityWeights[Math.Max(0, Math.Min(RarityWeights.Length - 1, (int)item.Rarity))];
                    w *= 1f + Math.Max(-0.5f, Math.Min(1.5f, luck * 0.05f)) * (item.Quality - 1.5f) / 2f;
                    _picker.Add(item, Math.Max(0.01f, w));
                }
            }
            if (!_picker.TryPick(_rng, out var chosen))
            {
                // Relax quality gates before giving up.
                if (minQuality > 0 || maxQuality < 4) return Draw(pool, owner, luck, 0, 4, consume);
                return null;
            }
            if (consume) _offered.Add(chosen.Id);
            return chosen;
        }

        /// <summary>Puts an offered-but-not-taken item back (e.g. a shop that was rerolled).</summary>
        public void Return(string itemId) => _offered.Remove(itemId);

        /// <summary>Marks an item as already seen this run (starting items, items granted by events).</summary>
        public void MarkOffered(string itemId) => _offered.Add(itemId);
    }
}
