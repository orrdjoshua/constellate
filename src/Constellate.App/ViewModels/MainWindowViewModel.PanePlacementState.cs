using System;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing parent-body child placement helpers:
/// lane-target insertion and reindex behavior for child panes within a parent body.
/// Query helpers for lane counts now live in MainWindowViewModel.PaneQueries.cs.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Place a child into a specific parent/lane at a target insert index for the parent's current SlideIndex.
    /// If the child belongs to a different parent or slide, it is moved accordingly. All visible children
    /// (non-minimized) for that parent/slide are then reindexed with sequential Orders lane-major to ensure stability.
    /// </summary>
    public void PlaceChildInParentLane(string childId, string parentId, int laneIndex, int insertIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        var slideIndex = parent.SlideIndex;

        var visible = ChildPanes
            .Where(c => !c.IsMinimized &&
                        string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
                        c.SlideIndex == slideIndex)
            .OrderBy(c => c.Order)
            .ToList();

        var idxGlobal = -1;
        ChildPaneDescriptor? moving = null;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.Id, childId, StringComparison.Ordinal))
            {
                continue;
            }

            idxGlobal = i;
            moving = c;
            break;
        }

        if (idxGlobal < 0 || moving is null)
        {
            return;
        }

        if (!string.Equals(moving.ParentId, parentId, StringComparison.Ordinal) || moving.SlideIndex != slideIndex)
        {
            moving = moving with
            {
                ParentId = parentId,
                SlideIndex = slideIndex
            };
        }

        visible.RemoveAll(c => string.Equals(c.Id, childId, StringComparison.Ordinal));

        var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));
        var lanes = new System.Collections.Generic.List<System.Collections.Generic.List<ChildPaneDescriptor>>(capacity: splitCount);
        for (var li = 0; li < splitCount; li++)
        {
            lanes.Add(visible.Where(c => c.ContainerIndex == li).OrderBy(c => c.Order).ToList());
        }

        var targetLane = Math.Clamp(laneIndex, 0, splitCount - 1);
        var laneList = lanes[targetLane];
        var clampedInsert = Math.Clamp(insertIndex, 0, laneList.Count);

        moving = moving with { ContainerIndex = targetLane };
        laneList.Insert(clampedInsert, moving);

        var reindexed = new System.Collections.Generic.List<ChildPaneDescriptor>();
        for (var li = 0; li < splitCount; li++)
        {
            foreach (var c in lanes[li])
            {
                if (c.ContainerIndex != li)
                {
                    reindexed.Add(c with { ContainerIndex = li });
                }
                else
                {
                    reindexed.Add(c);
                }
            }
        }

        // Build a definitive (childId -> (laneIndex, order)) map from the newly constructed lane lists.
        var orderMap = new System.Collections.Generic.Dictionary<string, (int lane, int order)>(System.StringComparer.Ordinal);
        var nextOrder = 0;
        for (var li = 0; li < lanes.Count; li++)
        {
            foreach (var c in lanes[li])
            {
                orderMap[c.Id] = (li, nextOrder++);
            }
        }

        // Apply across the entire ChildPanes collection so the moving child is updated even if it
        // originated floating or in a different parent/slide. For entries already in the target
        // parent/slide, just rewrite lane/order; for others, assign target ParentId/SlideIndex.
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (orderMap.TryGetValue(c.Id, out var info))
            {
                if (string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.SlideIndex == slideIndex)
                {
                    ChildPanes[i] = c with { ContainerIndex = info.lane, Order = info.order };
                }
                else
                {
                    ChildPanes[i] = c with
                    {
                        ParentId = parentId,
                        SlideIndex = slideIndex,
                        ContainerIndex = info.lane,
                        Order = info.order
                    };
                }
            }
        }

        RaiseChildPaneCollectionsChanged();
    }
}
