using System;
using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    /// <summary>
    /// Run-wide memory used by the anti-repetition weighting: which templates, bosses and enemy
    /// compositions were already used. Rebuilt deterministically when a run is regenerated from its seed.
    /// </summary>
    public sealed class GenerationHistory
    {
        public const int RecentTemplateWindow = 10;
        public const int RecentCompositionWindow = 3;

        private readonly Dictionary<string, int> _templateUses = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _templatesThisFloor = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _recentTemplates = new Queue<string>();
        private readonly Queue<string> _recentCompositions = new Queue<string>();
        private readonly HashSet<string> _bossesSeen = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<RoomType, int> _specialCounts = new Dictionary<RoomType, int>();

        public IReadOnlyCollection<string> BossesSeen => _bossesSeen;

        public void BeginFloor() => _templatesThisFloor.Clear();

        /// <summary>Multiplier applied to a template's weight (1 = fresh, near 0 = just used).</summary>
        public float TemplateFactor(string templateId)
        {
            if (_templatesThisFloor.Contains(templateId)) return 0.05f;
            float f = 1f;
            if (_templateUses.TryGetValue(templateId, out int uses)) f /= 1f + uses;
            if (_recentTemplates.Contains(templateId)) f *= 0.3f;
            return f;
        }

        public void UseTemplate(string templateId)
        {
            _templateUses.TryGetValue(templateId, out int n);
            _templateUses[templateId] = n + 1;
            _templatesThisFloor.Add(templateId);
            _recentTemplates.Enqueue(templateId);
            while (_recentTemplates.Count > RecentTemplateWindow) _recentTemplates.Dequeue();
        }

        public bool CompositionRecent(string key) => _recentCompositions.Contains(key);

        public void UseComposition(string key)
        {
            _recentCompositions.Enqueue(key);
            while (_recentCompositions.Count > RecentCompositionWindow) _recentCompositions.Dequeue();
        }

        public bool BossSeen(string id) => _bossesSeen.Contains(id);
        public void UseBoss(string id) { if (id != null) _bossesSeen.Add(id); }

        public int SpecialCount(RoomType t) => _specialCounts.TryGetValue(t, out var n) ? n : 0;
        public void UseSpecial(RoomType t) => _specialCounts[t] = SpecialCount(t) + 1;
        public int TemplateUses(string id) => _templateUses.TryGetValue(id, out var n) ? n : 0;
    }
}
