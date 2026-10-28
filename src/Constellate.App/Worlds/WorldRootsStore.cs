using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Constellate.App.Worlds
{
    internal sealed class WorldRootsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            IncludeFields = true
        };

        private readonly string _configPath;
        private readonly string _projectRootPath;

        public WorldRootsStore(string projectRootPath)
        {
            _projectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
            _configPath = Path.Combine(projectRootPath, ".constellate", "config", "world-roots.json");
        }

        public IReadOnlyList<WorldDbRootDefinition> LoadRoots()
        {
            var defaultRoot = BuildDefaultRoot();
            var roots = new List<WorldDbRootDefinition> { defaultRoot };

            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var storedRoots = JsonSerializer.Deserialize<List<WorldDbRootDefinition>>(json, JsonOptions);

                    if (storedRoots is not null)
                    {
                        foreach (var root in storedRoots)
                        {
                            var normalized = NormalizeRoot(root);
                            if (normalized is null)
                            {
                                continue;
                            }

                            if (string.Equals(normalized.RootId, defaultRoot.RootId, StringComparison.Ordinal))
                            {
                                roots[0] = normalized;
                            }
                            else
                            {
                                roots.Add(normalized);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Keep startup non-destructive if the world-roots config is unreadable.
            }

            return roots
                .Where(root => root.IsEnabled)
                .DistinctBy(root => root.RootId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void AddRoot(string searchDirectory, string? displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(searchDirectory))
            {
                throw new ArgumentException("Search directory is required.", nameof(searchDirectory));
            }

            var fullPath = Path.GetFullPath(searchDirectory);
            Directory.CreateDirectory(fullPath);

            var existing = LoadRoots();
            var roots = existing.ToList();

            var rootId = BuildRootId(fullPath);
            if (roots.Any(root => string.Equals(root.RootId, rootId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("That DB folder is already registered.");
            }

            roots.Add(new WorldDbRootDefinition(
                rootId,
                string.IsNullOrWhiteSpace(displayLabel)
                    ? new DirectoryInfo(fullPath).Name
                    : displayLabel.Trim(),
                fullPath,
                fullPath,
                IsDefault: false,
                IsEnabled: true));

            SaveRoots(roots);
        }

        private void SaveRoots(IEnumerable<WorldDbRootDefinition> roots)
        {
            var configDirectory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            File.WriteAllText(_configPath, JsonSerializer.Serialize(roots.ToArray(), JsonOptions));
        }

        private WorldDbRootDefinition BuildDefaultRoot()
        {
            return new WorldDbRootDefinition(
                "default",
                "Project-local worlds",
                ProjectPathResolver.ResolveProjectLocalWorldsDirectory(_projectRootPath),
                _projectRootPath,
                IsDefault: true,
                IsEnabled: true);
        }

        private static WorldDbRootDefinition? NormalizeRoot(WorldDbRootDefinition root)
        {
            if (root is null || string.IsNullOrWhiteSpace(root.SearchDirectory))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(root.SearchDirectory);

            return root with
            {
                RootId = string.IsNullOrWhiteSpace(root.RootId)
                    ? BuildRootId(fullPath)
                    : root.RootId.Trim(),
                DisplayLabel = string.IsNullOrWhiteSpace(root.DisplayLabel)
                    ? BuildRootId(fullPath)
                    : root.DisplayLabel.Trim(),
                SearchDirectory = fullPath,
                ProjectRootPath = string.IsNullOrWhiteSpace(root.ProjectRootPath)
                    ? fullPath
                    : root.ProjectRootPath
            };
        }

        private static string BuildRootId(string searchDirectory)
        {
            var fullPath = Path.GetFullPath(searchDirectory);

            return "root:" + fullPath
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '-')
                .Replace(Path.AltDirectorySeparatorChar, '-')
                .Replace(' ', '-')
                .ToLowerInvariant();
        }
    }
}
