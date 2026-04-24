using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing parent-body mutation helpers:
/// host slide-index mapping, split/slide mutation, and lane ratio persistence.
/// This keeps parent-body state changes separate from drag-shadow and layout-refresh plumbing.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private int GetSlideIndexForHost(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        return normalized switch
        {
            "top" => _topSlideIndex,
            "right" => _rightSlideIndex,
            "bottom" => _bottomSlideIndex,
            _ => _leftSlideIndex
        };
    }

    private void SlideParentPane(string arg)
    {
        var parts = arg.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var host = NormalizeHostId(parts[0]);
        var delta = string.Equals(parts[1], "next", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

        switch (host)
        {
            case "left":
                _leftSlideIndex = Math.Max(0, _leftSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "left", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _leftSlideIndex;
                }
                break;
            case "top":
                _topSlideIndex = Math.Max(0, _topSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "top", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _topSlideIndex;
                }
                break;
            case "right":
                _rightSlideIndex = Math.Max(0, _rightSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "right", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _rightSlideIndex;
                }
                break;
            case "bottom":
                _bottomSlideIndex = Math.Max(0, _bottomSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "bottom", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _bottomSlideIndex;
                }
                break;
        }

        RaiseChildPaneCollectionsChanged();
    }

    public void SetParentSplitCount(string? parentId, int count)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        parent.SplitCount = Math.Max(1, Math.Min(3, count));
        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    public void SetParentSlideIndex(string? parentId, int index)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        parent.SlideIndex = Math.Clamp(index, 0, 2);
        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    /// <summary>
    /// Persist preferred-size occupancies for a given lane by child id.
    /// Ratios are interpreted as occupancy against the lane fixed viewport:
    /// - 0.25 means 25% of the visible fixed viewport
    /// - 1.20 means 120% of the visible fixed viewport, which should scroll
    /// </summary>
    public void UpdateLanePreferredRatios(string parentId, int laneIndex, IEnumerable<(string childId, double ratio)> updates)
    {
        var map = updates?.ToDictionary(
                      x => x.childId,
                      x => Math.Max(0.05, x.ratio),
                      StringComparer.Ordinal) ??
                  new Dictionary<string, double>(StringComparer.Ordinal);

        if (map.Count == 0)
        {
            return;
        }

        foreach (var kv in map.Keys.ToArray())
        {
            map[kv] = Math.Max(0.05, map[kv]);
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.ContainerIndex == laneIndex)
            {
                if (map.TryGetValue(c.Id, out var r))
                {
                    ChildPanes[i] = c with { PreferredSizeRatio = r };
                }
            }
        }

        RaiseChildPaneCollectionsChanged();
    }

    /// <summary>
    /// Persist per-child absolute pixel sizes for a given lane. This is now the authoritative
    /// persistence path used by LanePresenter after splitter release. The stored size represents
    /// the child's extent in the lane's fixed dimension (height for vertical-flow lanes, width for horizontal-flow lanes).
    /// </summary>
    public void UpdateLaneFixedSizesPixels(string parentId, int laneIndex, IEnumerable<(string childId, double pixels)> updates)
    {
        var map = updates?
            .Where(x => !string.IsNullOrWhiteSpace(x.childId))
            .ToDictionary(x => x.childId, x => Math.Max(1.0, x.pixels), StringComparer.Ordinal)
            ?? new Dictionary<string, double>(StringComparer.Ordinal);

        if (map.Count == 0)
        {
            return;
        }

        var before = ChildPanes
            .Where(c => string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.ContainerIndex == laneIndex)
            .Select(c => $"{c.Id}:{c.FixedSizePixels:0.##}")
            .ToArray();
        var requested = map.Select(kvp => $"{kvp.Key}:{kvp.Value:0.##}").ToArray();

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.ParentId, parentId, StringComparison.Ordinal) || c.ContainerIndex != laneIndex)
            {
                continue;
            }

            if (map.TryGetValue(c.Id, out var px))
            {
                if (Math.Abs(c.FixedSizePixels - px) > double.Epsilon)
                {
                    ChildPanes[i] = c with { FixedSizePixels = px };
                }
            }
        }

        var after = ChildPanes
            .Where(c => string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.ContainerIndex == laneIndex)
            .Select(c => $"{c.Id}:{c.FixedSizePixels:0.##}")
            .ToArray();

        Debug.WriteLine(
            $"[LanePersist][Apply] parent={parentId} lane={laneIndex} " +
            $"before=[{string.Join(",", before)}] requested=[{string.Join(",", requested)}] after=[{string.Join(",", after)}]");

        RaiseChildPaneCollectionsChanged();
    }

    private void ApplyChildPaneSplitsForHost(string hostId, int splits)
    {
        var normalizedHost = NormalizeHostId(hostId);
        if (splits <= 0)
        {
            splits = 1;
        }

        splits = Math.Min(splits, 2);

        var targetParentIds = ParentPaneModels
            .Where(parent =>
                string.Equals(
                    NormalizeHostId(parent.HostId),
                    normalizedHost,
                    StringComparison.Ordinal))
            .Select(parent => parent.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (targetParentIds.Count == 0)
        {
            return;
        }

        var ordered = ChildPanes
            .Where(pane =>
                !string.IsNullOrWhiteSpace(pane.ParentId) &&
                targetParentIds.Contains(pane.ParentId))
            .OrderBy(pane => pane.Order)
            .ToArray();

        if (ordered.Length == 0)
        {
            return;
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            var pane = ordered[i];
            var newIndex = i % splits;

            for (var j = 0; j < ChildPanes.Count; j++)
            {
                if (!string.Equals(ChildPanes[j].Id, pane.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                ChildPanes[j] = ChildPanes[j] with { ContainerIndex = newIndex };
                break;
            }
        }

        RaiseChildPaneCollectionsChanged();
    }

    /// <summary>
    /// After a new child has been added to a parent/lane/slide, apply the MVP child sizing rule:
    /// every newly created child begins at 25% occupancy of the lane's fixed viewport,
    /// existing children keep their current occupancies,
    /// and any remaining viewport space is filler until occupancy exceeds 1.0 and the lane scrolls.
    /// </summary>
    private void ApplyDefaultChildSizeForNewLaneMember(string parentId, int laneIndex, string newChildId)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        var slideIndex = parent.SlideIndex;

        var laneChildren = ChildPanes
            .Where(c =>
                !c.IsMinimized &&
                string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
                c.SlideIndex == slideIndex &&
                c.ContainerIndex == laneIndex)
            .ToList();

        if (laneChildren.Count == 0)
        {
            return;
        }

        if (laneChildren.Count == 1)
        {
            var only = laneChildren[0];
            UpdateLanePreferredRatios(
                parentId,
                laneIndex,
                new[] { (only.Id, 0.25) });
            return;
        }

        // Preserve all existing child ratios exactly as-is.
        // Only assign the newly created child to 0.25.
        UpdateLanePreferredRatios(
            parentId,
            laneIndex,
            new[] { (newChildId, 0.25) });
    }
}
