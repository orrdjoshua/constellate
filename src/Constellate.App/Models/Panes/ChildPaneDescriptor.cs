namespace Constellate.App;

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
    double FixedSizePixels = 0.0);
