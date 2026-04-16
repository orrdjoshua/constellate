namespace Constellate.App;

public sealed record ChildPaneDescriptor(
    string Id,
    string Title,
    int Order,
    int ContainerIndex = 0,
    bool IsMinimized = false,
    int SlideIndex = 0,
    double FloatingX = 0,
    double FloatingY = 0,
    double FloatingWidth = 260,
    double FloatingHeight = 160,
    string? ParentId = null);
