using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Capabilities
{
    public sealed class CapabilityRegistry : ICapabilityRegistry
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, EngineCapability> _capabilities = new(StringComparer.Ordinal);

        public IReadOnlyList<EngineCapability> GetAll()
        {
            lock (_gate)
            {
                return _capabilities.Values
                    .OrderBy(x => x.Category, StringComparer.Ordinal)
                    .ThenBy(x => x.DisplayName, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public bool TryGet(string key, out EngineCapability capability)
        {
            lock (_gate)
            {
                return _capabilities.TryGetValue(key, out capability!);
            }
        }

        public void Register(EngineCapability capability)
        {
            ArgumentNullException.ThrowIfNull(capability);

            if (string.IsNullOrWhiteSpace(capability.Key))
            {
                throw new ArgumentException("Capability key is required.", nameof(capability));
            }

            lock (_gate)
            {
                _capabilities[capability.Key] = capability;
            }
        }
    }
}
