using System.Numerics;

namespace Constellate.Core.Scene
{
    public readonly record struct Transform(Vector3 Position, Vector3 RotationEuler, Vector3 Scale)
    {
        public static Transform Identity => new(Vector3.Zero, Vector3.Zero, Vector3.One);
    }
}
