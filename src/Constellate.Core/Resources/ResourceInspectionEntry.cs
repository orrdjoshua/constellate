using System.Collections.Generic;

namespace Constellate.Core.Resources
{
    public sealed record ResourceInspectionEntry(
        ResourceRegistration Registration,
        string? ResourceTitle,
        bool IsAssignedToWorld,
        IReadOnlyList<string> AssignedNodeIds)
    {
        public bool IsUnassigned => !IsAssignedToWorld;

        public bool HasResourceTitle =>
            !string.IsNullOrWhiteSpace(ResourceTitle);
    }
}
