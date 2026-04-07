using System;
using System.Collections.Generic;
using System.Numerics;

namespace Constellate.SDK
{
    public sealed record CreateEntityPayload(
        string Type,
        string? Id,
        string? Label,
        Vector3? Position,
        Vector3? RotationEuler,
        Vector3? Scale,
        float? VisualScale,
        float? Phase);

    public sealed record UpdateEntityPayload(
        string Id,
        string? Label,
        Vector3? Position,
        Vector3? RotationEuler,
        Vector3? Scale,
        float? VisualScale,
        float? Phase);

    public sealed record DeleteEntityPayload(string Id);

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

    public sealed record CommandInvokedPayload(
        string CommandName,
        Guid CommandId);
}
