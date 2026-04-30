using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Resources;
using Constellate.Core.Storage;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public sealed record ResolvedResourceSurfaceBindingState(
        string ProjectionBindingId,
        string ResourceId,
        string? ResourceTypeId,
        string? ResourceDisplayLabel,
        string? ResourceTitle,
        string SurfaceRole,
        string ViewRef,
        string ProjectionMode,
        string TargetSurfaceKind,
        string BindingState,
        string TargetKind,
        string? TargetId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public string BindingKey =>
            $"{ResourceSurfaceBindingPayload.BindingKindResourceSurface}:{ProjectionMode}:{TargetSurfaceKind}:{SurfaceRole}:{ViewRef}";

        public bool IsActive =>
            string.Equals(BindingState, "active", StringComparison.OrdinalIgnoreCase);
    }

    public static partial class EngineServices
    {
        private static string BuildProjectionBindingId(ResourceId resourceId, ResourceSurfaceBindingPayload binding)
        {
            return $"{resourceId}:{binding.BindingKey}";
        }

        public static IReadOnlyList<ResolvedResourceSurfaceBindingState> ListResolvedResourceSurfaceBindings(
            string? resourceId = null,
            string? projectionMode = null,
            string? targetSurfaceKind = null,
            bool activeOnly = false)
        {
            EnsureInitialized();

            var projectionBindingStore = PersistenceScope?.ProjectionBindingStore;
            if (projectionBindingStore is null)
            {
                return Array.Empty<ResolvedResourceSurfaceBindingState>();
            }

            IEnumerable<EngineProjectionBindingState> bindings = string.IsNullOrWhiteSpace(resourceId)
                ? projectionBindingStore.ListAll()
                : projectionBindingStore.ListByResource(resourceId.Trim());

            if (activeOnly)
            {
                bindings = bindings.Where(binding =>
                    binding.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(projectionMode))
            {
                var normalizedProjectionMode = projectionMode.Trim();
                bindings = bindings.Where(binding =>
                    string.Equals(binding.ProjectionMode, normalizedProjectionMode, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(targetSurfaceKind))
            {
                var normalizedTargetSurfaceKind = targetSurfaceKind.Trim();
                bindings = bindings.Where(binding =>
                    string.Equals(binding.TargetSurfaceKind, normalizedTargetSurfaceKind, StringComparison.OrdinalIgnoreCase));
            }

            var registrationsById = ListResourceRegistrationsById();

            return bindings
                .OrderByDescending(binding => binding.UpdatedAt)
                .ThenBy(binding => binding.ProjectionBindingId, StringComparer.Ordinal)
                .Select(binding => ResolveResolvedResourceSurfaceBindingState(binding, registrationsById))
                .ToArray();
        }

        public static ResolvedResourceSurfaceBindingState? GetResolvedResourceSurfaceBinding(string projectionBindingId)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(projectionBindingId))
            {
                return null;
            }

            var projectionBindingStore = PersistenceScope?.ProjectionBindingStore;
            if (projectionBindingStore is null ||
                !projectionBindingStore.TryGet(projectionBindingId.Trim(), out var binding))
            {
                return null;
            }

            return TryGetResourceRegistration(binding.ResourceId, out var registration)
                ? ResolveResolvedResourceSurfaceBindingState(binding, registration)
                : ResolveResolvedResourceSurfaceBindingState(binding, registration: null);
        }

        private static IReadOnlyDictionary<string, ResourceRegistration> ListResourceRegistrationsById()
        {
            return PersistenceScope?.ResourceRegistryStore?
                .ListAll()
                .ToDictionary(
                    registration => registration.ResourceId.ToString(),
                    registration => registration,
                    StringComparer.Ordinal)
                ?? new Dictionary<string, ResourceRegistration>(StringComparer.Ordinal);
        }

        private static bool TryGetResourceRegistration(string resourceId, out ResourceRegistration registration)
        {
            registration = null!;

            if (string.IsNullOrWhiteSpace(resourceId) ||
                !ResourceId.TryParse(resourceId.Trim(), out var parsedResourceId))
            {
                return false;
            }

            var resourceRegistryStore = PersistenceScope?.ResourceRegistryStore;
            return resourceRegistryStore is not null &&
                resourceRegistryStore.TryGet(parsedResourceId, out registration);
        }

        private static ResolvedResourceSurfaceBindingState ResolveResolvedResourceSurfaceBindingState(
            EngineProjectionBindingState binding,
            IReadOnlyDictionary<string, ResourceRegistration> registrationsById)
        {
            registrationsById.TryGetValue(binding.ResourceId, out var registration);
            return ResolveResolvedResourceSurfaceBindingState(binding, registration);
        }

        private static ResolvedResourceSurfaceBindingState ResolveResolvedResourceSurfaceBindingState(
            EngineProjectionBindingState binding,
            ResourceRegistration? registration)
        {
            var resourceDisplayLabel = ResolveResourceDisplayLabel(registration);
            var resourceTitle = ResolveNativeRecordResourceTitle(binding.ResourceId);

            return new ResolvedResourceSurfaceBindingState(
                binding.ProjectionBindingId,
                binding.ResourceId,
                binding.ResourceTypeId,
                resourceDisplayLabel,
                resourceTitle,
                binding.SurfaceRole,
                binding.ViewRef,
                binding.ProjectionMode,
                binding.TargetSurfaceKind,
                binding.BindingState,
                binding.TargetKind,
                binding.TargetId,
                binding.CreatedAt,
                binding.UpdatedAt);
        }

        private static string? ResolveResourceDisplayLabel(ResourceRegistration? registration)
        {
            if (registration is null ||
                string.IsNullOrWhiteSpace(registration.DisplayLabel))
            {
                return null;
            }

            return registration.DisplayLabel.Trim();
        }

        private static void PersistResourceSurfaceBinding(
            ResourceId resourceId,
            string? resourceTypeId,
            ResourceSurfaceBindingPayload binding,
            string bindingState,
            string? targetKind,
            string? targetId)
        {
            var projectionBindingStore = PersistenceScope?.ProjectionBindingStore;
            if (projectionBindingStore is null)
            {
                return;
            }

            var projectionBindingId = BuildProjectionBindingId(resourceId, binding);
            var timestamp = DateTimeOffset.UtcNow;
            var createdAt = projectionBindingStore.TryGet(projectionBindingId, out var existing)
                ? existing.CreatedAt
                : timestamp;

            projectionBindingStore.Upsert(new EngineProjectionBindingState(
                projectionBindingId,
                resourceId.ToString(),
                resourceTypeId,
                binding.SurfaceRole,
                binding.ViewRef,
                binding.ProjectionMode,
                binding.TargetSurfaceKind,
                bindingState,
                NormalizeTargetKind(targetKind, binding),
                string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim(),
                createdAt,
                timestamp));
        }

        private static string NormalizeTargetKind(string? targetKind, ResourceSurfaceBindingPayload binding)
        {
            if (!string.IsNullOrWhiteSpace(targetKind))
            {
                return targetKind.Trim();
            }

            return string.Equals(
                binding.TargetSurfaceKind,
                ResourceSurfaceBindingPayload.TargetSurfaceKindWorldNode,
                StringComparison.OrdinalIgnoreCase)
                ? "node"
                : "surface";
        }
    }
}
