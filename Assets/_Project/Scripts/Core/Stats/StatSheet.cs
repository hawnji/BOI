using System;
using System.Collections.Generic;

namespace Hollowcrawl.Stats
{
    /// <summary>
    /// Computes final stat values from a base plus modifiers. Values are cached and recomputed lazily
    /// when a modifier is added/removed or Invalidate() is called. Formula per stat:
    ///   base' = highest-priority OverrideBase, else base
    ///   value = (base' + sum(Flat)) * (1 + sum(PercentAdd)) * product(Multiply)
    ///   value = clamp(value, max(globalMin, Floor...), min(globalMax, Ceiling...))
    /// </summary>
    public sealed class StatSheet
    {
        private readonly float[] _base = new float[(int)StatId.Count];
        private readonly float[] _cached = new float[(int)StatId.Count];
        private readonly bool[] _dirty = new bool[(int)StatId.Count];
        private readonly List<StatModifier>[] _mods = new List<StatModifier>[(int)StatId.Count];

        /// <summary>Raised after a stat's final value changes (old, new). Fired lazily on access or via RecalculateAll.</summary>
        public event Action<StatId, float, float> StatChanged;

        /// <summary>Safety cap: refuses new modifiers beyond this count per stat (prevents unbounded stacking bugs).</summary>
        public int MaxModifiersPerStat { get; set; } = 512;

        public StatSheet()
        {
            for (int i = 0; i < _mods.Length; i++)
            {
                _mods[i] = new List<StatModifier>();
                _base[i] = StatTable.Get((StatId)i).Base;
                _dirty[i] = true;
            }
            RecalculateAll();
        }

        public float GetBase(StatId id) => _base[(int)id];

        public void SetBase(StatId id, float value)
        {
            _base[(int)id] = value;
            MarkDirty(id);
        }

        public float this[StatId id] => Get(id);

        public float Get(StatId id)
        {
            int i = (int)id;
            if (_dirty[i]) Recalculate(id);
            return _cached[i];
        }

        public bool Add(StatModifier mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            var list = _mods[(int)mod.Stat];
            if (list.Count >= MaxModifiersPerStat) return false;
            list.Add(mod);
            MarkDirty(mod.Stat);
            return true;
        }

        public bool Remove(StatModifier mod)
        {
            if (mod == null) return false;
            bool removed = _mods[(int)mod.Stat].Remove(mod);
            if (removed) MarkDirty(mod.Stat);
            return removed;
        }

        /// <summary>Removes every modifier added by the given source. Returns the number removed.</summary>
        public int RemoveAllFromSource(object source)
        {
            int total = 0;
            for (int i = 0; i < _mods.Length; i++)
            {
                int n = _mods[i].RemoveAll(m => Equals(m.Source, source));
                if (n > 0) { total += n; MarkDirty((StatId)i); }
            }
            return total;
        }

        public IReadOnlyList<StatModifier> GetModifiers(StatId id) => _mods[(int)id];

        /// <summary>Marks every stat dirty (use when conditional modifiers' inputs changed).</summary>
        public void Invalidate()
        {
            for (int i = 0; i < _dirty.Length; i++) _dirty[i] = true;
        }

        /// <summary>Recomputes everything now and fires StatChanged for values that changed.</summary>
        public void RecalculateAll()
        {
            for (int i = 0; i < _cached.Length; i++)
            {
                _dirty[i] = true;
                Recalculate((StatId)i);
            }
        }

        private void MarkDirty(StatId id) => _dirty[(int)id] = true;

        private void Recalculate(StatId id)
        {
            int i = (int)id;
            var limits = StatTable.Get(id);
            float baseValue = _base[i];
            float flat = 0f, percent = 0f, mult = 1f;
            float floor = limits.Min, ceiling = limits.Max;
            int overridePriority = int.MinValue;

            var list = _mods[i];
            for (int m = 0; m < list.Count; m++)
            {
                var mod = list[m];
                if (!mod.IsActive) continue;
                switch (mod.Op)
                {
                    case ModifierOp.Flat: flat += mod.Value; break;
                    case ModifierOp.PercentAdd: percent += mod.Value; break;
                    case ModifierOp.Multiply: mult *= mod.Value; break;
                    case ModifierOp.Floor: if (mod.Value > floor) floor = mod.Value; break;
                    case ModifierOp.Ceiling: if (mod.Value < ceiling) ceiling = mod.Value; break;
                    case ModifierOp.OverrideBase:
                        if (mod.Priority > overridePriority) { overridePriority = mod.Priority; baseValue = mod.Value; }
                        break;
                }
            }

            float value = (baseValue + flat) * Math.Max(0f, 1f + percent) * mult;
            if (ceiling < floor) ceiling = floor; // a floor beats a conflicting ceiling
            if (value < floor) value = floor;
            if (value > ceiling) value = ceiling;

            float old = _cached[i];
            _cached[i] = value;
            _dirty[i] = false;
            if (Math.Abs(old - value) > 1e-6f) StatChanged?.Invoke(id, old, value);
        }
    }
}
