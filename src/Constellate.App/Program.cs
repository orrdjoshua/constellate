using System;
using System.IO;
using Avalonia;
using Constellate.Core.Messaging;
using Constellate.Core.Storage;
using Constellate.Persistence.Bootstrap;

namespace Constellate.App
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
#if DEBUG
            try
            {
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
                System.Diagnostics.Trace.AutoFlush = true;
            }
            catch { /* ignore */ }
#endif
            ConfigureEnginePersistence();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToTrace();

        private static void ConfigureEnginePersistence()
        {
            try
            {
                var projectRootPath = ResolveProjectRootPath();
                var bootstrapper = new SqliteProjectPersistenceBootstrapper();
                var bootstrapOptions = BuildPersistenceOptions(projectRootPath);
                var bootstrapResult = bootstrapper.Bootstrap(bootstrapOptions);

                var persistenceScope = new SeedPersistenceScope(bootstrapResult);
                EngineServices.ConfigurePersistence(persistenceScope);
            }
            catch
            {
                // Keep application startup non-destructive while persistence remains in bootstrap-stage form.
            }
        }

        /// <summary>
        /// Build the persistence bootstrap options for the current run.
        /// Phase-5 intent:
        /// - Treat a single database file under the project's persistence directory as one concrete world.
        /// - Today we always select the default world database ("constellate.db") for the current clone/worktree.
        /// - A later world-selection flow will call this helper with user/UX-chosen world identity and
        ///   override the database file name accordingly, while keeping the same bootstrapper and scope seams.
        /// </summary>
        private static PersistenceBootstrapOptions BuildPersistenceOptions(string projectRootPath)
        {
            // For now we preserve the existing single-world behavior by relying on the default
            // DatabaseFileName ("constellate.db") exposed by PersistenceBootstrapOptions. The
            // short name remains "Constellate" so downstream stores can stamp project/world
            // metadata consistently even before a richer world-selection UX is in place.
            return new PersistenceBootstrapOptions(projectRootPath, "Constellate");
        }

        private static string ResolveProjectRootPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Constellate.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }
    }
}
