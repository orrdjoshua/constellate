using System;

namespace Constellate.Core.Resources
{
    public sealed record ResourceRegistration(
        ResourceId ResourceId,
        string TypeId,
        ResourceFamily Family,
        string ProviderId,
        ResourceOrigin Origin,
        ResourcePosture EffectivePosture,
        string AuthorityMode,
        string Locality,
        string LifecycleState,
        string DisplayLabel,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string? PrimaryPayloadLocator = null);
}
