using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Constellate.Core.Messaging;
using Constellate.Core.Storage;
using Constellate.Persistence.Bootstrap;

namespace Constellate.App.Worlds
{
    public static class WorldSessionCoordinator
    {
        public static void OpenWorld(WorldDescriptor world)
        {
            var scope = CreatePersistenceScope(world);

            EngineServices.ConfigurePersistence(scope);
            EngineHost.ConfigurePersistenceScope(scope);
            EngineHost.EnsureWorldSession();

            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime is null)
            {
                return;
            }

            var mainWindow = new MainWindow();
            lifetime.MainWindow = mainWindow;
            mainWindow.Show();
        }

        public static bool TrySwitchWorld(WorldDescriptor world, out string? error)
        {
            if (world is null)
            {
                error = "No world selected.";
                return false;
            }

            try
            {
                EngineServices.PersistCurrentSceneSnapshot();

                var scope = CreatePersistenceScope(world);
                EngineHost.SwitchWorld(scope);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static IEnginePersistenceScope CreatePersistenceScope(WorldDescriptor world)
        {
            var projectRoot = ProjectPathResolver.ResolveProjectRootPath();

            var options = !string.Equals(world.RootId, "default", StringComparison.Ordinal)
                ? new PersistenceBootstrapOptions(
                    projectRoot,
                    "Constellate",
                    DatabaseFilePath: world.DatabaseFilePath)
                : new PersistenceBootstrapOptions(
                    projectRoot,
                    "Constellate",
                    Path.GetFileName(world.DatabaseFilePath));

            var bootstrap = new SqliteProjectPersistenceBootstrapper().Bootstrap(options);
            return new SeedPersistenceScope(bootstrap);
        }
    }
}
