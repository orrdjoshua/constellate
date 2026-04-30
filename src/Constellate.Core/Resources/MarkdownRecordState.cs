using System;

namespace Constellate.Core.Resources
{
    public sealed record MarkdownRecordState(
        ResourceId ResourceId,
        string Title,
        string ContentType,
        string MarkdownBody,
        int CurrentRevisionNumber,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public string? ResourceTitle =>
            string.IsNullOrWhiteSpace(Title)
                ? null
                : Title.Trim();
    }
}
