using System;

namespace Constellate.Core.Resources
{
    public sealed record MarkdownRecordRevision(
        string RevisionId,
        ResourceId ResourceId,
        int RevisionNumber,
        string MarkdownBody,
        DateTimeOffset CreatedAt,
        string? ChangeSummary = null);
}
