using System;
using System.Collections.Generic;

namespace Hollowcrawl.Items
{
    /// <summary>
    /// Maps effect type keys used in content files to factories. New mechanics are added by writing
    /// one effect class and registering it here (or from a game module) - no engine changes needed.
    /// </summary>
    public sealed class EffectRegistry
    {
        private readonly Dictionary<string, Func<EffectSpec, IEffect>> _factories = new Dictionary<string, Func<EffectSpec, IEffect>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _docs = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Register(string type, Func<EffectSpec, IEffect> factory, string documentation = null)
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("Effect type key required.");
            _factories[type] = factory ?? throw new ArgumentNullException(nameof(factory));
            if (documentation != null) _docs[type] = documentation;
        }

        public bool IsRegistered(string type) => type != null && _factories.ContainsKey(type);
        public IEnumerable<string> Types => _factories.Keys;
        public string GetDocumentation(string type) => _docs.TryGetValue(type, out var d) ? d : null;

        public IEffect Create(EffectSpec spec)
        {
            if (spec?.Type == null || !_factories.TryGetValue(spec.Type, out var f)) return null;
            return f(spec);
        }

        /// <summary>Creates a registry with every built-in effect from the Effects folder.</summary>
        public static EffectRegistry CreateDefault()
        {
            var r = new EffectRegistry();
            Effects.BuiltInEffects.RegisterAll(r);
            return r;
        }
    }
}
