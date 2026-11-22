using System;

namespace Hollowcrawl.Stats
{
    public enum ModifierOp
    {
        /// <summary>Added to the base before percentages.</summary>
        Flat = 0,
        /// <summary>Percent bonuses are summed, then applied once: x (1 + sum).</summary>
        PercentAdd = 1,
        /// <summary>Multipliers are applied one after another.</summary>
        Multiply = 2,
        /// <summary>Raises the floor of the final value (highest wins).</summary>
        Floor = 3,
        /// <summary>Lowers the ceiling of the final value (lowest wins).</summary>
        Ceiling = 4,
        /// <summary>Replaces the base value. Highest priority override wins.</summary>
        OverrideBase = 5,
    }

    /// <summary>Evaluated each time the stat sheet recalculates. Call StatSheet.Invalidate() when inputs change.</summary>
    public interface IStatCondition
    {
        bool IsMet();
    }

    /// <summary>Supplies a modifier value computed on demand (e.g. "+0.1 damage per coin held").</summary>
    public interface IStatValueProvider
    {
        float GetValue();
    }

    public sealed class StatModifier
    {
        private readonly float _value;
        private readonly IStatValueProvider _provider;

        public StatId Stat { get; }
        public ModifierOp Op { get; }
        /// <summary>The modifier's value; dynamic modifiers compute it on each recalculation.</summary>
        public float Value
        {
            get
            {
                if (_provider == null) return _value;
                float v = _provider.GetValue();
                return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
            }
        }
        public bool IsDynamic => _provider != null;
        /// <summary>Who added this modifier (item instance, status effect, character, curse...). Used for removal.</summary>
        public object Source { get; }
        /// <summary>Higher priority overrides win; for other ops it only orders debug output.</summary>
        public int Priority { get; }
        public IStatCondition Condition { get; }
        /// <summary>Optional tag for the debugger (e.g. "item:ember_sigil").</summary>
        public string Label { get; }

        public StatModifier(StatId stat, ModifierOp op, float value, object source, int priority = 0, IStatCondition condition = null, string label = null)
            : this(stat, op, value, null, source, priority, condition, label) { }

        public StatModifier(StatId stat, ModifierOp op, IStatValueProvider provider, object source, int priority = 0, IStatCondition condition = null, string label = null)
            : this(stat, op, 0f, provider ?? throw new ArgumentNullException(nameof(provider)), source, priority, condition, label) { }

        private StatModifier(StatId stat, ModifierOp op, float value, IStatValueProvider provider, object source, int priority, IStatCondition condition, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Modifier value must be finite.");
            Stat = stat;
            Op = op;
            _value = value;
            _provider = provider;
            Source = source;
            Priority = priority;
            Condition = condition;
            Label = label;
        }

        public bool IsActive => Condition == null || Condition.IsMet();

        public override string ToString() => $"{Label ?? Source?.ToString() ?? "?"}: {Stat} {Op} {Value}";
    }
}
