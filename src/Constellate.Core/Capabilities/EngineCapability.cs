using System;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Capabilities
{
    public sealed record EngineCapability(
        string Key,
        string DisplayName,
        string Category,
        string Provider,
        string Version,
        string? Description = null,
        string? CapabilityKind = null,
        string? SourceDomain = null,
        string? CatalogCapabilityId = null,
        bool IsCatalogBacked = false)
    {
        public static EngineCapability FromPaneCapabilityDescriptor(
            PaneCapabilityDescriptor descriptor,
            string provider = "PaneCatalog",
            string version = "seeded-v1")
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return new EngineCapability(
                Key: $"pane.catalog::{descriptor.CapabilityId}",
                DisplayName: descriptor.DisplayLabel,
                Category: $"{descriptor.CapabilityKind} / {descriptor.SourceDomain}",
                Provider: provider,
                Version: version,
                Description: descriptor.Description,
                CapabilityKind: descriptor.CapabilityKind.ToString(),
                SourceDomain: descriptor.SourceDomain.ToString(),
                CatalogCapabilityId: descriptor.CapabilityId,
                IsCatalogBacked: true);
        }
    }
}
