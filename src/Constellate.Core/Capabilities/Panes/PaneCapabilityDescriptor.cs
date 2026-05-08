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

public enum PaneCapabilityHostClass
{
    InvocationHost,
    MenuInvocationHost,
    CommandBarHost,
    TextDisplayHost,
    TextInputHost,
    CollectionBrowserHost,
    TreeBrowserHost,
    TableBrowserHost,
    InspectorHost,
    StreamViewerHost,
    ArchiveViewerHost,
    StatusBadgeHost,
    MetricsHost
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
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyList<PaneCapabilityHostClass>? CompatibleHostClasses = null)
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

    public IReadOnlyList<PaneCapabilityHostClass> EffectiveCompatibleHostClasses =>
        CompatibleHostClasses is { Count: > 0 }
            ? CompatibleHostClasses
            : InferCompatibleHostClasses();

    private PaneCapabilityHostClass[] InferCompatibleHostClasses()
    {
        var classes = new HashSet<PaneCapabilityHostClass>();

        foreach (var projectionForm in SupportedProjectionForms)
        {
            switch (projectionForm)
            {
                case PaneProjectionForm.Button:
                    classes.Add(PaneCapabilityHostClass.InvocationHost);
                    break;

                case PaneProjectionForm.Toolbar:
                    classes.Add(PaneCapabilityHostClass.CommandBarHost);
                    classes.Add(PaneCapabilityHostClass.InvocationHost);
                    break;

                case PaneProjectionForm.Menu:
                    classes.Add(PaneCapabilityHostClass.MenuInvocationHost);
                    break;

                case PaneProjectionForm.List:
                    classes.Add(PaneCapabilityHostClass.CollectionBrowserHost);
                    break;

                case PaneProjectionForm.Table:
                    classes.Add(PaneCapabilityHostClass.TableBrowserHost);
                    break;

                case PaneProjectionForm.Tree:
                    classes.Add(PaneCapabilityHostClass.TreeBrowserHost);
                    break;

                case PaneProjectionForm.Editor:
                    classes.Add(CapabilityKind == PaneCapabilityKind.Editor
                        ? PaneCapabilityHostClass.TextInputHost
                        : PaneCapabilityHostClass.TextDisplayHost);
                    break;

                case PaneProjectionForm.Console:
                    classes.Add(PaneCapabilityHostClass.StreamViewerHost);
                    break;

                case PaneProjectionForm.Card:
                case PaneProjectionForm.SurfaceBinding:
                case PaneProjectionForm.WorldProjection3D:
                    classes.Add(PaneCapabilityHostClass.InspectorHost);
                    break;

                case PaneProjectionForm.Graph:
                case PaneProjectionForm.Chart:
                    classes.Add(PaneCapabilityHostClass.MetricsHost);
                    break;

                case PaneProjectionForm.Badge:
                    classes.Add(PaneCapabilityHostClass.StatusBadgeHost);
                    break;
            }
        }

        if (IsInvokable)
        {
            classes.Add(PaneCapabilityHostClass.InvocationHost);
        }

        if (IsObservable)
        {
            classes.Add(PaneCapabilityHostClass.InspectorHost);
        }

        if (CapabilityKind == PaneCapabilityKind.Editor)
        {
            classes.Add(PaneCapabilityHostClass.TextInputHost);
        }

        if (CapabilityKind == PaneCapabilityKind.RuntimeFeed)
        {
            classes.Add(PaneCapabilityHostClass.StreamViewerHost);
        }

        if (CapabilityKind == PaneCapabilityKind.ArchiveView)
        {
            classes.Add(PaneCapabilityHostClass.ArchiveViewerHost);
        }

        if (CapabilityKind == PaneCapabilityKind.Browser)
        {
            classes.Add(PaneCapabilityHostClass.CollectionBrowserHost);
        }

        if (classes.Count == 0)
        {
            classes.Add(PaneCapabilityHostClass.TextDisplayHost);
        }

        var resolved = new PaneCapabilityHostClass[classes.Count];
        classes.CopyTo(resolved);
        return resolved;
    }
}
