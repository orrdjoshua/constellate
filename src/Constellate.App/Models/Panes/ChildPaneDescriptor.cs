using System;
using System.Collections.Generic;

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
    Appearance
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
    // Authoritative fixed-dimension size (pixels) for docked layout; when 0, view will migrate from prior ratio.
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
    string? BaseDescription = null)
{
    public PaneSurfaceBinding? SurfaceBinding =>
        ResourceContext?.SurfaceBinding ??
        PaneSurfaceBinding.CreateResourceSurface(
            SurfaceRole,
            BoundViewRef);

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

    public bool HasLocalTitleOverride =>
        !string.Equals(Title, TitleBaseline, StringComparison.Ordinal);

    public bool CanResetLocalTitleOverride =>
        HasLocalTitleOverride;

    public string EffectiveDescription =>
        string.IsNullOrWhiteSpace(Description)
            ? string.Empty
            : Description.Trim();

    public string DescriptionBaseline =>
        string.IsNullOrWhiteSpace(BaseDescription)
            ? string.Empty
            : BaseDescription.Trim();

    public bool HasLocalDescriptionOverride =>
        !string.Equals(EffectiveDescription, DescriptionBaseline, StringComparison.Ordinal);

    public bool CanResetLocalDescriptionOverride =>
        HasLocalDescriptionOverride;

    public string PaneDescriptionSummary =>
        HasLocalDescriptionOverride
            ? $"Current description: {FormatDescriptionLabel(EffectiveDescription)} · baseline: {FormatDescriptionLabel(DescriptionBaseline)}"
            : $"Current description: {FormatDescriptionLabel(EffectiveDescription)}";

    public string EffectiveAppearanceVariant =>
        NormalizeAppearanceVariant(AppearanceVariant);

    public string AppearanceVariantBaseline =>
        NormalizeAppearanceVariant(BaseAppearanceVariant);

    public bool HasLocalAppearanceOverride =>
        !string.Equals(
            EffectiveAppearanceVariant,
            AppearanceVariantBaseline,
            StringComparison.Ordinal);

    public bool CanResetLocalAppearanceOverride =>
        HasLocalAppearanceOverride;

    public bool HasAnyLocalOverride =>
        HasLocalTitleOverride || HasLocalDescriptionOverride || HasLocalAppearanceOverride;

    public int LocalOverrideAxisCount =>
        (HasLocalTitleOverride ? 1 : 0) +
        (HasLocalDescriptionOverride ? 1 : 0) +
        (HasLocalAppearanceOverride ? 1 : 0);

    public ChildPaneWorkingCopyState WorkingCopyState =>
        new(
            SourcePosture,
            DefinitionSyncPosture,
            HasSavedInstanceState,
            GetLocalOverrideAxes());

    public string PaneWorkingCopyStatusSummary =>
        WorkingCopyState.StatusSummary;

    public string PaneWorkingCopySourceSummary =>
        WorkingCopyState.SourcePostureSummary;

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
        HasLocalAppearanceOverride
            ? $"Current appearance: {PaneAppearanceVariantLabel} · baseline: {PaneAppearanceBaselineLabel}"
            : $"Current appearance: {PaneAppearanceVariantLabel}";

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
            ChildPaneSourcePosture.CreatedLocalOnly => HasLocalModifications
                ? HasSavedInstanceState
                    ? "Local pane · saved in this workspace · local changes"
                    : "Local pane · local changes"
                : HasSavedInstanceState
                    ? "Local pane · saved in this workspace"
                    : "Local pane · not saved for reuse",
            _ => string.Empty
        };

    public bool ComputeHasLocalModifications(
        string? title = null,
        string? appearanceVariant = null,
        string? description = null)
    {
        var resolvedTitle = title is null
            ? Title
            : string.IsNullOrWhiteSpace(title)
                ? TitleBaseline
                : title.Trim();
        var resolvedAppearanceVariant = NormalizeAppearanceVariant(appearanceVariant ?? AppearanceVariant);
        var resolvedDescription = description is null
            ? EffectiveDescription
            : string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : description.Trim();

        return !string.Equals(resolvedTitle, TitleBaseline, StringComparison.Ordinal) ||
               !string.Equals(resolvedAppearanceVariant, AppearanceVariantBaseline, StringComparison.Ordinal) ||
               !string.Equals(resolvedDescription, DescriptionBaseline, StringComparison.Ordinal);
    }

    public ChildPaneDescriptor WithRecomputedLocalModifications()
    {
        return this with
        {
            HasLocalModifications = ComputeHasLocalModifications()
        };
    }

    public ChildPaneDescriptor WithCurrentLocalBaseline(bool hasSavedInstanceState)
    {
        return (this with
        {
            BaseTitle = Title,
            BaseAppearanceVariant = EffectiveAppearanceVariant,
            BaseDescription = EffectiveDescription,
            HasSavedInstanceState = hasSavedInstanceState
        }).WithRecomputedLocalModifications();
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
            BaseTitle = Title,
            BaseAppearanceVariant = EffectiveAppearanceVariant,
            BaseDescription = EffectiveDescription,
            SourcePosture = ChildPaneSourcePosture.DetachedFromDefinition,
            DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.NotApplicable,
            HasSavedInstanceState = true
        }).WithRecomputedLocalModifications();
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

    private ChildPaneLocalOverrideAxis[] GetLocalOverrideAxes()
    {
        if (!HasAnyLocalOverride)
        {
            return Array.Empty<ChildPaneLocalOverrideAxis>();
        }

        var axes = new ChildPaneLocalOverrideAxis[LocalOverrideAxisCount];
        var index = 0;

        if (HasLocalTitleOverride)
        {
            axes[index++] = ChildPaneLocalOverrideAxis.Title;
        }

        if (HasLocalDescriptionOverride)
        {
            axes[index++] = ChildPaneLocalOverrideAxis.Description;
        }

        if (HasLocalAppearanceOverride)
        {
            axes[index] = ChildPaneLocalOverrideAxis.Appearance;
        }

        return axes;
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

    private static string FormatDescriptionLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(empty)"
            : value;
    }
}
