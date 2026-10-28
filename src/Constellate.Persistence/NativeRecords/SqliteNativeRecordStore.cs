using System;
using System.Collections.Generic;
using System.Globalization;
using Constellate.Core.Resources;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.NativeRecords
{
    public sealed class SqliteNativeRecordStore : INativeRecordStore
    {
        private const string DefaultRecordKind = "markdown";
        private const string DefaultStorageMode = "EngineNative";

        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SqliteNativeRecordStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS native_records (
    native_record_id TEXT PRIMARY KEY,
    resource_id TEXT NOT NULL,
    record_kind TEXT NOT NULL,
    mime_or_content_type TEXT NOT NULL,
    storage_mode TEXT NOT NULL,
    body TEXT NOT NULL,
    title TEXT NOT NULL,
    current_revision_number INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS native_record_revisions (
    revision_id TEXT PRIMARY KEY,
    resource_id TEXT NOT NULL,
    revision_number INTEGER NOT NULL,
    markdown_body TEXT NOT NULL,
    created_at TEXT NOT NULL,
    change_summary TEXT NULL
);";
            command.ExecuteNonQuery();
            IsInitialized = true;
        }

        public MarkdownRecordState Create(MarkdownRecordState record, MarkdownRecordRevision initialRevision)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(initialRevision);
            EnsureInitialized();

            var currentRecord = ToRecord(record);
            var currentRevision = ToRevisionRecord(initialRevision);

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
INSERT INTO native_records (
    native_record_id,
    resource_id,
    record_kind,
    mime_or_content_type,
    storage_mode,
    body,
    title,
    current_revision_number,
    created_at,
    updated_at
)
VALUES (
    $nativeRecordId,
    $resourceId,
    $recordKind,
    $mimeOrContentType,
    $storageMode,
    $body,
    $title,
    $currentRevisionNumber,
    $createdAt,
    $updatedAt
)
ON CONFLICT(native_record_id) DO UPDATE SET
    resource_id = excluded.resource_id,
    record_kind = excluded.record_kind,
    mime_or_content_type = excluded.mime_or_content_type,
    storage_mode = excluded.storage_mode,
    body = excluded.body,
    title = excluded.title,
    current_revision_number = excluded.current_revision_number,
    created_at = excluded.created_at,
    updated_at = excluded.updated_at;";

                    command.Parameters.AddWithValue("$nativeRecordId", currentRecord.NativeRecordId);
                    command.Parameters.AddWithValue("$resourceId", currentRecord.ResourceId);
                    command.Parameters.AddWithValue("$recordKind", currentRecord.RecordKind);
                    command.Parameters.AddWithValue("$mimeOrContentType", currentRecord.MimeOrContentType);
                    command.Parameters.AddWithValue("$storageMode", currentRecord.StorageMode);
                    command.Parameters.AddWithValue("$body", currentRecord.Body);
                    command.Parameters.AddWithValue("$title", currentRecord.Title);
                    command.Parameters.AddWithValue("$currentRevisionNumber", currentRecord.CurrentRevisionNumber);
                    command.Parameters.AddWithValue("$createdAt", currentRecord.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$updatedAt", currentRecord.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"DELETE FROM native_record_revisions WHERE resource_id = $resourceId;";
                    command.Parameters.AddWithValue("$resourceId", currentRecord.ResourceId);
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
INSERT INTO native_record_revisions (
    revision_id,
    resource_id,
    revision_number,
    markdown_body,
    created_at,
    change_summary
)
VALUES (
    $revisionId,
    $resourceId,
    $revisionNumber,
    $markdownBody,
    $createdAt,
    $changeSummary
);";

                    command.Parameters.AddWithValue("$revisionId", currentRevision.RevisionId);
                    command.Parameters.AddWithValue("$resourceId", currentRevision.ResourceId);
                    command.Parameters.AddWithValue("$revisionNumber", currentRevision.RevisionNumber);
                    command.Parameters.AddWithValue("$markdownBody", currentRevision.MarkdownBody);
                    command.Parameters.AddWithValue("$createdAt", currentRevision.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$changeSummary", (object?)currentRevision.ChangeSummary ?? DBNull.Value);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return ToState(currentRecord);
        }

        public bool TryGet(ResourceId resourceId, out MarkdownRecordState record)
        {
            EnsureInitialized();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    native_record_id,
    resource_id,
    record_kind,
    mime_or_content_type,
    storage_mode,
    body,
    title,
    current_revision_number,
    created_at,
    updated_at
FROM native_records
WHERE resource_id = $resourceId
LIMIT 1;";
            command.Parameters.AddWithValue("$resourceId", resourceId.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                record = null!;
                return false;
            }

            record = ToState(ReadRecord(reader));
            return true;
        }

        public IReadOnlyList<MarkdownRecordRevision> ListRevisions(ResourceId resourceId)
        {
            EnsureInitialized();

            var revisions = new List<MarkdownRecordRevision>();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    revision_id,
    resource_id,
    revision_number,
    markdown_body,
    created_at,
    change_summary
FROM native_record_revisions
WHERE resource_id = $resourceId
ORDER BY revision_number;";
            command.Parameters.AddWithValue("$resourceId", resourceId.ToString());

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                revisions.Add(ToRevision(ReadRevisionRecord(reader)));
            }

            return revisions;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
            return connection;
        }

        private static NativeRecordRecord ReadRecord(SqliteDataReader reader)
        {
            return new NativeRecordRecord(
                ReadRequired(reader, "native_record_id"),
                ReadRequired(reader, "resource_id"),
                ReadRequired(reader, "record_kind"),
                ReadRequired(reader, "mime_or_content_type"),
                ReadRequired(reader, "storage_mode"),
                ReadRequired(reader, "body"),
                ReadRequired(reader, "title"),
                reader.GetInt32(reader.GetOrdinal("current_revision_number")),
                ReadDateTimeOffset(reader, "created_at"),
                ReadDateTimeOffset(reader, "updated_at"));
        }

        private static NativeRecordRevisionRecord ReadRevisionRecord(SqliteDataReader reader)
        {
            return new NativeRecordRevisionRecord(
                ReadRequired(reader, "revision_id"),
                ReadRequired(reader, "resource_id"),
                reader.GetInt32(reader.GetOrdinal("revision_number")),
                ReadRequired(reader, "markdown_body"),
                ReadDateTimeOffset(reader, "created_at"),
                ReadOptional(reader, "change_summary"));
        }

        private static NativeRecordRecord ToRecord(MarkdownRecordState state)
        {
            return new NativeRecordRecord(
                $"native_record:{state.ResourceId}",
                state.ResourceId.ToString(),
                DefaultRecordKind,
                state.ContentType,
                DefaultStorageMode,
                state.MarkdownBody,
                state.Title,
                state.CurrentRevisionNumber,
                state.CreatedAt,
                state.UpdatedAt);
        }

        private static NativeRecordRevisionRecord ToRevisionRecord(MarkdownRecordRevision revision)
        {
            return new NativeRecordRevisionRecord(
                revision.RevisionId,
                revision.ResourceId.ToString(),
                revision.RevisionNumber,
                revision.MarkdownBody,
                revision.CreatedAt,
                revision.ChangeSummary);
        }

        private static MarkdownRecordState ToState(NativeRecordRecord record)
        {
            if (!ResourceId.TryParse(record.ResourceId, out var resourceId))
            {
                throw new InvalidOperationException($"Invalid resource id '{record.ResourceId}' in native record state.");
            }

            return new MarkdownRecordState(
                resourceId,
                record.Title,
                record.MimeOrContentType,
                record.Body,
                record.CurrentRevisionNumber,
                record.CreatedAt,
                record.UpdatedAt);
        }

        private static MarkdownRecordRevision ToRevision(NativeRecordRevisionRecord record)
        {
            if (!ResourceId.TryParse(record.ResourceId, out var resourceId))
            {
                throw new InvalidOperationException($"Invalid resource id '{record.ResourceId}' in native record revision.");
            }

            return new MarkdownRecordRevision(
                record.RevisionId,
                resourceId,
                record.RevisionNumber,
                record.MarkdownBody,
                record.CreatedAt,
                record.ChangeSummary);
        }

        private static string ReadRequired(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
            {
                throw new InvalidOperationException($"Required column '{column}' was null.");
            }

            return reader.GetString(ordinal);
        }

        private static string? ReadOptional(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, string column)
        {
            return DateTimeOffset.Parse(
                ReadRequired(reader, column),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }
    }
}
