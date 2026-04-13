using System;
using System.IO;
using System.Text.Json;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// v0.1 scaffold for project storage and bootstrap.
    ///
    /// Responsibilities:
    /// - understand the on-disk layout of a CONSTELLATE project
    ///   (project.json, engine.db, content/, indexes/),
    /// - provide helpers to resolve those paths,
    /// - create a new project folder with minimal metadata and an empty DB file,
    /// - open an existing project and return its basic metadata.
    ///
    /// Later Phase A work will add:
    /// - schema bootstrap against engine.db using SPEC-020,
    /// - DB-backed EngineScene hydration in OpenProject.
    /// </summary>
    public sealed class EngineProjectStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        /// <summary>
        /// Compute canonical project paths from an arbitrary root directory path.
        /// This does not create any files or folders; it only normalizes locations.
        /// </summary>
        /// <param name="rootPath">User-supplied project root path.</param>
        /// <returns>Resolved ProjectPaths for this root.</returns>
        /// <exception cref="ArgumentException">Thrown when rootPath is null/empty/whitespace.</exception>
        public ProjectPaths GetProjectPaths(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("Project root path must be non-empty.", nameof(rootPath));

            var fullRoot = Path.GetFullPath(rootPath);

            return new ProjectPaths(
                RootPath: fullRoot,
                ProjectJsonPath: Path.Combine(fullRoot, "project.json"),
                DatabasePath: Path.Combine(fullRoot, "engine.db"),
                ContentRoot: Path.Combine(fullRoot, "content"),
                IndexesRoot: Path.Combine(fullRoot, "indexes"));
        }

        /// <summary>
        /// Create a new project at the given root path.
        ///
        /// Creates:
        /// - root folder (if missing),
        /// - content/ and indexes/ subfolders,
        /// - project.json (if missing),
        /// - an empty engine.db file (if missing).
        ///
        /// Schema bootstrap for engine.db is handled in a later Phase A slice.
        /// </summary>
        /// <param name="rootPath">Desired project root directory.</param>
        /// <param name="name">
        /// Optional project name; if null, the last segment of the root path is used.
        /// </param>
        public ProjectPaths CreateProject(string rootPath, string? name = null)
        {
            var paths = GetProjectPaths(rootPath);

            Directory.CreateDirectory(paths.RootPath);
            Directory.CreateDirectory(paths.ContentRoot);
            Directory.CreateDirectory(paths.IndexesRoot);

            if (!File.Exists(paths.ProjectJsonPath))
            {
                var projectName = string.IsNullOrWhiteSpace(name)
                    ? GetDefaultProjectNameFromRoot(paths.RootPath)
                    : name!.Trim();

                var manifest = new ProjectManifest(
                    Name: projectName,
                    Description: null,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    LastOpenedAtUtc: null,
                    MinimumEngineVersion: "0.1.0");

                var json = JsonSerializer.Serialize(manifest, JsonOptions);
                File.WriteAllText(paths.ProjectJsonPath, json);
            }

            if (!File.Exists(paths.DatabasePath))
            {
                // Create an empty DB file; schema bootstrap will be applied later.
                using var _ = File.Create(paths.DatabasePath);
            }

            return paths;
        }

        /// <summary>
        /// Open an existing project:
        /// - normalizes paths,
        /// - validates the presence of project.json and engine.db,
        /// - deserializes the project manifest,
        /// - updates LastOpenedAtUtc,
        /// - returns paths + manifest.
        ///
        /// DB schema/bootstrap and EngineScene hydration are handled by later CT-002 slices.
        /// </summary>
        /// <param name="rootPath">Existing project root directory.</param>
        /// <exception cref="DirectoryNotFoundException">If the root directory does not exist.</exception>
        /// <exception cref="FileNotFoundException">
        /// If project.json or engine.db are missing under the root.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If project.json cannot be parsed into a ProjectManifest.
        /// </exception>
        public ProjectOpenResult OpenProject(string rootPath)
        {
            var paths = GetProjectPaths(rootPath);

            if (!Directory.Exists(paths.RootPath))
            {
                throw new DirectoryNotFoundException($"Project root not found: {paths.RootPath}");
            }

            if (!File.Exists(paths.ProjectJsonPath))
            {
                throw new FileNotFoundException("Project manifest (project.json) not found.", paths.ProjectJsonPath);
            }

            if (!File.Exists(paths.DatabasePath))
            {
                throw new FileNotFoundException("Project database (engine.db) not found.", paths.DatabasePath);
            }

            var json = File.ReadAllText(paths.ProjectJsonPath);
            ProjectManifest manifest;

            try
            {
                manifest = JsonSerializer.Deserialize<ProjectManifest>(json, JsonOptions)
                           ?? throw new InvalidOperationException("project.json contained no manifest data.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse project.json at '{paths.ProjectJsonPath}'.", ex);
            }

            // Update LastOpenedAtUtc for basic project bookkeeping.
            var updatedManifest = manifest with { LastOpenedAtUtc = DateTimeOffset.UtcNow };
            if (!Equals(manifest, updatedManifest))
            {
                var updatedJson = JsonSerializer.Serialize(updatedManifest, JsonOptions);
                File.WriteAllText(paths.ProjectJsonPath, updatedJson);
                manifest = updatedManifest;
            }

            // NOTE: schema bootstrap + EngineScene hydration will be layered on top of this
            // entry point once SPEC-020 is wired through a SQLite provider.
            return new ProjectOpenResult(paths, manifest);
        }

        private static string GetDefaultProjectNameFromRoot(string rootPath)
        {
            var trimmed = rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            var lastSegment = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(lastSegment) ? "ConstellateProject" : lastSegment;
        }
    }

    /// <summary>
    /// Canonical on-disk structure for a CONSTELLATE project.
    /// Mirrors RFC-081 and is the shared value object used by higher-level
    /// CreateProject/OpenProject flows.
    /// </summary>
    public readonly record struct ProjectPaths(
        string RootPath,
        string ProjectJsonPath,
        string DatabasePath,
        string ContentRoot,
        string IndexesRoot);

    /// <summary>
    /// Minimal project.json manifest; serialized with camelCase properties via JsonOptions.
    /// Fields are aligned with RFC-081 but kept small for v0.1.
    /// </summary>
    public sealed record ProjectManifest(
        string Name,
        string? Description,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastOpenedAtUtc,
        string MinimumEngineVersion);

    /// <summary>
    /// Result object for OpenProject, bundling resolved paths and the loaded manifest.
    /// Future CT-002 work will add DB-backed scene/engine state on top of this.
    /// </summary>
    public sealed record ProjectOpenResult(
        ProjectPaths Paths,
        ProjectManifest Manifest);
}
