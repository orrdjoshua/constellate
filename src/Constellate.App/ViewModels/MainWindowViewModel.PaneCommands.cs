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

    private ChildPaneDescriptor? FindChildPaneById(string childPaneId)
    {
        return ChildPanes.FirstOrDefault(pane =>
            string.Equals(pane.Id, childPaneId, StringComparison.Ordinal));
    }

    public bool TryBeginPaneRename(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            return false;
        }

        ClearInlinePaneRenameState(exceptPaneId: paneId);

        var parentPane = ParentPaneModels.FirstOrDefault(parent =>
            string.Equals(parent.Id, paneId, StringComparison.Ordinal));
        if (parentPane is not null)
        {
            if (parentPane.IsInlineRenaming)
            {
                return false;
            }

            parentPane.IsInlineRenaming = true;
            RaiseParentPaneLayoutChanged();
            return true;
        }

        return TryUpdateChildPane(
            paneId,
            existing => existing.IsInlineRenaming
                ? null
                : existing.WithInlineRenameStarted());
    }

    public bool TryCommitPaneRename(string paneId, string? requestedTitle)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            return false;
        }

        var parentPane = ParentPaneModels.FirstOrDefault(parent =>
            string.Equals(parent.Id, paneId, StringComparison.Ordinal));
        if (parentPane is not null)
        {
            var resolvedTitle = string.IsNullOrWhiteSpace(requestedTitle)
                ? parentPane.Title
                : requestedTitle.Trim();
            var titleChanged = !string.Equals(parentPane.Title, resolvedTitle, StringComparison.Ordinal);
            var renameStateChanged = parentPane.IsInlineRenaming;

            parentPane.IsInlineRenaming = false;
            if (titleChanged)
            {
                parentPane.Title = resolvedTitle;
            }

            if (!titleChanged && !renameStateChanged)
            {
                return false;
            }

            RaiseParentPaneLayoutChanged();
            return true;
        }

        return TryUpdateChildPane(
            paneId,
            existing => existing.WithCommittedInlineRename(requestedTitle));
    }

    public bool TryCancelPaneRename(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            return false;
        }

        var parentPane = ParentPaneModels.FirstOrDefault(parent =>
            string.Equals(parent.Id, paneId, StringComparison.Ordinal));
        if (parentPane is not null)
        {
            if (!parentPane.IsInlineRenaming)
            {
                return false;
            }

            parentPane.IsInlineRenaming = false;
            RaiseParentPaneLayoutChanged();
            return true;
        }

        return TryUpdateChildPane(
            paneId,
            existing => existing.IsInlineRenaming
                ? existing.WithInlineRenameCancelled()
                : null);
    }

    private void ClearInlinePaneRenameState(string? exceptPaneId = null)
    {
        var parentRenameStateChanged = false;
        foreach (var parentPane in ParentPaneModels)
        {
            if (!parentPane.IsInlineRenaming ||
                string.Equals(parentPane.Id, exceptPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            parentPane.IsInlineRenaming = false;
            parentRenameStateChanged = true;
        }

        if (parentRenameStateChanged)
        {
            RaiseParentPaneLayoutChanged();
        }

        var childRenameStateChanged = false;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var childPane = ChildPanes[i];
            if (!childPane.IsInlineRenaming ||
                string.Equals(childPane.Id, exceptPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            ChildPanes[i] = childPane.WithInlineRenameCancelled();
            childRenameStateChanged = true;
        }

        if (childRenameStateChanged)
        {
            RaiseChildPaneCollectionsChanged();
        }
    }

    private bool TryUpdateChildPane(
        string childPaneId,
        Func<ChildPaneDescriptor, ChildPaneDescriptor?> update)
    {
        ArgumentNullException.ThrowIfNull(update);

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

            var updated = update(existing);
            if (updated is null || Equals(existing, updated))
            {
                return false;
            }

            ChildPanes[i] = updated;
            RaiseChildPaneCollectionsChanged();
            return true;
        }

        return false;
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

        return TryUpdateChildPane(childPaneId, existing => existing.WithLoadedDefinition(paneDefinition));
    }

    public bool TryResetChildPaneToLocalNew(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        return TryUpdateChildPane(childPaneId, existing => existing.WithResetToLocalNew());
    }

    public bool TrySaveChildPaneInstanceOnly(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        return TryUpdateChildPane(childPaneId, existing =>
            existing.CanSaveInstanceOnly
                ? existing.WithSavedInstanceOnlyState()
                : null);
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

        return TryUpdateChildPane(childPaneId, existing =>
            existing.WithRequestedLocalTitle(normalizedTitle));
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

        return TryUpdateChildPane(childPaneId, existing =>
            existing.WithRequestedLocalDescription(normalizedDescription));
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

        return TryUpdateChildPane(childPaneId, existing =>
            existing.WithRequestedAppearanceVariant(requestedVariant));
    }

    public bool TryResetChildPaneAppearanceVariant(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var existing = FindChildPaneById(childPaneId);

        return existing is not null &&
               TrySetChildPaneAppearanceVariant(childPaneId, existing.AppearanceVariantBaseline);
    }

    public bool TrySaveChildPaneAsNewDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var existing = FindChildPaneById(childPaneId);
        if (existing is null)
        {
            return false;
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

        return TryUpdateChildPane(childPaneId, current =>
            current.WithPromotedDefinition(newDefinition));
    }

    public bool TryDetachChildPaneFromDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        return TryUpdateChildPane(childPaneId, existing =>
            existing.IsDefinitionBacked
                ? existing.WithDetachedLocalBaseline()
                : null);
    }

    public bool TryRevertChildPaneToDefinition(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return false;
        }

        var existing = FindChildPaneById(childPaneId);
        if (existing is null ||
            string.IsNullOrWhiteSpace(existing.DefinitionId) ||
            !EngineServices.TryGetPaneDefinition(existing.DefinitionId, out var definition))
        {
            return false;
        }

        return TryUpdateChildPane(childPaneId, current =>
            current.WithRevertedToDefinition(definition));
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
        OnPropertyChanged(nameof(PaneCatalogDefinitionDetails));
        OnPropertyChanged(nameof(WorkspaceCatalogDetails));
        OnPropertyChanged(nameof(HasSeededPaneCatalogWorkspacePreview));
        OnPropertyChanged(nameof(SeededPaneCatalogWorkspacePreviewLines));
    }

    private void MaterializeSeededWorkspaceProofTargetIfNeeded()
    {
        var workspace = SeededPaneWorkspaces.FirstOrDefault();
        if (workspace is null || workspace.Members.Count == 0)
        {
            return;
        }

        var parentLayoutChanged = false;

        foreach (var member in workspace.Members.OrderBy(member => member.Ordinal))
        {
            var definition = SeededPaneDefinitions.FirstOrDefault(candidate =>
                string.Equals(candidate.PaneDefinitionId, member.PaneDefinitionId, StringComparison.Ordinal));
            if (definition is null)
            {
                continue;
            }

            var targetParent = EnsureSeededWorkspaceHostParent(member.HostHint);
            if (targetParent is null)
            {
                continue;
            }

            var targetLaneIndex = Math.Clamp(member.LaneIndex, 0, 2);
            if (targetParent.SplitCount < targetLaneIndex + 1)
            {
                targetParent.SplitCount = targetLaneIndex + 1;
                parentLayoutChanged = true;
            }

            if (member.SlideIndex >= 0 && targetParent.SlideIndex != member.SlideIndex)
            {
                targetParent.SlideIndex = member.SlideIndex;
                parentLayoutChanged = true;
            }

            if (targetParent.IsMinimized)
            {
                targetParent.IsMinimized = false;
                parentLayoutChanged = true;
            }

            var existingPane = ChildPanes.FirstOrDefault(pane =>
                string.Equals(pane.DefinitionId, definition.PaneDefinitionId, StringComparison.Ordinal));
            var targetInsertIndex = GetChildrenCountInLaneForCurrentSlide(targetParent.Id, targetLaneIndex);

            if (existingPane is not null)
            {
                var requiresDockMove =
                    existingPane.ParentId is null ||
                    !string.Equals(existingPane.ParentId, targetParent.Id, StringComparison.Ordinal) ||
                    existingPane.ContainerIndex != targetLaneIndex ||
                    existingPane.SlideIndex != targetParent.SlideIndex;

                if (requiresDockMove)
                {
                    DockChildPaneToParent(existingPane.Id, targetParent.Id, targetLaneIndex, targetInsertIndex);
                }

                if (existingPane.IsMinimized)
                {
                    SetChildPaneMinimized(existingPane.Id, false);
                }

                continue;
            }

            var existingChildIds = new HashSet<string>(
                ChildPanes.Select(pane => pane.Id),
                StringComparer.Ordinal);

            CreateChildPaneAt(targetParent.Id, targetLaneIndex, targetInsertIndex);

            var createdPane = ChildPanes.FirstOrDefault(pane =>
                !existingChildIds.Contains(pane.Id));
            if (createdPane is null)
            {
                continue;
            }

            TryApplyPaneDefinitionToChildPane(createdPane.Id, definition.PaneDefinitionId);
        }

        if (parentLayoutChanged)
        {
            RaiseParentPaneLayoutChanged(includeChildRefresh: true);
        }
    }

    private ParentPaneModel? EnsureSeededWorkspaceHostParent(string hostHint)
    {
        var normalizedHost = NormalizeHostId(hostHint);

        var existingParent = ParentPaneModels.FirstOrDefault(parent =>
            string.Equals(NormalizeHostId(parent.HostId), normalizedHost, StringComparison.Ordinal));
        if (existingParent is not null)
        {
            return existingParent;
        }

        var createdParent = CreateParentPaneModel(normalizedHost);
        ParentPaneModels.Add(createdParent);
        return createdParent;
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
