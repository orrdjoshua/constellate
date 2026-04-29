using System.Collections.Generic;

namespace Constellate.Core.Resources
{
    public sealed record ResourceInspectionEntry(
        ResourceRegistration Registration,
        bool IsAssignedToWorld,
        IReadOnlyList<string> AssignedNodeIds)
    {
        public bool IsUnassigned => !IsAssignedToWorld;
    }
}
