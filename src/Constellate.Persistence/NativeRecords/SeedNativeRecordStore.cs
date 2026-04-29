using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Resources;
using Constellate.Core.Storage;

namespace Constellate.Persistence.NativeRecords
{
    public sealed class SeedNativeRecordStore : INativeRecordStore
    {
        private const string DefaultRecordKind = "markdown";
        private const string DefaultStorageMode = "EngineNative";
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SeedNativeRecordStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public IList<NativeRecordRecord> Records { get; } = new List<NativeRecordRecord>();

        public IList<NativeRecordRevisionRecord> Revisions { get; } = new List<NativeRecordRevisionRecord>();

        public void EnsureInitialized()
        {
            IsInitialized = true;
        }

        public MarkdownRecordState Create(MarkdownRecordState record, MarkdownRecordRevision initialRevision)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(initialRevision);

            var currentRecord = ToRecord(record);

            for (var index = 0; index < Records.Count; index++)
            {
                if (string.Equals(Records[index].ResourceId, currentRecord.ResourceId, StringComparison.Ordinal))
                {
                    Records[index] = currentRecord;
                    RemoveRevisionsForResource(currentRecord.ResourceId);
                    Revisions.Add(ToRevisionRecord(initialRevision));
                    return ToState(currentRecord);
                }
            }

            Records.Add(currentRecord);
            Revisions.Add(ToRevisionRecord(initialRevision));
            return ToState(currentRecord);
        }

        public bool TryGet(ResourceId resourceId, out MarkdownRecordState record)
        {
            foreach (var entry in Records)
            {
                if (string.Equals(entry.ResourceId, resourceId.ToString(), StringComparison.Ordinal))
                {
                    record = ToState(entry);
                    return true;
                }
            }

            record = null!;
            return false;
        }

        public IReadOnlyList<MarkdownRecordRevision> ListRevisions(ResourceId resourceId)
        {
            return Revisions
                .Where(entry => string.Equals(entry.ResourceId, resourceId.ToString(), StringComparison.Ordinal))
                .OrderBy(entry => entry.RevisionNumber)
                .Select(ToRevision)
                .ToArray();
        }

        private void RemoveRevisionsForResource(string resourceId)
        {
            for (var index = Revisions.Count - 1; index >= 0; index--)
            {
                if (string.Equals(Revisions[index].ResourceId, resourceId, StringComparison.Ordinal))
                {
                    Revisions.RemoveAt(index);
                }
            }
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
    }
}
