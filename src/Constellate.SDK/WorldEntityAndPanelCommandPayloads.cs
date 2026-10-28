using System.Collections.Generic;
using System.Numerics;

namespace Constellate.SDK
{
    /// <summary>
    /// Narrower SDK payload family for world/entity mutation plus panel-attachment command data.
    /// This preserves the existing public payload names and namespace while moving them out of
    /// the older mixed-bundle file so App/Core/renderer consumers can reason about this command
    /// family more directly.
    /// </summary>
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

    public sealed record PanelCommandEntryPayload(
        string CommandId,
        string? DisplayLabel = null);

    public sealed record PanelCommandSurfaceMetadataPayload(
        string? SurfaceName = null,
        string? SurfaceGroup = null,
        string? SurfaceSource = null,
        IReadOnlyList<string>? CommandIds = null,
        IReadOnlyList<PanelCommandEntryPayload>? Commands = null);

    public sealed record AttachPanelPayload(
        string Id,
        string ViewRef,
        Vector3? LocalOffset = null,
        Vector2? Size = null,
        string? Anchor = null,
        bool? IsVisible = null,
        string? SurfaceKind = null,
        string? PaneletteKind = null,
        int? PaneletteTier = null,
        PanelCommandSurfaceMetadataPayload? CommandSurface = null);

    public sealed record ClearPanelAttachmentPayload(
        string Id);

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
}