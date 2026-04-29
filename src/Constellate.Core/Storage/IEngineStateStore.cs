using Constellate.Core.Scene;

namespace Constellate.Core.Storage
{
    public interface IEngineStateStore
    {
        bool HasPersistedSnapshot();
        SceneSnapshot? LoadSnapshot();
        void SaveSnapshot(SceneSnapshot snapshot);
    }
}
