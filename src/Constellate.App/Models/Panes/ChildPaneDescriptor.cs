using System;
using System.Collections.Generic;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.App;

public enum ChildPaneSourcePosture
{
    Unspecified,
    FromDefinition,
    DetachedFromDefinition,
    CreatedLocalOnly
}

public enum ChildPaneDefinitionOriginKind
{
    Unknown,
    Seeded,
    UserAuthored,
    PluginContributed,
    LocalOnly
}

public enum ChildPaneDefinitionSyncPosture
{
    NotApplicable,
    InSyncWithBaseRevision,
    BehindCurrentDefinitionRevision
}

public enum ChildPaneLocalOverrideAxis
{
    Title,
    Description,
    Appearance,
    CanvasComposition
}

public sealed record ChildPaneCanvasViewportState(
    double PanX = 0,
    double PanY = 0,
    double Zoom = 1.0,
    double ViewportWidth = 0,
    double ViewportHeight = 0)
{
    public static ChildPaneCanvasViewportState Default => new();

    public double NormalizedZoom =>
        Math.Clamp(Zoom, 0.25, 4.0);

    public ChildPaneCanvasViewportState Normalized =>
        this with
        {
            Zoom = NormalizedZoom,
            ViewportWidth = Math.Max(0, ViewportWidth),
            ViewportHeight = Math.Max(0, ViewportHeight)
        };

    public string Summary =>
        $"Canvas viewport: pan=({PanX:0.#}, {PanY:0.#}) · zoom={NormalizedZoom:0.##}x";

    public ChildPaneCanvasViewportState WithPan(double deltaX, double deltaY)
    {
        var normalized = Normalized;
        return normalized with
        {
            PanX = normalized.PanX + deltaX,
            PanY = normalized.PanY + deltaY
        };
    }

    public ChildPaneCanvasViewportState WithZoomDelta(double deltaZoom)
    {
        var normalized = Normalized;
        return normalized with
        {
            Zoom = Math.Clamp(normalized.Zoom + deltaZoom, 0.25, 4.0)
        };
    }
}

public sealed record ChildPaneCanvasElementPreviewPlacement(
    double X,
    double Y,
    double? Width = null,
    double? Height = null)
{
    public ChildPaneCanvasElementPreviewPlacement WithPosition(double x, double y)
    {
        return this with
        {
            X = Math.Max(0, x),
            Y = Math.Max(0, y)
        };
    }

    public ChildPaneCanvasElementPreviewPlacement WithSize(double width, double height)
    {
        return this with
        {
            Width = Math.Max(120, width),
            Height = Math.Max(64, height)
        };
    }

    public double ResolveWidth(double fallbackWidth) =>
        Width is > 0 ? Width.Value : fallbackWidth;

    public double ResolveHeight(double fallbackHeight) =>
        Height is > 0 ? Height.Value : fallbackHeight;
}

public sealed record ChildPaneCanvasAuthoringState(
    ChildPaneCanvasViewportState Viewport,
    IReadOnlyDictionary<string, ChildPaneCanvasElementPreviewPlacement> ElementPlacements,
    string? SelectedElementInstanceId)
{
    public int AuthoredElementOverrideCount =>
        ElementPlacements.Count;

    public bool HasAuthoredElementOverrides =>
        AuthoredElementOverrideCount > 0;

    public bool HasSelection =>
        !string.IsNullOrWhiteSpace(SelectedElementInstanceId);

    public string Summary =>
        HasAuthoredElementOverrides
            ? $"Pane-instance canvas working copy: {AuthoredElementOverrideCount} local element override(s){(HasSelection ? $" · selected {SelectedElementInstanceId}" : string.Empty)}"
            : HasSelection
                ? $"Pane-instance canvas working copy: no local element overrides yet · selected {SelectedElementInstanceId}"
                : "Pane-instance canvas working copy: no local element overrides yet.";

    public ChildPaneCanvasElementPreviewPlacement? TryGetElementPlacement(string? elementInstanceId)
    {
        if (string.IsNullOrWhiteSpace(elementInstanceId) ||
            !ElementPlacements.TryGetValue(elementInstanceId.Trim(), out var placement))
        {
            return null;
        }

        return placement;
    }
}

public sealed record ChildPaneAuthoredValues(
    string Title,
    string Description,
    string AppearanceVariant);

public sealed record ChildPaneAuthoredState(
    ChildPaneAuthoredValues Current,
    ChildPaneAuthoredValues Baseline)
{
    public bool HasTitleOverride =>
        !string.Equals(Current.Title, Baseline.Title, StringComparison.Ordinal);

    public bool HasDescriptionOverride =>
        !string.Equals(Current.Description, Baseline.Description, StringComparison.Ordinal);

    public bool HasAppearanceOverride =>
        !string.Equals(Current.AppearanceVariant, Baseline.AppearanceVariant, StringComparison.Ordinal);

    public bool HasAnyLocalOverride =>
        HasTitleOverride || HasDescriptionOverride || HasAppearanceOverride;

    public int OverrideAxisCount =>
        (HasTitleOverride ? 1 : 0) +
        (HasDescriptionOverride ? 1 : 0) +
        (HasAppearanceOverride ? 1 : 0);

    public IReadOnlyList<ChildPaneLocalOverrideAxis> ActiveOverrideAxes =>
        GetActiveOverrideAxes();

    public IReadOnlyList<string> ActiveOverrideAxisLabels =>
        GetActiveOverrideAxisLabels();

    public string TitleSummary =>
        HasTitleOverride
            ? $"Current title: {FormatTitleLabel(Current.Title)} · baseline: {FormatTitleLabel(Baseline.Title)}"
            : $"Current title: {FormatTitleLabel(Current.Title)}";

    public string CurrentSummary =>
        $"Current values: title={FormatTitleLabel(Current.Title)} · description={FormatDescriptionLabel(Current.Description)} · appearance={GetAppearanceVariantLabel(Current.AppearanceVariant)}";

    public string BaselineSummary =>
        $"Baseline values: title={FormatTitleLabel(Baseline.Title)} · description={FormatDescriptionLabel(Baseline.Description)} · appearance={GetAppearanceVariantLabel(Baseline.AppearanceVariant)}";

    public string OverrideAxisStatusSummary =>
        !HasAnyLocalOverride
            ? "No authored-value overrides are active."
            : $"Active authored-value overrides: {string.Join(" · ", ActiveOverrideAxisLabels)}";

    public string TitleCurrentValueSummary =>
        $"Current title: {FormatTitleLabel(Current.Title)}";

    public string TitleBaselineValueSummary =>
        $"Baseline title: {FormatTitleLabel(Baseline.Title)}";

    public string DescriptionCurrentValueSummary =>
        $"Current description: {FormatDescriptionLabel(Current.Description)}";

    public string DescriptionBaselineValueSummary =>
        $"Baseline description: {FormatDescriptionLabel(Baseline.Description)}";

    public string DescriptionSummary =>
        HasDescriptionOverride
            ? $"Current description: {FormatDescriptionLabel(Current.Description)} · baseline: {FormatDescriptionLabel(Baseline.Description)}"
            : $"Current description: {FormatDescriptionLabel(Current.Description)}";

    public string AppearanceSummary =>
        HasAppearanceOverride
            ? $"Current appearance: {GetAppearanceVariantLabel(Current.AppearanceVariant)} · baseline: {GetAppearanceVariantLabel(Baseline.AppearanceVariant)}"
            : $"Current appearance: {GetAppearanceVariantLabel(Current.AppearanceVariant)}";

    public string AppearanceCurrentValueSummary =>
        $"Current appearance: {GetAppearanceVariantLabel(Current.AppearanceVariant)}";

    public string AppearanceBaselineValueSummary =>
        $"Baseline appearance: {GetAppearanceVariantLabel(Baseline.AppearanceVariant)}";

    private ChildPaneLocalOverrideAxis[] GetActiveOverrideAxes()
    {
        if (!HasAnyLocalOverride)
        {
            return Array.Empty<ChildPaneLocalOverrideAxis>();
        }

        var axes = new ChildPaneLocalOverrideAxis[OverrideAxisCount];
        var index = 0;

        if (HasTitleOverride)
        {
            axes[index++] = ChildPaneLocalOverrideAxis.Title;
        }

        if (HasDescriptionOverride)
        {
            axes[index++] = ChildPaneLocalOverrideAxis.Description;
        }

        if (HasAppearanceOverride)
        {
            axes[index] = ChildPaneLocalOverrideAxis.Appearance;
        }

        return axes;
    }

    private string[] GetActiveOverrideAxisLabels()
    {
        var axes = ActiveOverrideAxes;
        if (axes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var labels = new string[axes.Count];
        for (var i = 0; i < axes.Count; i++)
        {
            labels[i] = GetAxisLabel(axes[i]);
        }

        return labels;
    }

    private static string GetAxisLabel(ChildPaneLocalOverrideAxis axis)
    {
        return axis switch
        {
            ChildPaneLocalOverrideAxis.Title => "Title",
            ChildPaneLocalOverrideAxis.Description => "Description",
            ChildPaneLocalOverrideAxis.Appearance => "Appearance",
            ChildPaneLocalOverrideAxis.CanvasComposition => "Canvas",
            _ => "Unknown"
        };
    }

    private static string GetAppearanceVariantLabel(string variant)
    {
        return variant switch
        {
            "cool" => "Cool",
            "warm" => "Warm",
            _ => "Default"
        };
    }

    private static string FormatTitleLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(untitled)"
            : value;
    }

    private static string FormatDescriptionLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(empty)"
            : value;
    }
}

public sealed record ChildPaneWorkingCopyState(
    ChildPaneSourcePosture SourcePosture,
    ChildPaneDefinitionSyncPosture DefinitionSyncPosture,
    bool HasSavedInstanceState,
    IReadOnlyList<ChildPaneLocalOverrideAxis> ActiveOverrideAxes)
{
    public bool HasLocalOverrides => ActiveOverrideAxes.Count > 0;

    public int LocalOverrideAxisCount => ActiveOverrideAxes.Count;

    public IReadOnlyList<string> ActiveOverrideAxisLabels => GetActiveOverrideAxisLabels();

    public IReadOnlyList<string> StatusBadgeLabels => GetStatusBadgeLabels();

    public bool HasStatusBadges =>
        StatusBadgeLabels.Count > 0;

    public bool HasDefinitionSyncSummary =>
        !string.IsNullOrWhiteSpace(DefinitionSyncSummary);

    public string OverrideSummary =>
        !HasLocalOverrides
            ? "No local instance overrides."
            : $"Local overrides: {string.Join(" · ", ActiveOverrideAxisLabels)}";

    public string SourcePostureSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => "Source posture: definition-backed instance",
            ChildPaneSourcePosture.DetachedFromDefinition => "Source posture: detached local pane",
            ChildPaneSourcePosture.CreatedLocalOnly => "Source posture: local-only pane",
            _ => "Source posture: unspecified"
        };

    public string DefinitionSyncSummary =>
        SourcePosture != ChildPaneSourcePosture.FromDefinition
            ? string.Empty
            : DefinitionSyncPosture switch
            {
                ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision => "Definition sync: in sync with base definition",
                ChildPaneDefinitionSyncPosture.BehindCurrentDefinitionRevision => "Definition sync: behind current definition revision",
                _ => "Definition sync: not yet established"
            };

    public string LocalStateSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => HasLocalOverrides
                ? HasSavedInstanceState
                    ? "Local instance state: saved local working copy with active overrides"
                    : "Local instance state: active overrides not yet saved as local instance state"
                : HasSavedInstanceState
                    ? "Local instance state: saved without active overrides"
                    : "Local instance state: no saved local override baseline",

            ChildPaneSourcePosture.DetachedFromDefinition => HasSavedInstanceState
                ? "Local instance state: detached baseline saved locally"
                : "Local instance state: detached baseline not yet saved locally",

            ChildPaneSourcePosture.CreatedLocalOnly => HasSavedInstanceState
                ? "Local instance state: local-only baseline saved locally"
                : "Local instance state: local-only baseline not yet saved locally",

            _ => HasLocalOverrides
                ? "Local instance state: working copy with local overrides"
                : "Local instance state: no explicit local baseline"
        };

    public string StatusSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => DefinitionSyncPosture == ChildPaneDefinitionSyncPosture.BehindCurrentDefinitionRevision
                ? HasLocalOverrides
                    ? HasSavedInstanceState
                        ? "Definition-backed pane · saved local working copy · out of date"
                        : "Definition-backed pane · local working copy · out of date"
                    : "Definition-backed pane · clean · out of date"
                : HasLocalOverrides
                    ? HasSavedInstanceState
                        ? "Definition-backed pane · saved local working copy"
                        : "Definition-backed pane · local working copy"
                    : "Definition-backed pane · clean",

            ChildPaneSourcePosture.DetachedFromDefinition => HasLocalOverrides
                ? HasSavedInstanceState
                    ? "Detached local pane · saved baseline plus active overrides"
                    : "Detached local pane · active unsaved overrides"
                : HasSavedInstanceState
                    ? "Detached local pane · saved locally"
                    : "Detached local pane · local save pending",

            ChildPaneSourcePosture.CreatedLocalOnly => HasLocalOverrides
                ? HasSavedInstanceState
                    ? "Local-only pane · saved baseline plus active overrides"
                    : "Local-only pane · unsaved working copy"
                : HasSavedInstanceState
                    ? "Local-only pane · saved locally"
                    : "Local-only pane · not yet saved",

            _ => HasLocalOverrides
                ? "Working copy with local overrides"
                : "No working-copy overrides"
        };

    private string[] GetStatusBadgeLabels()
    {
        var labels = new List<string>(4);

        switch (SourcePosture)
        {
            case ChildPaneSourcePosture.FromDefinition:
                labels.Add("Definition-Backed");
                labels.Add(DefinitionSyncPosture == ChildPaneDefinitionSyncPosture.BehindCurrentDefinitionRevision
                    ? "Out of Date"
                    : "In Sync");
                break;

            case ChildPaneSourcePosture.DetachedFromDefinition:
                labels.Add("Detached");
                break;

            case ChildPaneSourcePosture.CreatedLocalOnly:
                labels.Add("Local Only");
                break;
        }

        if (HasSavedInstanceState)
        {
            labels.Add(SourcePosture == ChildPaneSourcePosture.FromDefinition
                ? "Saved Instance"
                : "Saved Local Baseline");
        }

        if (HasLocalOverrides)
        {
            labels.Add("Local Overrides");
        }

        return labels.ToArray();
    }

    private string[] GetActiveOverrideAxisLabels()
    {
        if (ActiveOverrideAxes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var labels = new string[ActiveOverrideAxes.Count];
        for (var i = 0; i < ActiveOverrideAxes.Count; i++)
        {
            labels[i] = GetAxisLabel(ActiveOverrideAxes[i]);
        }

        return labels;
    }

    private static string GetAxisLabel(ChildPaneLocalOverrideAxis axis)
    {
        return axis switch
        {
            ChildPaneLocalOverrideAxis.Title => "Title",
            ChildPaneLocalOverrideAxis.Description => "Description",
            ChildPaneLocalOverrideAxis.Appearance => "Appearance",
            ChildPaneLocalOverrideAxis.CanvasComposition => "Canvas",
            _ => "Unknown"
        };
    }
}

public sealed record ChildPaneDescriptor(
    string Id,
    string Title,
    int Order,
    int ContainerIndex = 0,
    bool IsMinimized = false,
    int SlideIndex = 0,
    double PreferredSizeRatio = 0.25,
    double FloatingX = 0,
    double FloatingY = 0,
    double FloatingWidth = 260,
    double FloatingHeight = 160,
    string? ParentId = null,
    int FloatingZIndex = 0,
    double FixedSizePixels = 0.0,
    double FloatingWidthFull = 0.0,
    double FloatingHeightFull = 0.0,
    string? SurfaceRole = null,
    string? BoundViewRef = null,
    string? BoundResourceTitle = null,
    string? BoundResourceDisplayLabel = null,
    ChildPaneSourcePosture SourcePosture = ChildPaneSourcePosture.Unspecified,
    ChildPaneDefinitionOriginKind DefinitionOriginKind = ChildPaneDefinitionOriginKind.Unknown,
    string? DefinitionId = null,
    string? DefinitionLabel = null,
    ChildPaneDefinitionSyncPosture DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.NotApplicable,
    bool HasLocalModifications = false,
    bool HasSavedInstanceState = false,
    string? BaseTitle = null,
    ChildPaneResourceContext? ResourceContext = null,
    string AppearanceVariant = "default",
    string? BaseAppearanceVariant = null,
    string? Description = null,
    string? BaseDescription = null,
    bool IsInlineRenaming = false,
    bool IsAuthorMode = false,
    bool ShowPaneDefinitionPanel = false,
    ChildPaneCanvasViewportState? CanvasViewport = null,
    IReadOnlyDictionary<string, ChildPaneCanvasElementPreviewPlacement>? CanvasElementPreviewPlacements = null,
    IReadOnlyList<ChildPaneCanvasElementInstance>? LocalCanvasElements = null,
    string? SelectedCanvasElementInstanceId = null)
{
    public ChildPaneResourceContext? EffectiveResourceContext =>
        ResourceContext ??
        CreateFallbackResourceContext();

    public PaneSurfaceBinding? SurfaceBinding =>
        EffectiveResourceContext?.SurfaceBinding ??
        PaneSurfaceBinding.CreateResourceSurface(
            SurfaceRole,
            BoundViewRef);

    public bool HasDirectBoundResourceContextPresentation =>
        EffectiveResourceContext is not null;

    public string PaneBoundResourceReadoutTitle
    {
        get
        {
            var resourceContext = EffectiveResourceContext;
            if (resourceContext is null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(resourceContext.Title))
            {
                return resourceContext.Title!;
            }

            if (!string.IsNullOrWhiteSpace(resourceContext.DisplayLabel))
            {
                return resourceContext.DisplayLabel!;
            }

            return "Bound Resource";
        }
    }

    public string PaneBoundResourceReadoutSubtitle =>
        EffectiveResourceContext is { } resourceContext
            ? BuildBoundResourceReadoutSubtitle(resourceContext)
            : string.Empty;

    public string PaneDefaultEmptyBodyText =>
        HasDirectBoundResourceContextPresentation
            ? "This pane is currently bound to a resource context."
            : "(empty child pane)";

    public bool ShouldShowDefaultEmptyBodyText =>
        !IsAuthorMode &&
        !HasDefinitionIdentity &&
        !HasDirectBoundResourceContextPresentation;

    public string SurfaceBindingKey => SurfaceBinding?.BindingKey ?? string.Empty;

    public bool IsDefinitionBacked => SourcePosture == ChildPaneSourcePosture.FromDefinition;

    public bool IsDetachedFromDefinition => SourcePosture == ChildPaneSourcePosture.DetachedFromDefinition;

    public bool IsLocalOnly => SourcePosture == ChildPaneSourcePosture.CreatedLocalOnly;

    public bool CanSaveInstanceOnly =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => HasAnyLocalOverride || HasSavedInstanceState,
            ChildPaneSourcePosture.DetachedFromDefinition => true,
            ChildPaneSourcePosture.CreatedLocalOnly => true,
            _ => false
        };

    public bool CanSaveAsNewDefinition => true;

    public bool CanDetachFromDefinition => IsDefinitionBacked;

    public bool HasDefinitionIdentity =>
        !string.IsNullOrWhiteSpace(DefinitionId) ||
        !string.IsNullOrWhiteSpace(DefinitionLabel);

    public bool CanRevertToDefinition =>
        HasDefinitionIdentity &&
        (IsDefinitionBacked || IsDetachedFromDefinition);

    public bool HasPaneSourcePresentation =>
        SourcePosture != ChildPaneSourcePosture.Unspecified;

    public bool HasPaneSourceTextLabel =>
        !string.IsNullOrWhiteSpace(PaneSourceLabel);

    public string EffectiveDefinitionLabel =>
        !string.IsNullOrWhiteSpace(DefinitionLabel)
            ? DefinitionLabel!
            : !string.IsNullOrWhiteSpace(DefinitionId)
                ? DefinitionId!
                : "Local Pane";

    public string TitleBaseline =>
        !string.IsNullOrWhiteSpace(BaseTitle)
            ? BaseTitle!
            : HasDefinitionIdentity
                ? EffectiveDefinitionLabel
                : IsLocalOnly
                    ? "Untitled Pane"
                    : Title;

    public string EffectiveDescription =>
        string.IsNullOrWhiteSpace(Description)
            ? string.Empty
            : Description.Trim();

    public string DescriptionBaseline =>
        string.IsNullOrWhiteSpace(BaseDescription)
            ? string.Empty
            : BaseDescription.Trim();

    public string EffectiveAppearanceVariant =>
        NormalizeAppearanceVariant(AppearanceVariant);

    public ChildPaneCanvasViewportState EffectiveCanvasViewport =>
        (CanvasViewport ?? ChildPaneCanvasViewportState.Default).Normalized;

    public IReadOnlyList<ChildPaneCanvasElementInstance> EffectiveLocalCanvasElements =>
        LocalCanvasElements ?? Array.Empty<ChildPaneCanvasElementInstance>();

    public ChildPaneCanvasAuthoringState CanvasAuthoringState =>
        new(
            EffectiveCanvasViewport,
            CanvasElementPreviewPlacements ?? new Dictionary<string, ChildPaneCanvasElementPreviewPlacement>(StringComparer.Ordinal),
            SelectedCanvasElementInstanceId);

    public int LocalCanvasElementCount =>
        EffectiveLocalCanvasElements.Count;

    public bool HasLocalCanvasElements =>
        LocalCanvasElementCount > 0;

    public bool HasLocalCanvasChanges =>
        HasLocalCanvasElements || CanvasAuthoringState.HasAuthoredElementOverrides;

    public bool HasRealizedPaneContentSurface =>
        HasDefinitionIdentity || IsAuthorMode;

    public string PaneRealizedSurfaceTitle =>
        HasDefinitionIdentity
            ? "Realized Pane Definition"
            : "Authoring Canvas";

    public string PaneAuthorModeBadgeLabel =>
        IsAuthorMode ? "Author Mode" : "View Mode";

    public string PaneAuthorModeActionLabel =>
        IsAuthorMode ? "Exit Author Mode" : "Enter Author Mode";

    public string PaneDefinitionVisibilityToggleLabel =>
        ShowPaneDefinitionPanel ? "Hide Pane Definitions" : "Show Pane Definitions";

    public string PaneCanvasViewportSummary =>
        EffectiveCanvasViewport.Summary;

    public bool HasSelectedCanvasElement =>
        !string.IsNullOrWhiteSpace(SelectedCanvasElementInstanceId);

    public string PaneCanvasAuthoringStateSummary =>
        HasLocalCanvasChanges
            ? $"{CanvasAuthoringState.Summary} · local elements={LocalCanvasElementCount}"
            : CanvasAuthoringState.Summary;

    public string PaneCanvasSelectionSummary =>
        HasSelectedCanvasElement
            ? $"Selected element: {SelectedCanvasElementInstanceId}"
            : HasLocalCanvasElements
                ? "No canvas element selected."
                : "No canvas element selected. Use quick add or the author-mode body context menu to place the first element.";

    public string PaneCanvasEmptyStateSummary =>
        HasLocalCanvasElements
            ? $"Authored canvas contains {LocalCanvasElementCount} local element(s)."
            : "Blank authored canvas. Add the first raw element directly, then move and resize it inside the pane body.";

    public string PaneCanvasInteractionHint =>
        IsAuthorMode
            ? "Author mode suppresses live pane-content interaction. Use the quick-add strip or author-mode body context menu to place elements, click authored elements to select them, drag a selected preview to move it, and use the selected element's bottom-right grip to resize it. Use Ctrl+Wheel to zoom the canvas viewport, Wheel to pan vertically, and Shift+Wheel to pan horizontally."
            : "View mode keeps live pane content active. Enter author mode to begin pane-body composition and canvas navigation.";

    public string PaneAuthorModeSurfaceBackgroundBrush =>
        IsAuthorMode ? "#1A2D3D" : "#14212D";

    public string PaneAuthorModeSurfaceBorderBrush =>
        IsAuthorMode ? "#68B7FF" : "#355066";

    public string PaneBodyBackgroundBrush =>
        IsAuthorMode ? "#0D1620" : "#101722";

    public string AppearanceVariantBaseline =>
        NormalizeAppearanceVariant(BaseAppearanceVariant);

    public ChildPaneAuthoredValues CurrentAuthoredValues =>
        new(
            Title,
            EffectiveDescription,
            EffectiveAppearanceVariant);

    public ChildPaneAuthoredValues BaselineAuthoredValues =>
        new(
            TitleBaseline,
            DescriptionBaseline,
            AppearanceVariantBaseline);

    public ChildPaneAuthoredState AuthoredState =>
        new(
            CurrentAuthoredValues,
            BaselineAuthoredValues);

    public string PaneCurrentAuthoredSummary =>
        AuthoredState.CurrentSummary;

    public string PaneBaselineAuthoredSummary =>
        AuthoredState.BaselineSummary;

    public string PaneAuthoredValueStatusSummary =>
        AuthoredState.OverrideAxisStatusSummary;

    public string PaneTitleCurrentValueSummary =>
        AuthoredState.TitleCurrentValueSummary;

    public string PaneTitleBaselineValueSummary =>
        AuthoredState.TitleBaselineValueSummary;

    public string PaneTitleSummary =>
        AuthoredState.TitleSummary;

    public string PaneAuthoredTitleEditorValue =>
        CurrentAuthoredValues.Title;

    public string PaneDescriptionCurrentValueSummary =>
        AuthoredState.DescriptionCurrentValueSummary;

    public string PaneDescriptionBaselineValueSummary =>
        AuthoredState.DescriptionBaselineValueSummary;

    public bool HasLocalTitleOverride =>
        AuthoredState.HasTitleOverride;

    public bool CanResetLocalTitleOverride =>
        HasLocalTitleOverride;

    public bool HasLocalDescriptionOverride =>
        AuthoredState.HasDescriptionOverride;

    public bool CanResetLocalDescriptionOverride =>
        HasLocalDescriptionOverride;

    public string PaneDescriptionSummary =>
        AuthoredState.DescriptionSummary;

    public string PaneAuthoredDescriptionEditorValue =>
        CurrentAuthoredValues.Description;

    public bool HasLocalAppearanceOverride =>
        AuthoredState.HasAppearanceOverride;

    public bool CanResetLocalAppearanceOverride =>
        HasLocalAppearanceOverride;

    public bool HasAnyLocalOverride =>
        LocalOverrideAxes.Count > 0;

    public int LocalOverrideAxisCount =>
        LocalOverrideAxes.Count;

    public IReadOnlyList<ChildPaneLocalOverrideAxis> LocalOverrideAxes =>
        BuildLocalOverrideAxes();

    public ChildPaneWorkingCopyState WorkingCopyState =>
        new(
            SourcePosture,
            DefinitionSyncPosture,
            HasSavedInstanceState,
            LocalOverrideAxes);

    public string PaneWorkingCopyStatusSummary =>
        WorkingCopyState.StatusSummary;

    public string PaneWorkingCopySourceSummary =>
        WorkingCopyState.SourcePostureSummary;

    public IReadOnlyList<string> PaneWorkingCopyStatusBadgeLabels =>
        WorkingCopyState.StatusBadgeLabels;

    public bool HasPaneWorkingCopyStatusBadges =>
        WorkingCopyState.HasStatusBadges;

    public string PaneDefinitionChooserSourceSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => $"Current reusable definition: {EffectiveDefinitionLabel}",
            ChildPaneSourcePosture.DetachedFromDefinition => HasDefinitionIdentity
                ? $"Detached from reusable definition: {EffectiveDefinitionLabel}"
                : "Detached local pane with no active reusable definition link.",
            ChildPaneSourcePosture.CreatedLocalOnly => "This pane is local-only and not currently backed by a reusable definition.",
            _ => "Choose a reusable pane definition or keep working with pane-local authored state."
        };

    public string PaneDefinitionChooserActionSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => "Loading a different definition replaces this pane instance baseline with the selected reusable pane.",
            ChildPaneSourcePosture.DetachedFromDefinition => "Loading a definition reattaches this pane to reusable pane truth and replaces its detached local baseline.",
            ChildPaneSourcePosture.CreatedLocalOnly => "Loading a definition converts this local pane into a definition-backed instance. Create New resets to a fresh local pane baseline.",
            _ => "Load an existing pane definition or reset this child pane into a new local pane."
        };

    public string PaneWorkingCopyDefinitionSyncSummary =>
        WorkingCopyState.DefinitionSyncSummary;

    public bool HasPaneWorkingCopyDefinitionSyncSummary =>
        WorkingCopyState.HasDefinitionSyncSummary;

    public string PaneWorkingCopyLocalStateSummary =>
        WorkingCopyState.LocalStateSummary;

    public IReadOnlyList<string> PaneWorkingCopyActiveOverrideAxisLabels =>
        WorkingCopyState.ActiveOverrideAxisLabels;

    public bool HasPaneWorkingCopyOverrideAxes =>
        WorkingCopyState.LocalOverrideAxisCount > 0;

    public string PaneLocalOverrideSummary =>
        WorkingCopyState.OverrideSummary;

    public string PaneLifecycleActionSummary =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => HasAnyLocalOverride
                ? "Save Instance Only keeps title, description, and appearance overrides on this pane only. Save As New Definition publishes the current pane state as a reusable fork."
                : "This pane currently matches its definition. Save As New Definition publishes a reusable fork, while Detach turns the current pane into local authored state.",

            ChildPaneSourcePosture.DetachedFromDefinition => HasSavedInstanceState
                ? "This pane is detached and saved locally. Save Instance Only refreshes its detached local baseline; Revert to Definition reattaches its saved source."
                : "This pane is detached from its source definition. Save Instance Only establishes a detached local baseline; Revert to Definition reattaches the saved source.",

            ChildPaneSourcePosture.CreatedLocalOnly => HasSavedInstanceState
                ? "This pane is local-only and saved in the workspace. Save As New Definition promotes it for reuse."
                : "This pane is local-only. Save Instance Only keeps it in this workspace; Save As New Definition promotes it for reuse.",

            _ => "Pane authoring actions determine whether this state stays instance-local or becomes reusable definition truth."
        };

    public string PaneAppearanceVariantLabel =>
        GetAppearanceVariantLabel(EffectiveAppearanceVariant);

    public string PaneAppearanceBaselineLabel =>
        GetAppearanceVariantLabel(AppearanceVariantBaseline);

    public string PaneAppearanceSummary =>
        AuthoredState.AppearanceSummary;

    public string PaneAppearanceCurrentValueSummary =>
        AuthoredState.AppearanceCurrentValueSummary;

    public string PaneAppearanceBaselineValueSummary =>
        AuthoredState.AppearanceBaselineValueSummary;

    public string PaneHeaderBackgroundBrush =>
        EffectiveAppearanceVariant switch
        {
            "cool" => "#1F3146",
            "warm" => "#3A281C",
            _ => "#213244"
        };

    public string PaneHeaderBorderBrush =>
        EffectiveAppearanceVariant switch
        {
            "cool" => "#3F6E96",
            "warm" => "#8E623F",
            _ => "#314B62"
        };

    public string PaneEmptyHeaderBackgroundBrush =>
        EffectiveAppearanceVariant switch
        {
            "cool" => "#162432",
            "warm" => "#2A1D14",
            _ => "#1A2633"
        };

    public string PaneEmptyHeaderBorderBrush =>
        EffectiveAppearanceVariant switch
        {
            "cool" => "#315A78",
            "warm" => "#6F5136",
            _ => "#24384A"
        };

    public string PaneSourcePrimaryBadge =>
        DefinitionOriginKind switch
        {
            ChildPaneDefinitionOriginKind.Seeded => "Seeded",
            ChildPaneDefinitionOriginKind.UserAuthored => "User",
            ChildPaneDefinitionOriginKind.PluginContributed => "Plugin",
            ChildPaneDefinitionOriginKind.LocalOnly => "Local",
            _ => IsLocalOnly ? "Local" : string.Empty
        };

    public bool HasPaneSourcePrimaryBadge =>
        !string.IsNullOrWhiteSpace(PaneSourcePrimaryBadge);

    public string PaneSourceStatusBadge
    {
        get
        {
            if (HasLocalModifications)
            {
                return "Modified";
            }

            if (DefinitionSyncPosture == ChildPaneDefinitionSyncPosture.BehindCurrentDefinitionRevision)
            {
                return "Out of Date";
            }

            if (IsDetachedFromDefinition)
            {
                return "Detached";
            }

            if (IsLocalOnly)
            {
                return HasSavedInstanceState ? "Saved" : "Unsaved";
            }

            return string.Empty;
        }
    }

    public bool HasPaneSourceStatusBadge =>
        !string.IsNullOrWhiteSpace(PaneSourceStatusBadge);

    public string PaneSourceLabel =>
        SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => HasLocalModifications
                ? $"Based on: {EffectiveDefinitionLabel} · local changes"
                : $"Based on: {EffectiveDefinitionLabel}",

            ChildPaneSourcePosture.DetachedFromDefinition => HasDefinitionIdentity
                ? HasLocalModifications
                    ? $"Detached from: {EffectiveDefinitionLabel} · local changes"
                    : HasSavedInstanceState
                        ? $"Detached from: {EffectiveDefinitionLabel} · saved locally"
                        : $"Detached from: {EffectiveDefinitionLabel} · local save pending"
                : HasLocalModifications
                    ? "Detached local pane · local changes"
                    : HasSavedInstanceState
                        ? "Detached local pane · saved locally"
                        : "Detached local pane",

            ChildPaneSourcePosture.CreatedLocalOnly => string.Empty,
            _ => string.Empty
        };

    public bool ComputeHasLocalModifications(
        string? title = null,
        string? appearanceVariant = null,
        string? description = null)
    {
        var resolvedValues = new ChildPaneAuthoredValues(
            title is null
                ? CurrentAuthoredValues.Title
                : string.IsNullOrWhiteSpace(title)
                    ? TitleBaseline
                    : title.Trim(),
            description is null
                ? CurrentAuthoredValues.Description
                : string.IsNullOrWhiteSpace(description)
                    ? string.Empty
                    : description.Trim(),
            NormalizeAppearanceVariant(appearanceVariant ?? AppearanceVariant));

        return ComputeHasLocalModifications(resolvedValues);
    }

    public bool ComputeHasLocalModifications(ChildPaneAuthoredValues currentValues)
    {
        return !string.Equals(currentValues.Title, BaselineAuthoredValues.Title, StringComparison.Ordinal) ||
               !string.Equals(currentValues.Description, BaselineAuthoredValues.Description, StringComparison.Ordinal) ||
               !string.Equals(currentValues.AppearanceVariant, BaselineAuthoredValues.AppearanceVariant, StringComparison.Ordinal) ||
               HasLocalCanvasChanges;
    }

    public ChildPaneDescriptor WithRequestedLocalTitle(string? requestedTitle)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(requestedTitle)
            ? TitleBaseline
            : requestedTitle.Trim();

        return WithCurrentAuthoredValues(CurrentAuthoredValues with
        {
            Title = resolvedTitle
        });
    }

    public ChildPaneDescriptor WithRequestedLocalDescription(string? requestedDescription)
    {
        var resolvedDescription = string.IsNullOrWhiteSpace(requestedDescription)
            ? DescriptionBaseline
            : requestedDescription.Trim();

        return WithCurrentAuthoredValues(CurrentAuthoredValues with
        {
            Description = resolvedDescription
        });
    }

    public ChildPaneDescriptor WithRequestedAppearanceVariant(string? requestedVariant)
    {
        return WithCurrentAuthoredValues(CurrentAuthoredValues with
        {
            AppearanceVariant = NormalizeAppearanceVariant(requestedVariant)
        });
    }

    public ChildPaneDescriptor WithCurrentAuthoredValues(ChildPaneAuthoredValues currentValues)
    {
        return (this with
        {
            Title = currentValues.Title,
            Description = currentValues.Description,
            AppearanceVariant = NormalizeAppearanceVariant(currentValues.AppearanceVariant)
        }).WithRecomputedLocalModifications();
    }

    public ChildPaneDescriptor WithAuthorMode(bool isAuthorMode)
    {
        return this with
        {
            IsAuthorMode = isAuthorMode
        };
    }

    public ChildPaneDescriptor ToggleAuthorMode()
    {
        return this with
        {
            IsAuthorMode = !IsAuthorMode
        };
    }

    public ChildPaneDescriptor WithPaneDefinitionPanelVisibility(bool isVisible)
    {
        return this with
        {
            ShowPaneDefinitionPanel = isVisible
        };
    }

    public ChildPaneDescriptor TogglePaneDefinitionPanelVisibility()
    {
        return this with
        {
            ShowPaneDefinitionPanel = !ShowPaneDefinitionPanel
        };
    }

    public ChildPaneDescriptor WithCanvasViewport(ChildPaneCanvasViewportState viewport)
    {
        return this with
        {
            CanvasViewport = viewport.Normalized
        };
    }

    public ChildPaneDescriptor WithCanvasViewportPanned(double deltaX, double deltaY)
    {
        return WithCanvasViewport(EffectiveCanvasViewport.WithPan(deltaX, deltaY));
    }

    public ChildPaneDescriptor WithCanvasViewportZoomDelta(double deltaZoom)
    {
        return WithCanvasViewport(EffectiveCanvasViewport.WithZoomDelta(deltaZoom));
    }

    public ChildPaneDescriptor WithCanvasViewportReset()
    {
        return WithCanvasViewport(ChildPaneCanvasViewportState.Default);
    }

    public ChildPaneCanvasElementPreviewPlacement? TryGetCanvasElementPreviewPlacement(string? elementInstanceId)
    {
        return CanvasAuthoringState.TryGetElementPlacement(elementInstanceId);
    }

    public ChildPaneDescriptor WithCanvasElementPreviewPlacement(string elementInstanceId, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(elementInstanceId))
        {
            return this;
        }

        var normalizedId = elementInstanceId.Trim();
        var existingPlacement = TryGetCanvasElementPreviewPlacement(normalizedId);
        var updatedPlacement = existingPlacement is null
            ? new ChildPaneCanvasElementPreviewPlacement(Math.Max(0, x), Math.Max(0, y))
            : existingPlacement.WithPosition(x, y);

        return WithCanvasElementPreviewPlacementState(normalizedId, updatedPlacement);
    }

    public ChildPaneDescriptor WithCanvasElementPreviewSize(
        string elementInstanceId,
        double width,
        double height,
        double fallbackX,
        double fallbackY)
    {
        if (string.IsNullOrWhiteSpace(elementInstanceId))
        {
            return this;
        }

        var normalizedId = elementInstanceId.Trim();
        var existingPlacement = TryGetCanvasElementPreviewPlacement(normalizedId);
        var updatedPlacement = (existingPlacement ?? new ChildPaneCanvasElementPreviewPlacement(
                Math.Max(0, fallbackX),
                Math.Max(0, fallbackY)))
            .WithSize(width, height);

        return WithCanvasElementPreviewPlacementState(normalizedId, updatedPlacement);
    }

    public ChildPaneDescriptor WithCanvasElementPreviewPlacementState(
        string elementInstanceId,
        ChildPaneCanvasElementPreviewPlacement previewPlacement)
    {
        if (string.IsNullOrWhiteSpace(elementInstanceId))
        {
            return this;
        }

        var normalizedId = elementInstanceId.Trim();
        var normalizedPlacement = previewPlacement with
        {
            X = Math.Max(0, previewPlacement.X),
            Y = Math.Max(0, previewPlacement.Y),
            Width = previewPlacement.Width is double width
                ? Math.Max(120, width)
                : null,
            Height = previewPlacement.Height is double height
                ? Math.Max(64, height)
                : null
        };
        var placements = CanvasElementPreviewPlacements is null
            ? new Dictionary<string, ChildPaneCanvasElementPreviewPlacement>(StringComparer.Ordinal)
            : new Dictionary<string, ChildPaneCanvasElementPreviewPlacement>(CanvasElementPreviewPlacements, StringComparer.Ordinal);

        if (placements.TryGetValue(normalizedId, out var existingPlacement) &&
            Equals(existingPlacement, normalizedPlacement))
        {
            return this;
        }

        placements[normalizedId] = normalizedPlacement;

        return this with
        {
            CanvasElementPreviewPlacements = placements
        };
    }

    public ChildPaneDescriptor WithSelectedCanvasElement(string? elementInstanceId)
    {
        return this with
        {
            SelectedCanvasElementInstanceId = string.IsNullOrWhiteSpace(elementInstanceId)
                ? null
                : elementInstanceId.Trim()
        };
    }

    public ChildPaneDescriptor ClearSelectedCanvasElement()
    {
        return this with
        {
            SelectedCanvasElementInstanceId = null
        };
    }

    public ChildPaneDescriptor WithAddedLocalCanvasElement(ChildPaneCanvasElementInstance element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var nextElements = LocalCanvasElements is null
            ? new List<ChildPaneCanvasElementInstance>()
            : new List<ChildPaneCanvasElementInstance>(LocalCanvasElements);
        nextElements.Add(element);

        return (this with
        {
            IsAuthorMode = true,
            LocalCanvasElements = nextElements,
            SelectedCanvasElementInstanceId = element.InstanceId
        }).WithRecomputedLocalModifications();
    }

    public ChildPaneDescriptor WithBaselineAuthoredValues(ChildPaneAuthoredValues baselineValues, bool hasSavedInstanceState)
    {
        return (this with
        {
            BaseTitle = baselineValues.Title,
            BaseDescription = baselineValues.Description,
            BaseAppearanceVariant = NormalizeAppearanceVariant(baselineValues.AppearanceVariant),
            HasSavedInstanceState = hasSavedInstanceState
        }).WithRecomputedLocalModifications();
    }

    public ChildPaneDescriptor WithRecomputedLocalModifications()
    {
        return this with
        {
            HasLocalModifications = ComputeHasLocalModifications(CurrentAuthoredValues)
        };
    }

    public ChildPaneDescriptor WithCurrentLocalBaseline(bool hasSavedInstanceState)
    {
        return WithBaselineAuthoredValues(CurrentAuthoredValues, hasSavedInstanceState);
    }

    public ChildPaneDescriptor WithSavedInstanceOnlyState()
    {
        return SourcePosture switch
        {
            ChildPaneSourcePosture.FromDefinition => (this with
            {
                HasSavedInstanceState = true
            }).WithRecomputedLocalModifications(),

            ChildPaneSourcePosture.DetachedFromDefinition or ChildPaneSourcePosture.CreatedLocalOnly => WithCurrentLocalBaseline(true),

            _ => (this with
            {
                HasSavedInstanceState = true
            }).WithRecomputedLocalModifications()
        };
    }

    public ChildPaneDescriptor WithDetachedLocalBaseline()
    {
        return (this with
        {
            SourcePosture = ChildPaneSourcePosture.DetachedFromDefinition,
            DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.NotApplicable,
            HasSavedInstanceState = true
        }).WithBaselineAuthoredValues(CurrentAuthoredValues, true);
    }

    public ChildPaneDescriptor WithLoadedDefinition(PaneDefinitionDescriptor definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var authoredValues = CreateDefinitionAuthoredValues(definition);

        return ((this with
        {
            SourcePosture = ChildPaneSourcePosture.FromDefinition,
            DefinitionOriginKind = definition.IsSeeded
                ? ChildPaneDefinitionOriginKind.Seeded
                : ChildPaneDefinitionOriginKind.UserAuthored,
            DefinitionId = definition.PaneDefinitionId,
            DefinitionLabel = definition.DisplayLabel,
            DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision,
            HasSavedInstanceState = false,
            SurfaceRole = null,
            BoundViewRef = null,
            BoundResourceTitle = null,
            BoundResourceDisplayLabel = null,
            ResourceContext = null,
            CanvasElementPreviewPlacements = null,
            LocalCanvasElements = null,
            SelectedCanvasElementInstanceId = null
        }).WithBaselineAuthoredValues(authoredValues, false))
            .WithCurrentAuthoredValues(authoredValues);
    }

    public ChildPaneDescriptor WithResetToLocalNew()
    {
        var authoredValues = new ChildPaneAuthoredValues(
            "Untitled Pane",
            string.Empty,
            "default");

        return ((this with
        {
            SourcePosture = ChildPaneSourcePosture.CreatedLocalOnly,
            DefinitionOriginKind = ChildPaneDefinitionOriginKind.LocalOnly,
            DefinitionId = null,
            DefinitionLabel = null,
            DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.NotApplicable,
            HasSavedInstanceState = false,
            SurfaceRole = null,
            BoundViewRef = null,
            BoundResourceTitle = null,
            BoundResourceDisplayLabel = null,
            ResourceContext = null,
            CanvasElementPreviewPlacements = null,
            LocalCanvasElements = null,
            SelectedCanvasElementInstanceId = null
        }).WithBaselineAuthoredValues(authoredValues, false))
            .WithCurrentAuthoredValues(authoredValues);
    }

    public ChildPaneDescriptor WithPromotedDefinition(PaneDefinitionDescriptor definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var authoredValues = CreateDefinitionAuthoredValues(
            definition,
            CurrentAuthoredValues.AppearanceVariant);

        return ((this with
        {
            SourcePosture = ChildPaneSourcePosture.FromDefinition,
            DefinitionOriginKind = definition.IsSeeded
                ? ChildPaneDefinitionOriginKind.Seeded
                : ChildPaneDefinitionOriginKind.UserAuthored,
            DefinitionId = definition.PaneDefinitionId,
            DefinitionLabel = definition.DisplayLabel,
            DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision,
            HasSavedInstanceState = false
        }).WithBaselineAuthoredValues(authoredValues, false))
            .WithCurrentAuthoredValues(authoredValues);
    }

    public ChildPaneDescriptor WithRevertedToDefinition(PaneDefinitionDescriptor definition)
    {
        return WithLoadedDefinition(definition);
    }

    public ChildPaneDescriptor WithInlineRenameStarted()
    {
        return this with
        {
            IsInlineRenaming = true
        };
    }

    private ChildPaneLocalOverrideAxis[] BuildLocalOverrideAxes()
    {
        var axes = new List<ChildPaneLocalOverrideAxis>(AuthoredState.ActiveOverrideAxes.Count + 1);
        axes.AddRange(AuthoredState.ActiveOverrideAxes);

        if (HasLocalCanvasChanges)
        {
            axes.Add(ChildPaneLocalOverrideAxis.CanvasComposition);
        }

        return axes.ToArray();
    }

    public ChildPaneDescriptor WithInlineRenameCancelled()
    {
        return this with
        {
            IsInlineRenaming = false
        };
    }

    public ChildPaneDescriptor WithCommittedInlineRename(string? requestedTitle)
    {
        return WithRequestedLocalTitle(requestedTitle) with { IsInlineRenaming = false };
    }

    public static string NormalizeAppearanceVariant(string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant))
        {
            return "default";
        }

        var normalized = variant.Trim().ToLowerInvariant();
        return normalized is "cool" or "warm"
            ? normalized
            : "default";
    }

    private ChildPaneResourceContext? CreateFallbackResourceContext()
    {
        if (string.IsNullOrWhiteSpace(SurfaceRole) &&
            string.IsNullOrWhiteSpace(BoundViewRef) &&
            string.IsNullOrWhiteSpace(BoundResourceTitle) &&
            string.IsNullOrWhiteSpace(BoundResourceDisplayLabel))
        {
            return null;
        }

        return new ChildPaneResourceContext(
            DisplayLabel: BoundResourceDisplayLabel,
            Title: BoundResourceTitle,
            ViewRef: BoundViewRef,
            SurfaceRole: SurfaceRole);
    }

    private static string BuildBoundResourceReadoutSubtitle(ChildPaneResourceContext resourceContext)
    {
        var subtitle = string.Empty;

        if (!string.IsNullOrWhiteSpace(resourceContext.SurfaceRole))
        {
            subtitle = resourceContext.SurfaceRole!;
        }

        if (!string.IsNullOrWhiteSpace(resourceContext.ViewRef))
        {
            subtitle = string.IsNullOrWhiteSpace(subtitle)
                ? resourceContext.ViewRef!
                : $"{subtitle} · {resourceContext.ViewRef}";
        }

        return string.IsNullOrWhiteSpace(subtitle)
            ? "Pane is bound to a resource context."
            : subtitle;
    }

    private static ChildPaneAuthoredValues CreateDefinitionAuthoredValues(
        PaneDefinitionDescriptor definition,
        string appearanceVariant = "default")
    {
        return new ChildPaneAuthoredValues(
            definition.DisplayLabel,
            string.IsNullOrWhiteSpace(definition.Description)
                ? string.Empty
                : definition.Description.Trim(),
            NormalizeAppearanceVariant(appearanceVariant));
    }

    private static string GetAppearanceVariantLabel(string variant)
    {
        return variant switch
        {
            "cool" => "Cool",
            "warm" => "Warm",
            _ => "Default"
        };
    }
}
