using System.Collections.Generic;

namespace Constellate.App;

/// <summary>
/// Transitional App-local shell layout aggregate used to snapshot current host/minimized/slide
/// state together with parent/child collections. It is useful for current layout save/restore
/// flows, but it should not be mistaken for final durable workspace truth; later convergence
/// should further separate live shell realization artifacts from reusable workspace definitions.
/// </summary>
public sealed record ShellLayoutDescriptor(
    string HostId,
    bool IsMinimized,
    string? SavedHostId = null,
    bool SavedIsMinimized = false,
    int LeftSlideIndex = 0,
    int TopSlideIndex = 0,
    int RightSlideIndex = 0,
    int BottomSlideIndex = 0,
    IReadOnlyList<ParentPaneLayoutSnapshot>? ParentPanes = null,
    IReadOnlyList<ChildPaneDescriptor>? ChildPanes = null);
