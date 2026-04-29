using System;

namespace Constellate.Persistence.Registry
{
    public sealed record ResourceRegistryRecord(
        string ResourceId,
        string TypeId,
        string Family,
        string ProviderId,
        string Origin,
        string EffectivePosture,
        string AuthorityMode,
        string Locality,
        string LifecycleState,
        string DisplayLabel,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string? PrimaryPayloadLocator = null);
}
