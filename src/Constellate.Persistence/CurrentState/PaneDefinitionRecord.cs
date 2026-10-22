using System;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Persistence.CurrentState
{
    /// <summary>
    /// Current-state row wrapper for authored pane-definition truth.
    /// This preserves durable identity and timestamp envelope around a broader
    /// pane-definition descriptor, which makes the lane more clarified than the
    /// adjacent node/project row shapes while still remaining broader than the
    /// narrower projection-binding state/record vocabulary.
    /// </summary>
    public sealed record PaneDefinitionRecord(
        string PaneDefinitionId,
        PaneDefinitionDescriptor Descriptor,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
