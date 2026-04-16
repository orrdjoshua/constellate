namespace Constellate.App;

/// <summary>
/// Lightweight in-memory snapshot of a parent pane used only for session-local
/// layout preset save/restore. ParentPaneModel is the authoritative live model.
/// </summary>
public sealed record ParentPaneLayoutSnapshot(
    string Id,
    string Title,
    string HostId,
    bool IsMinimized = false,
    double FloatingX = 0,
    double FloatingY = 0,
    double FloatingWidth = 320,
    double FloatingHeight = 240,
    int SplitCount = 1,
    int SlideIndex = 0);
