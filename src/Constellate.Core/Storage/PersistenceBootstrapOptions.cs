namespace Constellate.Core.Storage
{
    public sealed record PersistenceBootstrapOptions(
        string ProjectRootPath,
        string ProjectShortName,
        string DatabaseFileName = "constellate.db",
        string DataDirectoryName = ".constellate");
}
