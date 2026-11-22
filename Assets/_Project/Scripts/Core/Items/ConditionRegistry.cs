using System;
using System.Collections.Generic;
using System.Globalization;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Items
{
    /// <summary>
    /// Named, data-driven conditions. Expression grammar: terms separated by '&amp;' (all must pass) or
    /// '|' (any passes, lower precedence); '!' negates a term; arguments follow ':'.
    /// Examples: "health_full", "!has_temp_health", "has_tag:fire:2 &amp; luck_at_least:2", "character:wren|character:oslo".
    /// </summary>
    public sealed class ConditionRegistry
    {
        public delegate bool ConditionFn(InteractionEngine engine, EffectContext ctx, string[] args);

        private readonly Dictionary<string, ConditionFn> _conditions = new Dictionary<string, ConditionFn>(StringComparer.Ordinal);
        private readonly Dictionary<string, Term[][]> _parsed = new Dictionary<string, Term[][]>(StringComparer.Ordinal);

        public ConditionRegistry()
        {
            RegisterBuiltIns();
        }

        public void Register(string name, ConditionFn fn) => _conditions[name] = fn;
        public bool IsKnown(string name) => _conditions.ContainsKey(name);

        /// <summary>Returns a list of unknown condition names in an expression (for content validation).</summary>
        public List<string> FindUnknown(string expression)
        {
            var unknown = new List<string>();
            if (string.IsNullOrWhiteSpace(expression)) return unknown;
            foreach (var orGroup in Parse(expression))
                foreach (var term in orGroup)
                    if (!_conditions.ContainsKey(term.Name)) unknown.Add(term.Name);
            return unknown;
        }

        public bool Evaluate(string expression, InteractionEngine engine, EffectContext ctx)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;
            var groups = Parse(expression);
            for (int g = 0; g < groups.Length; g++)
            {
                bool all = true;
                var terms = groups[g];
                for (int t = 0; t < terms.Length && all; t++)
                {
                    var term = terms[t];
                    bool result = _conditions.TryGetValue(term.Name, out var fn) && fn(engine, ctx, term.Args);
                    all = term.Negate ? !result : result;
                }
                if (all) return true;
            }
            return false;
        }

        private sealed class Term
        {
            public string Name;
            public string[] Args;
            public bool Negate;
        }

        // Parsed form: [orGroup][andTerm]
        private Term[][] Parse(string expression)
        {
            if (_parsed.TryGetValue(expression, out var cached)) return cached;
            var orParts = expression.Split('|');
            var result = new Term[orParts.Length][];
            for (int o = 0; o < orParts.Length; o++)
            {
                var andParts = orParts[o].Split('&');
                result[o] = new Term[andParts.Length];
                for (int a = 0; a < andParts.Length; a++)
                {
                    string text = andParts[a].Trim();
                    bool negate = text.StartsWith("!", StringComparison.Ordinal);
                    if (negate) text = text.Substring(1).Trim();
                    var pieces = text.Split(':');
                    var args = new string[pieces.Length - 1];
                    Array.Copy(pieces, 1, args, 0, args.Length);
                    result[o][a] = new Term { Name = pieces[0], Args = args, Negate = negate };
                }
            }
            _parsed[expression] = result;
            return result;
        }

        private static int Arg(string[] args, int i, int fallback)
            => args.Length > i && int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static float ArgF(string[] args, int i, float fallback)
            => args.Length > i && float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private void RegisterBuiltIns()
        {
            Register("always", (e, c, a) => true);
            Register("health_full", (e, c, a) => e.Health != null && e.Health.Vigor >= e.Health.MaxVigor);
            Register("health_at_most", (e, c, a) => e.Health != null && e.Health.Vigor <= Arg(a, 0, 2));
            Register("total_health_at_most", (e, c, a) => e.Health != null && e.Health.TotalHalves <= Arg(a, 0, 2));
            Register("has_temp_health", (e, c, a) => e.Health != null && (e.Health.Ward + e.Health.Umbral) > 0);
            Register("no_containers", (e, c, a) => e.Health != null && e.Health.Containers == 0);
            Register("has_item", (e, c, a) => a.Length > 0 && e.HasItem(a[0]));
            Register("has_tag", (e, c, a) => a.Length > 0 && e.CountHeldWithTag(a[0]) >= Arg(a, 1, 1));
            Register("character", (e, c, a) => a.Length > 0 && string.Equals(e.CharacterId, a[0], StringComparison.Ordinal));
            Register("curse", (e, c, a) => a.Length > 0 && e.HasCurse(a[0]));
            Register("in_combat", (e, c, a) => e.InCombat);
            Register("room_type", (e, c, a) => a.Length > 0 && string.Equals(e.CurrentRoomType, a[0], StringComparison.Ordinal));
            Register("luck_at_least", (e, c, a) => e.Stats[StatId.Luck] >= ArgF(a, 0, 1f));
            Register("stat_at_least", (e, c, a) => a.Length > 1 && Enum.TryParse<StatId>(a[0], true, out var s) && e.Stats[s] >= ArgF(a, 1, 0f));
            Register("stat_below", (e, c, a) => a.Length > 1 && Enum.TryParse<StatId>(a[0], true, out var s) && e.Stats[s] < ArgF(a, 1, 0f));
            Register("coins_at_least", (e, c, a) => e.Resources.Get(Economy.ResourceKind.Coins) >= Arg(a, 0, 1));
            Register("bombs_at_least", (e, c, a) => e.Resources.Get(Economy.ResourceKind.Bombs) >= Arg(a, 0, 1));
            Register("keys_at_least", (e, c, a) => e.Resources.Get(Economy.ResourceKind.Keys) >= Arg(a, 0, 1));
            Register("stacks_at_least", (e, c, a) => c != null && c.Stacks >= Arg(a, 0, 2));
            Register("floor_at_least", (e, c, a) => e.FloorIndex >= Arg(a, 0, 1));
            Register("companions_at_least", (e, c, a) => e.CountHeldWithTag("companion") >= Arg(a, 0, 1));
            Register("transformed", (e, c, a) => a.Length > 0 ? e.HasTransformation(a[0]) : e.ActiveTransformationCount > 0);
        }
    }
}
