using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Hollowcrawl.Content
{
    /// <summary>
    /// Loosely typed parameter dictionary for data-driven effect specs. Values come from JSON
    /// (long, double, bool, string, arrays) and are converted on read with defaults.
    /// </summary>
    public sealed class ParamBag
    {
        private readonly Dictionary<string, object> _values;

        public static readonly ParamBag Empty = new ParamBag();

        public ParamBag() { _values = new Dictionary<string, object>(StringComparer.Ordinal); }
        public ParamBag(IDictionary<string, object> values)
        {
            _values = values == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(values, StringComparer.Ordinal);
        }

        public IEnumerable<string> Keys => _values.Keys;
        public bool Has(string key) => _values.ContainsKey(key);

        public ParamBag Set(string key, object value) { _values[key] = value; return this; }

        public object GetRaw(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public float GetFloat(string key, float fallback = 0f)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return fallback;
            switch (v)
            {
                case float f: return f;
                case double d: return (float)d;
                case long l: return l;
                case int i: return i;
                case bool b: return b ? 1f : 0f;
                case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p): return p;
                default:
                    try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); } catch { return fallback; }
            }
        }

        public int GetInt(string key, int fallback = 0)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return fallback;
            switch (v)
            {
                case int i: return i;
                case long l: return (int)l;
                case double d: return (int)Math.Round(d);
                case float f: return (int)Math.Round(f);
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p): return p;
                default:
                    try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return fallback; }
            }
        }

        public bool GetBool(string key, bool fallback = false)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return fallback;
            switch (v)
            {
                case bool b: return b;
                case string s: return bool.TryParse(s, out var p) ? p : fallback;
                case long l: return l != 0;
                case int i: return i != 0;
                case double d: return Math.Abs(d) > double.Epsilon;
                default: return fallback;
            }
        }

        public string GetString(string key, string fallback = null)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return fallback;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public string[] GetStringArray(string key)
        {
            if (!_values.TryGetValue(key, out var v) || v == null) return Array.Empty<string>();
            if (v is string[] arr) return arr;
            if (v is string single) return new[] { single };
            if (v is IEnumerable e)
            {
                var list = new List<string>();
                foreach (var o in e) if (o != null) list.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
                return list.ToArray();
            }
            return Array.Empty<string>();
        }

        public TEnum GetEnum<TEnum>(string key, TEnum fallback) where TEnum : struct
        {
            var s = GetString(key);
            return s != null && Enum.TryParse<TEnum>(s, true, out var r) ? r : fallback;
        }
    }
}
