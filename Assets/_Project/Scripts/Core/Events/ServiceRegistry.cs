using System;
using System.Collections.Generic;

namespace Hollowcrawl.Events
{
    /// <summary>
    /// Minimal explicit service registry populated once by the bootstrap. Prefer constructor/field
    /// injection where practical; this exists so MonoBehaviours spawned at runtime can reach
    /// long-lived services (save, input, content database) without singletons scattered everywhere.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public bool Unregister<T>() where T : class => _services.Remove(typeof(T));

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException("Service not registered: " + typeof(T).Name);
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = null;
            return false;
        }

        public void Clear() => _services.Clear();
    }
}
