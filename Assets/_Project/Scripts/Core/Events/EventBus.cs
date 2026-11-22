using System;
using System.Collections.Generic;

namespace Hollowcrawl.Events
{
    /// <summary>Marker for game events. Use structs to avoid per-publish allocations.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// Typed publish/subscribe bus for decoupled systems (UI, audio, progression, achievements).
    /// Safeguards: duplicate subscriptions are ignored, handlers may unsubscribe during dispatch,
    /// and nested publishing is capped to stop runaway event loops.
    /// Gameplay-critical item interactions use <see cref="Hollowcrawl.Items.InteractionEngine"/>, which
    /// has ordering and recursion rules of its own; this bus is for broadcast notifications.
    /// </summary>
    public sealed class EventBus
    {
        public const int MaxPublishDepth = 16;

        private interface IChannel { int Count { get; } }

        private sealed class Channel<T> : IChannel where T : struct, IGameEvent
        {
            public readonly List<Action<T>> Handlers = new List<Action<T>>();
            public readonly List<Action<T>> Snapshot = new List<Action<T>>();
            public int Count => Handlers.Count;
        }

        private readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();
        private int _depth;

        /// <summary>Raised when a publish was dropped because of excessive nesting.</summary>
        public event Action<Type> RecursionLimitHit;
        /// <summary>Raised when a handler throws; dispatch continues for the other handlers.</summary>
        public event Action<Exception> HandlerFailed;

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var ch = GetChannel<T>();
            if (!ch.Handlers.Contains(handler)) ch.Handlers.Add(handler);
            return new Subscription<T>(this, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (_channels.TryGetValue(typeof(T), out var c)) ((Channel<T>)c).Handlers.Remove(handler);
        }

        public int SubscriberCount<T>() where T : struct, IGameEvent
            => _channels.TryGetValue(typeof(T), out var c) ? c.Count : 0;

        public void Publish<T>(in T evt) where T : struct, IGameEvent
        {
            if (!_channels.TryGetValue(typeof(T), out var c)) return;
            if (_depth >= MaxPublishDepth)
            {
                RecursionLimitHit?.Invoke(typeof(T));
                return;
            }
            var ch = (Channel<T>)c;
            if (ch.Handlers.Count == 0) return;

            // Copy so handlers can (un)subscribe safely. Nested publishes of the same type use a fresh copy.
            List<Action<T>> snapshot = _depth == 0 ? ch.Snapshot : new List<Action<T>>(ch.Handlers.Count);
            snapshot.Clear();
            snapshot.AddRange(ch.Handlers);
            _depth++;
            try
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    try { snapshot[i](evt); }
                    catch (Exception ex) { HandlerFailed?.Invoke(ex); }
                }
            }
            finally
            {
                _depth--;
                if (_depth == 0) ch.Snapshot.Clear();
            }
        }

        public void Clear() => _channels.Clear();

        private Channel<T> GetChannel<T>() where T : struct, IGameEvent
        {
            if (!_channels.TryGetValue(typeof(T), out var c))
            {
                c = new Channel<T>();
                _channels[typeof(T)] = c;
            }
            return (Channel<T>)c;
        }

        private sealed class Subscription<T> : IDisposable where T : struct, IGameEvent
        {
            private EventBus _bus;
            private Action<T> _handler;
            public Subscription(EventBus bus, Action<T> handler) { _bus = bus; _handler = handler; }
            public void Dispose()
            {
                _bus?.Unsubscribe(_handler);
                _bus = null;
                _handler = null;
            }
        }
    }
}
