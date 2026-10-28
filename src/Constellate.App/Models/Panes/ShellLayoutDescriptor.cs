using System.Collections.Generic;

namespace Constellate.App;

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
