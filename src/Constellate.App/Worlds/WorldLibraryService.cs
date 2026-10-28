using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Constellate.Core.Storage;
using Constellate.Persistence.Bootstrap;
using Constellate.Persistence.CurrentState;

namespace Constellate.App.Worlds
{
    internal sealed record WorldDbRootDefinition(
        string RootId,
        string DisplayLabel,
        string SearchDirectory,
        string ProjectRootPath,
        bool IsDefault = false,
        bool IsEnabled = true);

    internal sealed class WorldLibraryService
    {
        private readonly WorldRootsStore _worldRootsStore;

        public WorldLibraryService()
        {
            _worldRootsStore = new WorldRootsStore(ProjectPathResolver.ResolveProjectRootPath());
        }

        public IReadOnlyList<WorldDescriptor> DiscoverWorlds()
        {
            var roots = ResolveEnabledRoots();
            var worlds = new List<WorldDescriptor>();

            foreach (var root in roots)
            {
                if (!Directory.Exists(root.SearchDirectory))
                {
                    continue;
                }

                foreach (var dbPath in Directory.EnumerateFiles(
                    root.SearchDirectory,
                    "*.db",
                    SearchOption.TopDirectoryOnly))
                {
                    worlds.Add(BuildDescriptorFromDatabasePath(dbPath, root));
                }
            }

            return worlds
                .OrderBy(world => world.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public WorldDescriptor CreateNewWorld(string displayName)
        {
            var root = ResolveEnabledRoots().FirstOrDefault(candidate => candidate.IsDefault)
                ?? ResolveEnabledRoots().First();

            var safeName = BuildDatabaseFileName(displayName);
            var databaseFilePath = string.Equals(root.RootId, "default", StringComparison.Ordinal)
                ? null
                : Path.Combine(root.SearchDirectory, safeName);

            var options = new PersistenceBootstrapOptions(
                root.ProjectRootPath,
                "Constellate",
                safeName,
                DatabaseFilePath: databaseFilePath);
            var result = new SqliteProjectPersistenceBootstrapper().Bootstrap(options);

            var engineProjectStore = new SqliteEngineProjectStore(result);

            EngineProjectRecord projectRecord;
            if (result.DatabaseFileCreated)
            {
                projectRecord = engineProjectStore.CreateNew(displayName);
            }
            else if (!engineProjectStore.TryGet(out projectRecord))
            {
                projectRecord = engineProjectStore.CreateNew(
                    Path.GetFileNameWithoutExtension(result.DatabasePath));
            }

            return BuildDescriptorFromProjectRecord(
                projectRecord,
                result.DatabasePath,
                root.RootId,
                result.DatabaseFileCreated);
        }

        private IReadOnlyList<WorldDbRootDefinition> ResolveEnabledRoots()
        {
            return _worldRootsStore.LoadRoots();
        }

        public void AddRoot(string searchDirectory, string? displayLabel = null)
        {
            _worldRootsStore.AddRoot(searchDirectory, displayLabel);
        }

        private static WorldDescriptor BuildDescriptorFromDatabasePath(
            string dbPath,
            WorldDbRootDefinition root)
        {
            var options = new PersistenceBootstrapOptions(
                root.ProjectRootPath,
                "Constellate",
                Path.GetFileName(dbPath),
                DatabaseFilePath: dbPath);
            var result = new SqliteProjectPersistenceBootstrapper().Bootstrap(options);
            var engineProjectStore = new SqliteEngineProjectStore(result);

            if (engineProjectStore.TryGet(out var projectRecord))
            {
                return BuildDescriptorFromProjectRecord(
                    projectRecord,
                    dbPath,
                    root.RootId,
                    isNew: false);
            }

            var fileInfo = new FileInfo(dbPath);
            var fileNameNoExt = Path.GetFileNameWithoutExtension(dbPath);

            return new WorldDescriptor(
                WorldId: fileNameNoExt,
                DisplayName: fileNameNoExt,
                DatabaseFilePath: dbPath,
                RootId: root.RootId,
                Description: null,
                CreatedAt: fileInfo.Exists ? fileInfo.CreationTimeUtc : null,
                LastOpenedAt: fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null,
                EngineSchemaVersion: null,
                ProjectVersion: null,
                IsNew: false,
                IsCompatible: true,
                IsReadOnly: false);
        }

        private static WorldDescriptor BuildDescriptorFromProjectRecord(
            EngineProjectRecord projectRecord,
            string databasePath,
            string rootId,
            bool isNew)
        {
            return new WorldDescriptor(
                WorldId: projectRecord.ProjectId,
                DisplayName: projectRecord.DisplayName,
                DatabaseFilePath: databasePath,
                RootId: rootId,
                Description: null,
                CreatedAt: projectRecord.CreatedAt,
                LastOpenedAt: projectRecord.UpdatedAt,
                EngineSchemaVersion: projectRecord.EngineSchemaVersion,
                ProjectVersion: projectRecord.ProjectVersion,
                IsNew: isNew,
                IsCompatible: true,
                IsReadOnly: false);
        }

        private static string BuildDatabaseFileName(string displayName)
        {
            var builder = new StringBuilder();
            var lastWasSeparator = false;

            foreach (var ch in displayName.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('-');
                    lastWasSeparator = true;
                }
            }

            var slug = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(slug)
                ? "constellate-world.db"
                : $"{slug}.db";
        }
    }
}
