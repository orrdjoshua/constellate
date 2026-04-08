using System.Numerics;

namespace Constellate.Core.Scene
{
    public sealed record ViewParams(
        float Yaw,
        float Pitch,
        float Distance,
        Vector3 Target);
}
