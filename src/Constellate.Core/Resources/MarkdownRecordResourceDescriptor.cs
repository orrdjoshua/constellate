using System;

namespace Constellate.Core.Resources
{
    public sealed record MarkdownRecordResourceDescriptor(
        ResourceId ResourceId,
        string DisplayLabel,
        string Title,
        string ContentType = "text/markdown")
    {
        public const string DefaultTypeId = "resource_type.constellate.markdown_record";
        public const string DefaultProviderId = "provider.constellate.native.records";
        public const string DefaultContentType = "text/markdown";
        public const string DefaultAuthorityMode = "EngineNative";
        public const string DefaultLocality = "Local";
        public const string DefaultDetailViewRefPrefix = "resource.markdown.detail";
        public const string DefaultDetailSurfaceRole = "resource.markdown.detail";
        public const string DefaultLifecycleState = "Created";

        public static ResourceTypeDescriptor ResourceType { get; } = ResourceTypeDescriptor.Create(
            DefaultTypeId,
            "Markdown Record",
            ResourceFamily.Record,
            ResourceOrigin.Native,
            ResourcePosture.Passive);

        public ResourceFamily Family => ResourceType.Family;
        public ResourceOrigin Origin => ResourceType.Origin;
        public ResourcePosture Posture => ResourceType.DefaultPosture;
        public string TypeId => ResourceType.TypeId;
        public string DetailViewRef => BuildDetailViewRef(ResourceId);

        public ResourceRegistration CreateRegistration(DateTimeOffset? timestamp = null)
        {
            var resolvedTimestamp = timestamp ?? DateTimeOffset.UtcNow;

            return new ResourceRegistration(
                ResourceId,
                TypeId,
                Family,
                DefaultProviderId,
                Origin,
                Posture,
                DefaultAuthorityMode,
                DefaultLocality,
                DefaultLifecycleState,
                DisplayLabel,
                resolvedTimestamp,
                resolvedTimestamp,
                DetailViewRef);
        }

        public MarkdownRecordState CreateInitialState(
            string? markdownBody = null,
            DateTimeOffset? timestamp = null)
        {
            var resolvedTimestamp = timestamp ?? DateTimeOffset.UtcNow;

            return new MarkdownRecordState(
                ResourceId,
                Title,
                ContentType,
                markdownBody ?? string.Empty,
                1,
                resolvedTimestamp,
                resolvedTimestamp);
        }

        public MarkdownRecordRevision CreateInitialRevision(
            string? markdownBody = null,
            string? changeSummary = null,
            DateTimeOffset? timestamp = null)
        {
            var resolvedTimestamp = timestamp ?? DateTimeOffset.UtcNow;

            return new MarkdownRecordRevision(
                $"revision:{Guid.NewGuid():N}",
                ResourceId,
                1,
                markdownBody ?? string.Empty,
                resolvedTimestamp,
                string.IsNullOrWhiteSpace(changeSummary) ? "Initial revision" : changeSummary.Trim());
        }

        public static string BuildDetailViewRef(ResourceId resourceId)
        {
            return $"{DefaultDetailViewRefPrefix}:{resourceId}";
        }

        public static MarkdownRecordResourceDescriptor Create(string title, string? displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Record title is required.", nameof(title));
            }

            var normalizedTitle = title.Trim();
            var normalizedDisplayLabel = string.IsNullOrWhiteSpace(displayLabel)
                ? normalizedTitle
                : displayLabel.Trim();

            return new MarkdownRecordResourceDescriptor(
                ResourceId.New(),
                normalizedDisplayLabel,
                normalizedTitle,
                DefaultContentType);
        }
    }
}
