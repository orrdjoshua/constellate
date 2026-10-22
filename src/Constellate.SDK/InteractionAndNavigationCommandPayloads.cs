using System.Collections.Generic;

namespace Constellate.SDK
{
    /// <summary>
    /// Narrower SDK payload family for focus/selection/group/navigation command data.
    /// The types stay in the same namespace so existing consumers keep compiling while
    /// the file-level organization becomes easier to evolve and review.
    /// </summary>
    public sealed record FocusEntityPayload(string Id);

    public sealed record FocusPanelPayload(
        string Id,
        string ViewRef);

    public sealed record SelectEntitiesPayload(
        IReadOnlyList<string> Ids,
        bool Replace = true);

    public sealed record SelectPanelPayload(
        string Id,
        string ViewRef,
        bool Replace = true);

    public sealed record GroupSelectionPayload(
        string? Label = null);

    public sealed record GroupMembershipPayload(
        string GroupId);

    public sealed record DeleteGroupPayload(
        string GroupId);

    public sealed record BookmarkSavePayload(
        string Name);

    public sealed record CenterOnNodePayload(
        string Id,
        float? Distance = null);

    public sealed record FrameSelectionPayload(
        IReadOnlyList<string>? Ids = null,
        float Padding = 1.35f);

    public sealed record SetInteractionModePayload(
        string Mode);

    public sealed record BookmarkRestorePayload(
        string Name);

    public sealed record EnterNodePayload(
        string Id,
        string? Mode = null);

    public sealed record ExitNodePayload(
        string? Id = null);

    public sealed record ExpandNodePayload(
        string Id);

    public sealed record CollapseNodePayload(
        string Id);
}
