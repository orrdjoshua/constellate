using System;
using System.Numerics;

namespace Constellate.SDK
{
    /// <summary>
    /// Narrower SDK payload family for runtime invocation signals and the renderer/Core
    /// view-camera bridge payloads. This keeps the public types stable while clarifying
    /// the boundary between command-domain payloads and view-state signaling.
    /// </summary>
    public sealed record CommandInvokedPayload(
        string CommandName,
        Guid CommandId);

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
