using System;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Persistence.CurrentState
{
    public sealed record PaneDefinitionRecord(
        string PaneDefinitionId,
        PaneDefinitionDescriptor Descriptor,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
