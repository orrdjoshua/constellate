using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Capabilities
{
    public sealed class CapabilityRegistry : ICapabilityRegistry
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, EngineCapability> _capabilities = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EngineCapability> _catalogBackedCapabilities = new(StringComparer.Ordinal);

        public CapabilityRegistry(IPaneCatalog? paneCatalog = null)
        {
            RegisterCatalogCapabilities(paneCatalog ?? new SeededPaneCatalog());
        }

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

        public IReadOnlyList<EngineCapability> GetCatalogBacked()
        {
            lock (_gate)
            {
                return _catalogBackedCapabilities.Values
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

        public bool TryGetByCatalogCapabilityId(string capabilityId, out EngineCapability capability)
        {
            lock (_gate)
            {
                return _catalogBackedCapabilities.TryGetValue(capabilityId, out capability!);
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
                RegisterInternal(capability);
            }
        }

        private void RegisterCatalogCapabilities(IPaneCatalog paneCatalog)
        {
            ArgumentNullException.ThrowIfNull(paneCatalog);

            lock (_gate)
            {
                foreach (var descriptor in paneCatalog.GetCapabilityDescriptors())
                {
                    RegisterInternal(EngineCapability.FromPaneCapabilityDescriptor(descriptor));
                }
            }
        }

        private void RegisterInternal(EngineCapability capability)
        {
            _capabilities[capability.Key] = capability;

            if (capability.IsCatalogBacked &&
                !string.IsNullOrWhiteSpace(capability.CatalogCapabilityId))
            {
                _catalogBackedCapabilities[capability.CatalogCapabilityId] = capability;
            }
        }
    }
}
