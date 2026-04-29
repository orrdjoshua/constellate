using System;

namespace Constellate.Persistence.Registry
{
    public sealed record ProviderRegistrationRecord(
        string RegistrationId,
        string ProviderId,
        string Origin,
        string ProviderKind,
        string InstalledVersion,
        string State,
        string CompatibilityResult,
        string TrustClass,
        bool Enabled,
        DateTimeOffset RegisteredAt,
        DateTimeOffset UpdatedAt,
        string? PackageId = null,
        string? ManifestHash = null);
}
