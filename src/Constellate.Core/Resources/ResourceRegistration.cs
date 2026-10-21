using System;

namespace Constellate.Core.Resources
{
    /// <summary>
    /// Current resource-catalog registration seam for identity, family, provider affiliation,
    /// locality, and lifecycle metadata. This is a meaningful foothold for canonical resource
    /// participation, but it should not be mistaken for full provider fulfillment or typed flow/runtime contracts.
    /// </summary>
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
