using System;
using System.Diagnostics;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing child/parent pane creation,
/// destruction, and simple in-collection reordering behavior.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const string CanonicalRecordDetailPaneId = "record.detail.primary";
    private const string CanonicalRecordDetailSurfaceRole = "resource.markdown.detail";

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
            FixedSizePixels: fixedPixels));

        RaiseChildPaneCollectionsChanged();

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
    }

    private void UpsertCanonicalRecordDetailChildPane(
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

        var normalizedResourceTitle = string.IsNullOrWhiteSpace(resourceTitle)
            ? "Record Detail"
            : resourceTitle.Trim();
        var normalizedResourceDisplayLabel = string.IsNullOrWhiteSpace(resourceDisplayLabel)
            ? normalizedResourceTitle
            : resourceDisplayLabel.Trim();
        var resourceContext = new ChildPaneResourceContext(
            DisplayLabel: normalizedResourceDisplayLabel,
            Title: normalizedResourceTitle,
            ViewRef: normalizedViewRef,
            SurfaceRole: CanonicalRecordDetailSurfaceRole);

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var existing = ChildPanes[i];
            if (!string.Equals(existing.Id, CanonicalRecordDetailPaneId, StringComparison.Ordinal))
            {
                continue;
            }

            ChildPanes[i] = existing with
            {
                Title = "Record Detail",
                IsMinimized = false,
                SurfaceRole = CanonicalRecordDetailSurfaceRole,
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

        var fixedPixels = Math.Max(1.0, fixedViewport * 0.25);
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
            CanonicalRecordDetailPaneId,
            "Record Detail",
            insertOrder,
            ContainerIndex: containerIndex,
            IsMinimized: false,
            SlideIndex: slideIndex,
            PreferredSizeRatio: preferredSizeRatio,
            ParentId: parent.Id,
            FixedSizePixels: fixedPixels,
            SurfaceRole: CanonicalRecordDetailSurfaceRole,
            BoundViewRef: normalizedViewRef,
            BoundResourceTitle: normalizedResourceTitle,
            BoundResourceDisplayLabel: normalizedResourceDisplayLabel,
            ResourceContext: resourceContext));

        RaiseChildPaneCollectionsChanged();
        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
        _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
        _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
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
            FixedSizePixels: fixedPixels));

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
