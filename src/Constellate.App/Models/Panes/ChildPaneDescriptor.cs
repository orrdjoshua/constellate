namespace Constellate.App;

public sealed record ChildPaneDescriptor(
    string Id,
    string Title,
    int Order,
    int ContainerIndex = 0,
    bool IsMinimized = false,
    int SlideIndex = 0,
    string? ParentId = null);
