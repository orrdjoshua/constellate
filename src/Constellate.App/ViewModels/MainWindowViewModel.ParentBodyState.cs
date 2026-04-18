using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Persist preferred-size ratios for a given lane by child id. Ratios should already be normalized, but we normalize defensively.
    /// </summary>
    public void UpdateLanePreferredRatios(string parentId, int laneIndex, IEnumerable<(string childId, double ratio)> updates)
    {
        var map = updates?.ToDictionary(x => x.childId, x => Math.Max(0.01, x.ratio), StringComparer.Ordinal) ??
                  new Dictionary<string, double>(StringComparer.Ordinal);
        var sum = map.Values.Sum();
        if (sum <= 1e-6)
        {
            return;
        }

        foreach (var kv in map.Keys.ToArray())
        {
            map[kv] = map[kv] / sum;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.ContainerIndex == laneIndex)
            {
                if (map.TryGetValue(c.Id, out var r))
                {
                    ChildPanes[i] = c with { PreferredSizeRatio = Math.Clamp(r, 0.05, 0.95) };
                }
            }
        }

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
}
