using System;
using System.IO;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// v0.1 scaffold for project storage and bootstrap.
    ///
    /// This type is responsible for:
    /// - understanding the on-disk layout of a CONSTELLATE project
    ///   (project.json, engine.db, content/, indexes/),
    /// - providing helpers to resolve those paths.
    ///
    /// Later Phase A work will add:
    /// - CreateProject(path): create folder structure, project.json, empty engine.db,
    /// - OpenProject(path): basic validation and, eventually, DB-backed snapshot load.
    /// </summary>
    public sealed class EngineProjectStore
    {
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
}
