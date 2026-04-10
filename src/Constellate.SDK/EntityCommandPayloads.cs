using System;
using System.Collections.Generic;
using System.Numerics;

namespace Constellate.SDK
{
    public sealed record NodeAppearancePayload(
        string? Primitive = null,
        string? FillColor = null,
        string? OutlineColor = null,
        float? Opacity = null);

    public sealed record CreateEntityPayload(
        string Type,
        string? Id,
        string? Label,
        Vector3? Position,
        Vector3? RotationEuler,
        Vector3? Scale,
        float? VisualScale,
        float? Phase,
        NodeAppearancePayload? Appearance = null);

    public sealed record UpdateEntityPayload(
        string Id,
        string? Label,
        Vector3? Position,
        Vector3? RotationEuler,
        Vector3? Scale,
        float? VisualScale,
        float? Phase,
        NodeAppearancePayload? Appearance = null);

    public sealed record UpdateEntitiesPayload(
        IReadOnlyList<UpdateEntityPayload> Entities);

    public sealed record DeleteEntityPayload(string Id);

    public sealed record DeleteEntitiesPayload(
        IReadOnlyList<string> Ids);

    public sealed record SetTransformPayload(
        string Id,
        Vector3? Position,
        Vector3? RotationEuler,
        Vector3? Scale);

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

    public sealed record AttachPanelPayload(
        string Id,
        string ViewRef,
        Vector3? LocalOffset = null,
        Vector2? Size = null,
        string? Anchor = null,
        bool? IsVisible = null);

    public sealed record ConnectEntitiesPayload(
        string SourceId,
        string TargetId,
        string? Kind = null,
        float? Weight = null);

    public sealed record UnlinkEntitiesPayload(
        string SourceId,
        string TargetId,
        string? Kind = null,
        float? Weight = null);

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

    public sealed record CommandInvokedPayload(
        string CommandName,
        Guid CommandId);

    // View/camera bridge payloads
    public sealed record ViewChangedPayload(
        float Yaw,
        float Pitch,
        float Distance,
        Vector3 Target);

    public sealed record ViewSetRequestedPayload(
        float Yaw,
        float Pitch,
        float Distance,
        Vector3 Target);
}
