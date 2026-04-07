using System;
using System.Collections.Generic;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public static class EngineServices
    {
        public static ICommandBus CommandBus { get; private set; } = null!;
        public static IEventBus EventBus { get; private set; } = null!;

        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            var bus = new SimpleInProcBus();
            CommandBus = bus;
            EventBus = bus;
            _initialized = true;
        }

        private sealed class SimpleInProcBus : ICommandBus, IEventBus, IDisposable
        {
            private readonly object _gate = new();
            private readonly List<Func<Envelope, bool>> _commandHandlers = new();
            private readonly List<Func<Envelope, bool>> _eventHandlers = new();

            public event Action<Envelope>? Sent;
            public event Action<Envelope>? Published;

            public void Send(Envelope command)
            {
                Func<Envelope, bool>[] snapshot;
                lock (_gate) snapshot = _commandHandlers.ToArray();

                foreach (var handler in snapshot)
                {
                    try { handler(command); } catch { /* swallow for now; add logging later */ }
                }
                Sent?.Invoke(command);
            }

            public void Publish(Envelope evt)
            {
                Func<Envelope, bool>[] snapshot;
                lock (_gate) snapshot = _eventHandlers.ToArray();

                foreach (var handler in snapshot)
                {
                    try { handler(evt); } catch { /* swallow for now; add logging later */ }
                }
                Published?.Invoke(evt);
            }

            IDisposable ICommandBus.Subscribe(string name, Func<Envelope, bool> handler)
            {
                lock (_gate) _commandHandlers.Add(handler);
                return new Unsubscriber(() =>
                {
                    lock (_gate) _commandHandlers.Remove(handler);
                });
            }

            IDisposable IEventBus.Subscribe(string name, Func<Envelope, bool> handler)
            {
                lock (_gate) _eventHandlers.Add(handler);
                return new Unsubscriber(() =>
                {
                    lock (_gate) _eventHandlers.Remove(handler);
                });
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    _commandHandlers.Clear();
                    _eventHandlers.Clear();
                }
            }

            private sealed class Unsubscriber : IDisposable
            {
                private readonly Action _dispose;
                private bool _done;
                public Unsubscriber(Action dispose) => _dispose = dispose;
                public void Dispose()
                {
                    if (_done) return;
                    _done = true;
                    _dispose();
                }
            }
        }
    }
}
