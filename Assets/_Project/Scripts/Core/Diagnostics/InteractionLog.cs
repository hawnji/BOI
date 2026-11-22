using System.Collections.Generic;

namespace Hollowcrawl.Items
{
    public enum InteractionLogKind { Inventory, Synergy, Transformation, Active, Proc, Guard, Error }

    public struct InteractionLogEntry
    {
        public long Index;
        public InteractionLogKind Kind;
        public string Source;
        public string Message;
        public override string ToString() => $"#{Index} [{Kind}] {Source}: {Message}";
    }

    /// <summary>Fixed-size ring buffer of recent engine events for the in-game/editor debugger.</summary>
    public sealed class InteractionLog
    {
        private readonly InteractionLogEntry[] _buffer;
        private long _count;

        public InteractionLog(int capacity) { _buffer = new InteractionLogEntry[capacity < 8 ? 8 : capacity]; }

        public long TotalLogged => _count;
        public int ErrorCount { get; private set; }
        public int GuardCount { get; private set; }

        public void Add(InteractionLogKind kind, string source, string message)
        {
            if (kind == InteractionLogKind.Error) ErrorCount++;
            if (kind == InteractionLogKind.Guard) GuardCount++;
            _buffer[_count % _buffer.Length] = new InteractionLogEntry { Index = _count, Kind = kind, Source = source, Message = message };
            _count++;
        }

        public IEnumerable<InteractionLogEntry> Entries()
        {
            long start = _count > _buffer.Length ? _count - _buffer.Length : 0;
            for (long i = start; i < _count; i++) yield return _buffer[i % _buffer.Length];
        }
    }

    public sealed class InteractionSnapshot
    {
        public sealed class EffectLine
        {
            public string Source;
            public string Type;
            public bool Enabled;
            public bool ConditionMet;
            public string Condition;
            public string SuppressedBy;
            public override string ToString()
                => $"{(Enabled ? "+" : "-")} {Source} :: {Type}" +
                   (Condition != null ? $" [if {Condition}: {ConditionMet}]" : "") +
                   (SuppressedBy != null ? $" (suppressed by {SuppressedBy})" : "");
        }

        public readonly List<string> Items = new List<string>();
        public readonly List<EffectLine> Effects = new List<EffectLine>();
        public readonly List<string> Synergies = new List<string>();
        public readonly List<string> Transformations = new List<string>();
        public readonly List<string> ProjectilePipeline = new List<string>();
        public readonly List<InteractionLogEntry> Log = new List<InteractionLogEntry>();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Items: " + string.Join(", ", Items));
            sb.AppendLine("Synergies: " + string.Join(", ", Synergies));
            sb.AppendLine("Transformations: " + string.Join(", ", Transformations));
            sb.AppendLine("Projectile pipeline:");
            foreach (var p in ProjectilePipeline) sb.AppendLine("  " + p);
            sb.AppendLine("Effects:");
            foreach (var e in Effects) sb.AppendLine("  " + e);
            return sb.ToString();
        }
    }
}
