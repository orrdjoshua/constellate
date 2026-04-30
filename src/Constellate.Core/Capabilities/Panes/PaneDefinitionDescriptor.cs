using System.Collections.Generic;

namespace Constellate.Core.Capabilities.Panes;

public enum PaneDefinitionKind
{
    ChildPane,
    ParentWorkspace
}

public enum PaneElementKind
{
    Section,
    DefinitionHeader,
    TextBlock,
    LabelValueField,
    Button,
    CommandBar,
    ListBrowser,
    TreeBrowser,
    TableBrowser,
    TextEditor,
    PropertyEditor,
    InspectorGroup,
    StreamConsole,
    EventFeed,
    RuntimeActivityPanel,
    TaskMonitor,
    ResourceBrowser,
    CommandBrowser,
    CapabilityBrowser,
    ArchiveBrowser,
    FilterBar,
    TabsHost,
    SplitHost,
    StatusBadge,
    MetricsReadout,
    ProjectionStatusView
}

public enum PaneElementBindingTargetKind
{
    Capability,
    StateSelector,
    RuntimeFeed,
    ArchiveView,
    ResourceContext,
    ProjectionBinding,
    LiteralText,
    LayoutPolicy
}

public sealed record PaneElementBindingDescriptor(
    PaneElementBindingTargetKind TargetKind,
    string TargetRef,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record PaneElementDescriptor(
    string ElementId,
    PaneElementKind ElementKind,
    string DisplayLabel,
    PaneElementBindingDescriptor? Binding = null,
    IReadOnlyList<PaneElementDescriptor>? Children = null,
    IReadOnlyDictionary<string, string>? VisualSettings = null,
    IReadOnlyDictionary<string, string>? BehaviorSettings = null);

public sealed record PaneDefinitionDescriptor(
    string PaneDefinitionId,
    string DisplayLabel,
    PaneDefinitionKind DefinitionKind,
    bool IsSeeded,
    IReadOnlyList<PaneElementDescriptor> Elements,
    string? Description = null,
    IReadOnlyList<string>? Tags = null);

public sealed record PaneWorkspaceMemberDescriptor(
    string MemberId,
    string PaneDefinitionId,
    int Ordinal,
    string HostHint,
    int LaneIndex = 0,
    int SlideIndex = 0);

public sealed record PaneWorkspaceDescriptor(
    string WorkspaceId,
    string DisplayLabel,
    bool IsSeeded,
    IReadOnlyList<PaneWorkspaceMemberDescriptor> Members,
    string? Description = null,
    IReadOnlyList<string>? Tags = null);
