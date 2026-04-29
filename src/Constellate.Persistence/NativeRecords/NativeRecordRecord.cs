using System;

namespace Constellate.Persistence.NativeRecords
{
    public sealed record NativeRecordRecord(
        string NativeRecordId,
        string ResourceId,
        string RecordKind,
        string MimeOrContentType,
        string StorageMode,
        string Body,
        string Title,
        int CurrentRevisionNumber,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
