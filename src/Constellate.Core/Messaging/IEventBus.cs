using System;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public interface IEventBus
    {
        void Publish(Envelope evt);
        event Action<Envelope>? Published;

        // Returns a disposable to unsubscribe.
        IDisposable Subscribe(string name, Func<Envelope, bool> handler);
    }
}
