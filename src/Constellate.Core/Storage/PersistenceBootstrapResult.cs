namespace Constellate.Core.Storage
{
    public sealed record PersistenceBootstrapResult(
        string ProjectRootPath,
        string DataDirectoryPath,
        string DatabasePath,
        string ProviderName,
        bool DatabaseFileCreated);
}
