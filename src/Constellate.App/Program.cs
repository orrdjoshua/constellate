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
                var bootstrapResult = bootstrapper.Bootstrap(
                    new PersistenceBootstrapOptions(projectRootPath, "Constellate"));

                var persistenceScope = new SeedPersistenceScope(bootstrapResult);
                EngineServices.ConfigurePersistence(persistenceScope);
            }
            catch
            {
                // Keep application startup non-destructive while persistence remains in bootstrap-stage form.
            }
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
