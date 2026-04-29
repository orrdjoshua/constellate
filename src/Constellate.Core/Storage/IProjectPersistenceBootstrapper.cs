namespace Constellate.Core.Storage
{
    public interface IProjectPersistenceBootstrapper
    {
        PersistenceBootstrapResult Bootstrap(PersistenceBootstrapOptions options);
    }
}
