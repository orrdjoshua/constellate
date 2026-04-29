using System;

namespace Constellate.Core.Resources
{
    public readonly record struct ResourceId(Guid Value)
    {
        public static ResourceId New() => new(Guid.NewGuid());

        public static bool TryParse(string? rawValue, out ResourceId resourceId)
        {
            if (Guid.TryParse(rawValue, out var value))
            {
                resourceId = new ResourceId(value);
                return true;
            }

            resourceId = default;
            return false;
        }

        public override string ToString() => Value.ToString();
    }
}
