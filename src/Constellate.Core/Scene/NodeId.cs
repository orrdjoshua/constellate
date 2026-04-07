using System;

namespace Constellate.Core.Scene
{
    public readonly record struct NodeId(Guid Value)
    {
        public static NodeId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
