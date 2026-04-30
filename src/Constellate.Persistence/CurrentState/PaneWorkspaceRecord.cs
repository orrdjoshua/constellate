using System;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Persistence.CurrentState
{
    public sealed record PaneWorkspaceRecord(
        string WorkspaceId,
        PaneWorkspaceDescriptor Descriptor,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
