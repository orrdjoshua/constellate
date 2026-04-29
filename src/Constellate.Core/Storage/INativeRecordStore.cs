using System.Collections.Generic;
using Constellate.Core.Resources;

namespace Constellate.Core.Storage
{
    public interface INativeRecordStore
    {
        void EnsureInitialized();

        MarkdownRecordState Create(MarkdownRecordState record, MarkdownRecordRevision initialRevision);

        bool TryGet(ResourceId resourceId, out MarkdownRecordState record);

        IReadOnlyList<MarkdownRecordRevision> ListRevisions(ResourceId resourceId);
    }
}
