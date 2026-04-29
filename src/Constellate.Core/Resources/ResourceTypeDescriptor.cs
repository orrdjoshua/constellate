using System;

namespace Constellate.Core.Resources
{
    public sealed record ResourceTypeDescriptor(
        string TypeId,
        string DisplayName,
        ResourceFamily Family,
        ResourceOrigin Origin,
        ResourcePosture DefaultPosture)
    {
        public static ResourceTypeDescriptor Create(
            string typeId,
            string displayName,
            ResourceFamily family,
            ResourceOrigin origin,
            ResourcePosture defaultPosture)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Resource type id is required.", nameof(typeId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Resource display name is required.", nameof(displayName));
            }

            return new ResourceTypeDescriptor(
                typeId.Trim(),
                displayName.Trim(),
                family,
                origin,
                defaultPosture);
        }
    }
}
