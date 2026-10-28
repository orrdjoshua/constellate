using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Constellate.Core.Storage;
using Constellate.Persistence.Bootstrap;

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

            return BuildDescriptorFromBootstrap(
                result.DatabasePath,
                root.RootId,
                displayName,
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

        private static WorldDescriptor BuildDescriptorFromBootstrap(
            string databasePath,
            string rootId,
            string displayName,
            bool isNew)
        {
            var fileInfo = new FileInfo(databasePath);

            return new WorldDescriptor(
                WorldId: Path.GetFileNameWithoutExtension(databasePath),
                DisplayName: displayName,
                DatabaseFilePath: databasePath,
                RootId: rootId,
                Description: null,
                CreatedAt: fileInfo.Exists ? fileInfo.CreationTimeUtc : DateTimeOffset.UtcNow,
                LastOpenedAt: fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTimeOffset.UtcNow,
                EngineSchemaVersion: null,
                ProjectVersion: null,
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
