using System;
using System.IO;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Bootstrap
{
    public sealed class SqliteProjectPersistenceBootstrapper : IProjectPersistenceBootstrapper
    {
        public PersistenceBootstrapResult Bootstrap(PersistenceBootstrapOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.ProjectRootPath))
            {
                throw new ArgumentException("Project root path is required.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.ProjectShortName))
            {
                throw new ArgumentException("Project short name is required.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.DatabaseFileName))
            {
                throw new ArgumentException("Database file name is required.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.DataDirectoryName))
            {
                throw new ArgumentException("Data directory name is required.", nameof(options));
            }

            var projectRootPath = Path.GetFullPath(options.ProjectRootPath);
            var dataDirectoryPath = Path.Combine(projectRootPath, options.DataDirectoryName, "persistence");
            Directory.CreateDirectory(dataDirectoryPath);

            var databasePath = Path.Combine(dataDirectoryPath, options.DatabaseFileName);
            var databaseFileCreated = false;

            if (!File.Exists(databasePath))
            {
                using var _ = File.Create(databasePath);
                databaseFileCreated = true;
            }

            return new PersistenceBootstrapResult(
                projectRootPath,
                dataDirectoryPath,
                databasePath,
                "sqlite",
                databaseFileCreated);
        }
    }
}
