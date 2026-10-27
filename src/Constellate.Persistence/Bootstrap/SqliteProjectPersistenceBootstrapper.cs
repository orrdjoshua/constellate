using System;
using System.IO;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Bootstrap
{
    public sealed class SqliteProjectPersistenceBootstrapper : IProjectPersistenceBootstrapper
    {
        // Commentary/parity note:
        // This bootstrapper already establishes the intended local-first project persistence root,
        // but it currently proves more about directory/database posture than about durable lane
        // realization. Later persistence work should keep this bootstrap seam narrow while moving
        // real current-state and archive truth into the stores layered beneath it.
        //
        // Phase-5 intent:
        // - Treat the resolved databasePath as the concrete identity for the current project/world
        //   within a given clone/worktree.
        // - On first creation of the file (databaseFileCreated == true), allow composed stores to
        //   perform schema creation plus default seeding for catalogs/definitions/workspaces/
        //   authoring entries as appropriate.
        // - On subsequent opens of an existing file, perform migrations only; do not silently
        //   reseed or overwrite user data. Any "restore defaults" posture should be an explicit
        //   project/world administration flow layered above this bootstrapper, not hidden here.

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
