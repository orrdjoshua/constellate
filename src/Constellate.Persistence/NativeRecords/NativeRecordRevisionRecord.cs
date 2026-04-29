using System;

namespace Constellate.Persistence.NativeRecords
{
    public sealed record NativeRecordRevisionRecord(
        string RevisionId,
        string ResourceId,
        int RevisionNumber,
        string MarkdownBody,
        DateTimeOffset CreatedAt,
        string? ChangeSummary = null);
}
