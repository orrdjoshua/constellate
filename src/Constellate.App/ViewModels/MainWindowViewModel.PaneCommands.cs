using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Messaging;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing child/parent pane creation,
/// destruction, and simple in-collection reordering behavior.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Legacy MVP rule retained only as a creation-time fallback.
    /// Real persisted docked child sizing now lives in ChildPaneDescriptor.FixedSizePixels.
    /// </summary>
    private static double GetDefaultChildPanePreferredSizeRatio(ParentPaneModel parent)
    {
        _ = parent;
        return 0.25;
    }

    private static double ResolveChildPanePreferredSizeRatio(ParentPaneModel parent, double? preferredSizeRatio)
    {
        var resolved = preferredSizeRatio ?? GetDefaultChildPanePreferredSizeRatio(parent);
        return Math.Clamp(resolved, 0.05, 0.95);
    }

    private static string NormalizeChildPaneAppearanceVariant(string? requestedVariant)
    {
        return ChildPaneDescriptor.NormalizeAppearanceVariant(requestedVariant);
    }

    private static bool ComputeChildPaneHasLocalModifications(
        ChildPaneDescriptor pane,
        string resolvedTitle,
        string appearanceVariant,
        string description)
    {
        return pane.ComputeHasLocalModifications(
            title: resolvedTitle,
            appearanceVariant: appearanceVariant,
            description: description);
    }

    private void CreateChildPane(string? parentOrHost)
    {
        CreateChildPane(parentOrHost, preferredSizeRatio: null);
    }

    private void CreateChildPane(string? parentOrHost, double? preferredSizeRatio)
    {
        ParentPaneModel? parent = null;
        string normalizedHost;

        // Creation is zero-impact. Existing children are never resized or rebalanced.

        if (!string.IsNullOrWhiteSpace(parentOrHost))
        {
            parent = ParentPaneModels
                .FirstOrDefault(p => string.Equals(p.Id, parentOrHost, StringComparison.Ordinal));
        }

        if (parent is not null)
        {
            normalizedHost = NormalizeHostId(parent.HostId);
        }
        else
        {
            normalizedHost = NormalizeHostId(parentOrHost);

            if (!string.IsNullOrWhiteSpace(parentOrHost))
            {
                parent = ParentPaneModels
                    .FirstOrDefault(p =>
                        !p.IsMinimized &&
                        string.Equals(p.HostId, normalizedHost, StringComparison.OrdinalIgnoreCase));
            }
        }

        parent ??= ParentPaneModels.FirstOrDefault();
        if (parent is null)
        {
            return;
        }

        var parentId = parent.Id;
        var slideIndex = parent.SlideIndex;
        var resolvedPreferredSizeRatio = ResolveChildPanePreferredSizeRatio(parent, preferredSizeRatio);
        var activeLane = parent.LanesVisible.FirstOrDefault(lane => lane.LaneIndex == 0);
        var existingLaneState = activeLane?.Children
            .Select(child => $"{child.Id}(fixed={child.FixedSizePixels:0.##},ratio={child.PreferredSizeRatio:0.###},min={child.IsMinimized})")
            .ToArray()
            ?? Array.Empty<string>();
        var activeLaneChildCountBeforeCreate = activeLane?.Children.Count ?? 0;

        var nextOrder = ChildPanes.Count == 0
            ? 0
            : ChildPanes.Max(pane => pane.Order) + 1;

        var nextOrdinal = GenerateNextChildOrdinal();
        var id = $"child.{nextOrdinal}";
        var title = $"Pane #{nextOrdinal}";

        Debug.WriteLine(
            $"[ChildCreate] parent={parent.Id} orientation={(parent.IsVerticalBodyOrientation ? "vertical" : "horizontal")} " +
            $"slide={slideIndex} splitCount={parent.SplitCount} targetLane=0 existingLaneChildren={activeLaneChildCountBeforeCreate} " +
            $"bodyW={parent.BodyViewportWidth:0.##} bodyH={parent.BodyViewportHeight:0.##} " +
            $"fixed={parent.BodyViewportFixedSize:0.##} adjustable={parent.BodyViewportAdjustableSize:0.##} " +
            $"laneChildrenState=[{string.Join(", ", existingLaneState)}] " +
            $"laneViewportW={(activeLane?.ViewportWidth ?? 0):0.##} laneViewportH={(activeLane?.ViewportHeight ?? 0):0.##} " +
            $"laneFixed={(activeLane?.FixedViewportSize ?? 0):0.##} laneAdjustable={(activeLane?.AdjustableViewportSize ?? 0):0.##} " +
            $"requestedRatio={resolvedPreferredSizeRatio:0.###} newChildId={id} title=\"{title}\"");

        // Authoritative creation size:
        // 25% of the current fixed viewport of the first lane on the active slide.
        var fixedViewport = (activeLane?.FixedViewportSize ?? 0) > 0
            ? activeLane!.FixedViewportSize
            : parent.BodyViewportFixedSize;

        if (fixedViewport <= 0)
        {
            fixedViewport = parent.IsVerticalBodyOrientation
                ? Math.Max(1.0, parent.BodyViewportHeight)
                : Math.Max(1.0, parent.BodyViewportWidth);
        }

        var fixedPixels = Math.Max(1.0, fixedViewport * 0.25);

        ChildPanes.Add(new ChildPaneDescriptor(
            id,
            title,
            nextOrder,
            ContainerIndex: 0,
            IsMinimized: false,
            SlideIndex: slideIndex,
            PreferredSizeRatio: resolvedPreferredSizeRatio,
            ParentId: parentId,
            FixedSizePixels: fixedPixels,
            BaseTitle: title,
            SourcePosture: ChildPaneSourcePosture.CreatedLocalOnly,
            DefinitionOriginKind: ChildPaneDefinitionOriginKind.LocalOnly));

        RaiseChildPaneCollectionsChanged();

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
    }

    public bool TryApplyPaneDefinitionToChildPane(string childPaneId, string paneDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId) || string.IsNullOrWhiteSpace(paneDefinitionId))
        {
            return false;
        }

        var paneDefinition = SeededPaneDefinitions.FirstOrDefault(definition =>
            string.Equals(definition.PaneDefinitionId, paneDefinitionId, StringComparison.Ordinal));
        if (paneDefinition is null)
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            ChildPanes[i] = (existing with
            {
                Title = paneDefinition.DisplayLabel,
                BaseTitle = paneDefinition.DisplayLabel,
                Description = paneDefinition.Description ?? string.Empty,
                BaseDescription = paneDefinition.Description ?? string.Empty,
                AppearanceVariant = "default",
                BaseAppearanceVariant = "default",
                SourcePosture = ChildPaneSourcePosture.FromDefinition,
                DefinitionOriginKind = paneDefinition.IsSeeded
                    ? ChildPaneDefinitionOriginKind.Seeded
                    : ChildPaneDefinitionOriginKind.UserAuthored,
                DefinitionId = paneDefinition.PaneDefinitionId,
                DefinitionLabel = paneDefinition.DisplayLabel,
                DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision,
                HasLocalModifications = false,
                HasSavedInstanceState = false,
                SurfaceRole = null,
                BoundViewRef = null,
                BoundResourceTitle = null,
                BoundResourceDisplayLabel = null,
                ResourceContext = null
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryResetChildPaneToLocalNew(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            ChildPanes[i] = (existing with
            {
                Title = "Untitled Pane",
                BaseTitle = "Untitled Pane",
                Description = string.Empty,
                BaseDescription = string.Empty,
                AppearanceVariant = "default",
                BaseAppearanceVariant = "default",
                SourcePosture = ChildPaneSourcePosture.CreatedLocalOnly,
                DefinitionOriginKind = ChildPaneDefinitionOriginKind.LocalOnly,
                DefinitionId = null,
                DefinitionLabel = null,
                DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.NotApplicable,
                HasLocalModifications = false,
                HasSavedInstanceState = false,
                SurfaceRole = null,
                BoundViewRef = null,
                BoundResourceTitle = null,
                BoundResourceDisplayLabel = null,
                ResourceContext = null
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TrySaveChildPaneInstanceOnly(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal) ||
                !existing.CanSaveInstanceOnly)
            {
                continue;
            }

            ChildPanes[i] = existing.WithSavedInstanceOnlyState();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TrySetChildPaneLocalTitle(string childPaneId, string? requestedTitle)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(requestedTitle)
            ? null
            : requestedTitle.Trim();

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            var fallbackTitle = existing.TitleBaseline;
            var resolvedTitle = string.IsNullOrWhiteSpace(normalizedTitle)
                ? fallbackTitle
                : normalizedTitle!;

            var hasLocalModifications = ComputeChildPaneHasLocalModifications(
                existing,
                resolvedTitle,
                existing.EffectiveAppearanceVariant,
                existing.EffectiveDescription);

            if (string.Equals(existing.Title, resolvedTitle, StringComparison.Ordinal) &&
                existing.HasLocalModifications == hasLocalModifications)
            {
                return false;
            }

            ChildPanes[i] = (existing with
            {
                Title = resolvedTitle
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryResetChildPaneLocalTitle(string childPaneId)
    {
        return TrySetChildPaneLocalTitle(childPaneId, null);
    }

    public bool TrySetChildPaneLocalDescription(string childPaneId, string? requestedDescription)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(requestedDescription)
            ? null
            : requestedDescription.Trim();

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            var resolvedDescription = normalizedDescription ?? existing.DescriptionBaseline;
            var hasLocalModifications = ComputeChildPaneHasLocalModifications(
                existing,
                existing.Title,
                existing.EffectiveAppearanceVariant,
                resolvedDescription);

            if (string.Equals(existing.EffectiveDescription, resolvedDescription, StringComparison.Ordinal) &&
                existing.HasLocalModifications == hasLocalModifications)
            {
                return false;
            }

            ChildPanes[i] = (existing with
            {
                Description = resolvedDescription
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryResetChildPaneLocalDescription(string childPaneId)
    {
        return TrySetChildPaneLocalDescription(childPaneId, null);
    }

    public bool TrySetChildPaneAppearanceVariant(string childPaneId, string? requestedVariant)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var normalizedAppearanceVariant = NormalizeChildPaneAppearanceVariant(requestedVariant);

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            var hasLocalModifications = ComputeChildPaneHasLocalModifications(
                existing,
                existing.Title,
                normalizedAppearanceVariant,
                existing.EffectiveDescription);

            if (string.Equals(existing.EffectiveAppearanceVariant, normalizedAppearanceVariant, StringComparison.Ordinal) &&
                existing.HasLocalModifications == hasLocalModifications)
            {
                return false;
            }

            ChildPanes[i] = (existing with
            {
                AppearanceVariant = normalizedAppearanceVariant
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryResetChildPaneAppearanceVariant(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var existing = ChildPanes.FirstOrDefault(pane =>
            string.Equals(pane.Id, childPaneId, StringComparison.Ordinal));

        return existing is not null &&
               TrySetChildPaneAppearanceVariant(childPaneId, existing.AppearanceVariantBaseline);
    }

    public bool TrySaveChildPaneAsNewDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            var displayLabel = string.IsNullOrWhiteSpace(existing.Title)
                ? "Untitled Pane"
                : existing.Title.Trim();
            var description = string.IsNullOrWhiteSpace(existing.EffectiveDescription)
                ? null
                : existing.EffectiveDescription;
            var currentDefinitions = EngineServices.ListPaneDefinitions();
            var definitionIds = new HashSet<string>(
                currentDefinitions.Select(definition => definition.PaneDefinitionId),
                StringComparer.Ordinal);
            var newDefinitionId = BuildUniqueUserPaneDefinitionId(displayLabel, definitionIds);

            PaneDefinitionDescriptor newDefinition;
            if (!string.IsNullOrWhiteSpace(existing.DefinitionId) &&
                EngineServices.TryGetPaneDefinition(existing.DefinitionId, out var sourceDefinition))
            {
                var tags = sourceDefinition.Tags is null
                    ? new[] { "user-authored" }
                    : sourceDefinition.Tags
                        .Append("user-authored")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                newDefinition = sourceDefinition with
                {
                    PaneDefinitionId = newDefinitionId,
                    DisplayLabel = displayLabel,
                    Description = description,
                    IsSeeded = false,
                    Tags = tags
                };
            }
            else
            {
                newDefinition = new PaneDefinitionDescriptor(
                    newDefinitionId,
                    displayLabel,
                    PaneDefinitionKind.ChildPane,
                    false,
                    new[]
                    {
                        new PaneElementDescriptor(
                            "definition.header",
                            PaneElementKind.DefinitionHeader,
                            displayLabel),
                        new PaneElementDescriptor(
                            "definition.placeholder",
                            PaneElementKind.TextBlock,
                            "Pane definition promoted from a local child pane instance.",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.LiteralText,
                                "Pane definition promoted from a local child pane instance."))
                    },
                    description ?? "User-authored pane definition promoted from a live child pane instance.",
                    new[] { "user-authored" });
            }

            EngineServices.SavePaneDefinition(newDefinition);
            RefreshPaneDefinitionCatalogSnapshot();

            ChildPanes[i] = (existing with
            {
                Title = displayLabel,
                BaseTitle = displayLabel,
                Description = newDefinition.Description ?? string.Empty,
                BaseDescription = newDefinition.Description ?? string.Empty,
                AppearanceVariant = existing.EffectiveAppearanceVariant,
                BaseAppearanceVariant = existing.EffectiveAppearanceVariant,
                SourcePosture = ChildPaneSourcePosture.FromDefinition,
                DefinitionOriginKind = ChildPaneDefinitionOriginKind.UserAuthored,
                DefinitionId = newDefinition.PaneDefinitionId,
                DefinitionLabel = newDefinition.DisplayLabel,
                DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision,
                HasSavedInstanceState = false
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryDetachChildPaneFromDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal) ||
                !existing.IsDefinitionBacked)
            {
                continue;
            }

            ChildPanes[i] = existing.WithDetachedLocalBaseline();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    public bool TryRevertChildPaneToDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, childPaneId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(existing.DefinitionId) ||
                !EngineServices.TryGetPaneDefinition(existing.DefinitionId, out var definition))
            {
                continue;
            }

            ChildPanes[i] = (existing with
            {
                Title = definition.DisplayLabel,
                BaseTitle = definition.DisplayLabel,
                Description = definition.Description ?? string.Empty,
                BaseDescription = definition.Description ?? string.Empty,
                AppearanceVariant = "default",
                BaseAppearanceVariant = "default",
                SourcePosture = ChildPaneSourcePosture.FromDefinition,
                DefinitionOriginKind = definition.IsSeeded
                    ? ChildPaneDefinitionOriginKind.Seeded
                    : ChildPaneDefinitionOriginKind.UserAuthored,
                DefinitionLabel = definition.DisplayLabel,
                DefinitionSyncPosture = ChildPaneDefinitionSyncPosture.InSyncWithBaseRevision,
                HasLocalModifications = false,
                HasSavedInstanceState = false,
                SurfaceRole = null,
                BoundViewRef = null,
                BoundResourceTitle = null,
                BoundResourceDisplayLabel = null,
                ResourceContext = null
            }).WithRecomputedLocalModifications();

            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
    }

    private void RefreshPaneDefinitionCatalogSnapshot()
    {
        var definitions = EngineServices.ListPaneDefinitions();
        SeededPaneDefinitions.Clear();

        foreach (var definition in definitions)
        {
            SeededPaneDefinitions.Add(definition);
        }

        OnPropertyChanged(nameof(HasSeededPaneCatalog));
        OnPropertyChanged(nameof(SeededPaneDefinitionCount));
        OnPropertyChanged(nameof(SeededPaneCatalogPrimaryLabel));
        OnPropertyChanged(nameof(SeededPaneCatalogSummary));
    }

    private static string BuildUniqueUserPaneDefinitionId(string displayLabel, IReadOnlySet<string> existingDefinitionIds)
    {
        var slugBuilder = new StringBuilder();
        var lastWasSeparator = false;

        foreach (var ch in displayLabel)
        {
            var normalized = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(normalized))
            {
                slugBuilder.Append(normalized);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            slugBuilder.Append('.');
            lastWasSeparator = true;
        }

        var slug = slugBuilder
            .ToString()
            .Trim('.');
        var baseId = string.IsNullOrWhiteSpace(slug)
            ? "pane.user.authored"
            : $"pane.user.{slug}";
        var candidate = baseId;
        var suffix = 2;

        while (existingDefinitionIds.Contains(candidate))
        {
            candidate = $"{baseId}.{suffix}";
            suffix++;
        }

        return candidate;
    }

    private void UpsertResourceBoundChildPane(
        string paneId,
        string paneTitle,
        string surfaceRole,
        string? viewRef,
        string? resourceDisplayLabel,
        string? resourceTitle)
    {
        var normalizedViewRef = string.IsNullOrWhiteSpace(viewRef)
            ? string.Empty
            : viewRef.Trim();
        if (string.IsNullOrWhiteSpace(normalizedViewRef))
        {
            return;
        }

        var normalizedPaneTitle = string.IsNullOrWhiteSpace(paneTitle)
            ? "Resource Detail"
            : paneTitle.Trim();
        var normalizedResourceTitle = string.IsNullOrWhiteSpace(resourceTitle)
            ? normalizedPaneTitle
            : resourceTitle.Trim();
        var normalizedResourceDisplayLabel = string.IsNullOrWhiteSpace(resourceDisplayLabel)
            ? normalizedResourceTitle
            : resourceDisplayLabel.Trim();
        var resourceContext = new ChildPaneResourceContext(
            DisplayLabel: normalizedResourceDisplayLabel,
            Title: normalizedResourceTitle,
            ViewRef: normalizedViewRef,
            SurfaceRole: surfaceRole);

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, paneId, StringComparison.Ordinal))
            {
                continue;
            }

            ChildPanes[i] = existing with
            {
                Title = normalizedPaneTitle,
                IsMinimized = false,
                SurfaceRole = surfaceRole,
                BoundViewRef = normalizedViewRef,
                BoundResourceTitle = normalizedResourceTitle,
                BoundResourceDisplayLabel = normalizedResourceDisplayLabel,
                ResourceContext = resourceContext
            };

            RaiseChildPaneCollectionsChanged();
            return;
        }

        var anchorChild = ChildPanes.FirstOrDefault(p =>
            string.Equals(p.Id, "shell.current", StringComparison.Ordinal));
        var parent = anchorChild?.ParentId is not null
            ? ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, anchorChild.ParentId, StringComparison.Ordinal))
            : ParentPaneModels.FirstOrDefault();
        if (parent is null)
        {
            return;
        }

        var containerIndex = anchorChild?.ContainerIndex ?? 0;
        var slideIndex = anchorChild?.SlideIndex ?? parent.SlideIndex;
        var preferredSizeRatio = ResolveChildPanePreferredSizeRatio(parent, 0.25);
        var activeLane = parent.LanesVisible.FirstOrDefault(lane => lane.LaneIndex == containerIndex);
        var fixedViewport = (activeLane?.FixedViewportSize ?? 0) > 0
            ? activeLane!.FixedViewportSize
            : parent.BodyViewportFixedSize;

        if (fixedViewport <= 0)
        {
            fixedViewport = parent.IsVerticalBodyOrientation
                ? Math.Max(1.0, parent.BodyViewportHeight)
                : Math.Max(1.0, parent.BodyViewportWidth);
        }

        var fixedPixels = Math.Max(1.0, fixedViewport * ResolveChildPanePreferredSizeRatio(parent, 0.25));
        var insertOrder = anchorChild is not null
            ? anchorChild.Order + 1
            : ChildPanes.Count == 0
                ? 0
                : ChildPanes.Max(pane => pane.Order) + 1;

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (existing.Order < insertOrder)
            {
                continue;
            }

            ChildPanes[i] = existing with { Order = existing.Order + 1 };
        }

        ChildPanes.Add(new ChildPaneDescriptor(
            paneId,
            normalizedPaneTitle,
            insertOrder,
            ContainerIndex: containerIndex,
            IsMinimized: false,
            SlideIndex: slideIndex,
            PreferredSizeRatio: preferredSizeRatio,
            ParentId: parent.Id,
            FixedSizePixels: fixedPixels,
            SurfaceRole: surfaceRole,
            BoundViewRef: normalizedViewRef,
            BoundResourceTitle: normalizedResourceTitle,
            BoundResourceDisplayLabel: normalizedResourceDisplayLabel,
            BaseTitle: normalizedPaneTitle,
            ResourceContext: resourceContext));

        RaiseChildPaneCollectionsChanged();
        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
        _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
        _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
    }

    private static string BuildResourceBoundChildPaneId(string surfaceRole, string viewRef)
    {
        var seed = $"resource:{surfaceRole}:{viewRef}";
        var builder = new StringBuilder(seed.Length);
        var lastWasSeparator = false;

        foreach (var ch in seed)
        {
            var normalized = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(normalized))
            {
                builder.Append(normalized);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            builder.Append('.');
            lastWasSeparator = true;
        }

        var slug = builder.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(slug)
            ? "pane.resource.detail"
            : $"pane.{slug}";
    }

    private void UpsertResourceBoundDetailChildPane(
        string surfaceRole,
        string? viewRef,
        string? resourceDisplayLabel,
        string? resourceTitle)
    {
        var normalizedViewRef = string.IsNullOrWhiteSpace(viewRef)
            ? string.Empty
            : viewRef.Trim();
        if (string.IsNullOrWhiteSpace(surfaceRole) || string.IsNullOrWhiteSpace(normalizedViewRef))
        {
            return;
        }

        UpsertResourceBoundChildPane(
            BuildResourceBoundChildPaneId(surfaceRole, normalizedViewRef),
            string.IsNullOrWhiteSpace(resourceTitle) ? "Resource Detail" : resourceTitle.Trim(),
            surfaceRole,
            normalizedViewRef,
            resourceDisplayLabel,
            resourceTitle);
    }

    private void ReconcileResourceBoundDetailChildPanes(IEnumerable<string> expectedPaneIds)
    {
        ArgumentNullException.ThrowIfNull(expectedPaneIds);

        var expectedPaneIdSet = new HashSet<string>(expectedPaneIds, StringComparer.Ordinal);
        var removedAny = false;

        for (var i = ChildPanes.Count - 1; i >= 0; i--)
        {
            var pane = ChildPanes[i];
            var surfaceBinding = GetPaneSurfaceBinding(pane);
            if (surfaceBinding is null ||
                !string.Equals(surfaceBinding.ProjectionMode, PaneSurfaceBinding.ProjectionModeDetail, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(surfaceBinding.TargetSurfaceKind, PaneSurfaceBinding.TargetSurfaceKindChildPaneBody, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var autoGeneratedPaneId = BuildResourceBoundChildPaneId(surfaceBinding.SurfaceRole, surfaceBinding.ViewRef);
            if (!string.Equals(pane.Id, autoGeneratedPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            if (expectedPaneIdSet.Contains(pane.Id))
            {
                continue;
            }

            ChildPanes.RemoveAt(i);
            removedAny = true;
        }

        if (!removedAny)
        {
            return;
        }

        RaiseChildPaneCollectionsChanged();
        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
        _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
        _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
    }

    private bool IsAutoGeneratedResourceBoundDetailPane(ChildPaneDescriptor pane)
    {
        var surfaceBinding = GetPaneSurfaceBinding(pane);
        if (surfaceBinding is null ||
            !string.Equals(surfaceBinding.ProjectionMode, PaneSurfaceBinding.ProjectionModeDetail, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(surfaceBinding.TargetSurfaceKind, PaneSurfaceBinding.TargetSurfaceKindChildPaneBody, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var autoGeneratedPaneId = BuildResourceBoundChildPaneId(
            surfaceBinding.SurfaceRole,
            surfaceBinding.ViewRef);

        return string.Equals(pane.Id, autoGeneratedPaneId, StringComparison.Ordinal);
    }


    /// <summary>
    /// Create a new child pane against a specific parent/lane and insert it at the given insert index
    /// for the parent’s current slide. Seeds 25% of the lane’s fixed viewport (absolute pixels), then
    /// reuses PlaceChildInParentLane to position it correctly and reindex neighbors.
    /// </summary>
    public void CreateChildPaneAt(string parentId, int laneIndex, int insertIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        var slideIndex = parent.SlideIndex;
        var nextOrder = ChildPanes.Count == 0 ? 0 : ChildPanes.Max(p => p.Order) + 1;
        var nextOrdinal = GenerateNextChildOrdinal();
        var id = $"child.{nextOrdinal}";
        var title = $"Pane #{nextOrdinal}";

        // Resolve fixed viewport for the target lane from current LanesVisible; fallback to parent body fixed size.
        var laneView = parent.LanesVisible.FirstOrDefault(l => l.LaneIndex == laneIndex);
        var fixedViewport = (laneView?.FixedViewportSize ?? 0) > 0
            ? laneView!.FixedViewportSize
            : parent.BodyViewportFixedSize > 0 ? parent.BodyViewportFixedSize
            : parent.IsVerticalBodyOrientation ? Math.Max(1.0, parent.BodyViewportHeight) : Math.Max(1.0, parent.BodyViewportWidth);

        var fixedPixels = Math.Max(1.0, fixedViewport * 0.25);

        ChildPanes.Add(new ChildPaneDescriptor(
            id,
            title,
            nextOrder,
            ContainerIndex: laneIndex,
            IsMinimized: false,
            SlideIndex: slideIndex,
            PreferredSizeRatio: 0.25,
            ParentId: parentId,
            FixedSizePixels: fixedPixels,
            BaseTitle: title,
            SourcePosture: ChildPaneSourcePosture.CreatedLocalOnly,
            DefinitionOriginKind: ChildPaneDefinitionOriginKind.LocalOnly));

        // Place the brand-new child at the requested insert slot; will also reindex lane.
        PlaceChildInParentLane(id, parentId, Math.Max(0, laneIndex), Math.Max(0, insertIndex));

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
    }

    public void DestroyChildPane(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var idx = -1;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var current = ChildPanes[i];
            if (!string.Equals(current.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            idx = i;
            break;
        }

        if (idx >= 0)
        {
            ChildPanes.RemoveAt(idx);
            RaiseChildPaneCollectionsChanged();
        }
    }

    /// <summary>
    /// Generate a new child id of the form "child.N" where N is one greater than
    /// any existing numeric suffix on child ids. This avoids reusing ids after
    /// deletions or other reordering operations.
    /// </summary>
    private int GenerateNextChildOrdinal()
    {
        var maxOrdinal = 0;

        foreach (var pane in ChildPanes)
        {
            if (!pane.Id.StartsWith("child.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = pane.Id.Substring("child.".Length);
            if (int.TryParse(suffix, out var n) && n > maxOrdinal)
            {
                maxOrdinal = n;
            }
        }

        return maxOrdinal + 1;
    }

    private bool CanMoveChildPane(string id, int delta)
    {
        var ordered = ChildPanesOrdered.ToList();
        var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        var newIndex = index + delta;
        return newIndex >= 0 && newIndex < ordered.Count;
    }

    private void MoveChildPane(string id, int delta)
    {
        var ordered = ChildPanesOrdered.ToList();
        var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var newIndex = index + delta;
        if (newIndex < 0 || newIndex >= ordered.Count)
        {
            return;
        }

        var a = ordered[index];
        var b = ordered[newIndex];

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var current = ChildPanes[i];
            if (string.Equals(current.Id, a.Id, StringComparison.Ordinal))
            {
                ChildPanes[i] = current with { Order = b.Order };
            }
            else if (string.Equals(current.Id, b.Id, StringComparison.Ordinal))
            {
                ChildPanes[i] = current with { Order = a.Order };
            }
        }

        RaiseChildPaneCollectionsChanged();
        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
        _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
        _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
    }

    private ParentPaneModel CreateParentPaneModel(string hostId)
    {
        var normalizedHost = NormalizeHostId(hostId);
        var nextOrdinal = ParentPaneModels.Count(parent =>
            string.Equals(NormalizeHostId(parent.HostId), normalizedHost, StringComparison.Ordinal)) + 1;

        // Simple global ordinal for visibility during QA; note this is
        // present-count based, so duplicates can occur if panes are removed/re-added.
        var globalOrdinal = ParentPaneModels.Count + 1;

        return new ParentPaneModel
        {
            Id = $"parent.{normalizedHost}.{nextOrdinal}",
            Title = $"Parent Pane #{globalOrdinal}",
            HostId = normalizedHost,
            IsMinimized = false,
            SplitCount = 1,
            SlideIndex = 0,
            FloatingWidth = 320,
            FloatingHeight = 240
        };
    }
}
