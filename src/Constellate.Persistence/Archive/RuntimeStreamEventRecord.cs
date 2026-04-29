using System;

namespace Constellate.Persistence.Archive
{
    public sealed record RuntimeStreamEventRecord(
        string RuntimeStreamEventId,
        string RuntimeStreamId,
        long SequenceNo,
        string EventKind,
        string Severity,
        DateTimeOffset EmittedAt,
        string PayloadJson,
        string? CorrelationId = null,
        string? RetentionClass = null);
}
