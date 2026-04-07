using System;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public interface ICommandBus
    {
        void Send(Envelope command);
        event Action<Envelope>? Sent;

        // Returns a disposable to unsubscribe.
        IDisposable Subscribe(string name, Func<Envelope, bool> handler);
    }
}
