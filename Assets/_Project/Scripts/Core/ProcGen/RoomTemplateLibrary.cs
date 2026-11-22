using System;
using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    /// <summary>Indexed collection of validated room templates.</summary>
    public sealed class RoomTemplateLibrary
    {
        private readonly List<RoomTemplate> _all = new List<RoomTemplate>();
        private readonly Dictionary<string, RoomTemplate> _byId = new Dictionary<string, RoomTemplate>(StringComparer.Ordinal);
        private readonly Dictionary<(RoomShape, RoomType), List<RoomTemplate>> _index = new Dictionary<(RoomShape, RoomType), List<RoomTemplate>>();

        public IReadOnlyList<RoomTemplate> All => _all;
        public int Count => _all.Count;

        /// <summary>Adds templates, skipping invalid ones. Returns the validation report for everything submitted.</summary>
        public ValidationReport AddRange(IEnumerable<RoomTemplate> templates)
        {
            var report = new ValidationReport();
            foreach (var t in templates)
            {
                var r = RoomTemplateValidator.Validate(t);
                report.Merge(r);
                if (!r.IsValid) continue;
                if (_byId.ContainsKey(t.Id)) { report.Error($"Duplicate template id '{t.Id}'."); continue; }
                Add(t);
            }
            foreach (var d in RoomTemplateValidator.FindDuplicates(_all)) report.Warn("Duplicate layout: " + d);
            return report;
        }

        private void Add(RoomTemplate t)
        {
            _all.Add(t);
            _byId[t.Id] = t;
            foreach (var type in t.Types)
            {
                var key = (t.Shape, type);
                if (!_index.TryGetValue(key, out var list)) _index[key] = list = new List<RoomTemplate>();
                list.Add(t);
            }
        }

        public RoomTemplate Get(string id) => id != null && _byId.TryGetValue(id, out var t) ? t : null;

        public IReadOnlyList<RoomTemplate> Get(RoomShape shape, RoomType type)
            => _index.TryGetValue((shape, type), out var list) ? list : (IReadOnlyList<RoomTemplate>)Array.Empty<RoomTemplate>();

        public bool Has(RoomShape shape, RoomType type, string theme)
        {
            foreach (var t in Get(shape, type)) if (t.SuitsTheme(theme)) return true;
            return false;
        }

        public int CountFor(RoomType type)
        {
            int n = 0;
            foreach (var t in _all) if (t.AllowsType(type)) n++;
            return n;
        }
    }
}
