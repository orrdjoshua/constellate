using System.Collections.Generic;

namespace Constellate.Core.Capabilities.Panes;

public enum PaneCapabilityKind
{
    EngineCommand,
    ResourceAction,
    PortInvocation,
    ProjectionAffordance,
    StateSelector,
    RuntimeFeed,
    ArchiveView,
    Editor,
    Browser,
    Inspector,
    Selector,
    Visualizer
}

public enum PaneCapabilitySourceDomain
{
    EngineState,
    ResourceState,
    RuntimeStream,
    ArchiveHistory,
    ProjectionState,
    WorkspaceState,
    ProviderMetadata,
    ExternalReference
}

public enum PaneCapabilityAuthority
{
    ReadOnly,
    ProjectionLocal,
    EngineStateMutating,
    ResourceStateMutating,
    RuntimeControl,
    ExternalSideEffect,
    Destructive
}

public enum PaneCapabilityLifetime
{
    OneShot,
    ContinuousStream,
    CurrentSnapshot,
    HistoricalArchive,
    EditablePersistentState,
    TransientLocalState
}

public enum PaneCapabilityContext
{
    GlobalProject,
    CurrentWorld,
    CurrentSelection,
    FocusedNode,
    FocusedResource,
    ExplicitResource,
    ExplicitPort,
    CurrentPaneInstance,
    CurrentParentWorkspace,
    CurrentOperation,
    BackgroundContext
}

public enum PaneProjectionForm
{
    Button,
    Toolbar,
    Menu,
    List,
    Table,
    Tree,
    Editor,
    Console,
    Card,
    Graph,
    Chart,
    Badge,
    SurfaceBinding,
    WorldProjection3D
}

public sealed record PaneCapabilityDescriptor(
    string CapabilityId,
    string DisplayLabel,
    PaneCapabilityKind CapabilityKind,
    PaneCapabilitySourceDomain SourceDomain,
    PaneCapabilityAuthority Authority,
    PaneCapabilityLifetime Lifetime,
    PaneCapabilityContext DefaultContext,
    IReadOnlyList<PaneProjectionForm> SupportedProjectionForms,
    string? Description = null,
    string? OwnerKind = null,
    string? BindingTargetRef = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public bool IsInvokable =>
        CapabilityKind is PaneCapabilityKind.EngineCommand or
        PaneCapabilityKind.ResourceAction or
        PaneCapabilityKind.PortInvocation or
        PaneCapabilityKind.ProjectionAffordance;

    public bool IsObservable =>
        CapabilityKind is PaneCapabilityKind.StateSelector or
        PaneCapabilityKind.RuntimeFeed or
        PaneCapabilityKind.ArchiveView or
        PaneCapabilityKind.Inspector or
        PaneCapabilityKind.Visualizer;

    public bool IsAuthoringSurface =>
        CapabilityKind is PaneCapabilityKind.Editor or
        PaneCapabilityKind.Selector or
        PaneCapabilityKind.Browser;
}
