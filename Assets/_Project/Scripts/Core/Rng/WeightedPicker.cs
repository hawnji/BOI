using System;
using System.Collections.Generic;

namespace Hollowcrawl.Rng
{
    /// <summary>Allocation-light weighted selection. Reusable: call Clear() and Add() again.</summary>
    public sealed class WeightedPicker<T>
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<float> _weights = new List<float>();
        private float _total;

        public int Count => _items.Count;
        public float TotalWeight => _total;

        public void Clear() { _items.Clear(); _weights.Clear(); _total = 0f; }

        public void Add(T item, float weight)
        {
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight)) return;
            _items.Add(item);
            _weights.Add(weight);
            _total += weight;
        }

        public bool TryPick(SeededRandom rng, out T result)
        {
            result = default;
            if (_items.Count == 0 || _total <= 0f) return false;
            float roll = rng.NextFloat() * _total;
            for (int i = 0; i < _items.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f) { result = _items[i]; return true; }
            }
            result = _items[_items.Count - 1];
            return true;
        }

        public T Pick(SeededRandom rng)
        {
            if (!TryPick(rng, out var r)) throw new InvalidOperationException("WeightedPicker is empty.");
            return r;
        }

        /// <summary>Picks and removes, so repeated calls never return the same entry.</summary>
        public bool TryTake(SeededRandom rng, out T result)
        {
            result = default;
            if (_items.Count == 0 || _total <= 0f) return false;
            float roll = rng.NextFloat() * _total;
            int chosen = _items.Count - 1;
            for (int i = 0; i < _items.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f) { chosen = i; break; }
            }
            result = _items[chosen];
            _total -= _weights[chosen];
            _items.RemoveAt(chosen);
            _weights.RemoveAt(chosen);
            return true;
        }
    }
}
