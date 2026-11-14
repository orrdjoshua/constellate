using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
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
            var identity = CreateWorldIdentity(world);

            EngineServices.ConfigurePersistence(scope);
            EngineHost.ConfigurePersistenceScope(scope);
            EngineHost.ConfigureWorldIdentity(identity);
            EngineHost.SwitchWorld(scope, identity);

            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime is null)
            {
                return;
            }

            var mainWindow = new MainWindow();
            lifetime.MainWindow = mainWindow;
            mainWindow.Show();
        }

        public static bool TryOpenWorld(WorldDescriptor world, out string? error)
        {
            try
            {
                OpenWorld(world);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySwitchWorld(
            WorldDescriptor world,
            bool saveCurrentWorld,
            out string? error)
        {
            if (world is null)
            {
                error = "No world selected.";
                return false;
            }

            try
            {
                if (saveCurrentWorld)
                {
                    EngineServices.PersistCurrentSceneSnapshot();
                }

                var scope = CreatePersistenceScope(world);
                EngineHost.SwitchWorld(scope, CreateWorldIdentity(world));
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryExitToWorldSelection(
            bool saveCurrentWorld,
            out string? error)
        {
            try
            {
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                if (lifetime is null)
                {
                    error = "Desktop application lifetime is not available.";
                    return false;
                }

                if (saveCurrentWorld)
                {
                    EngineServices.PersistCurrentSceneSnapshot();
                }

                lifetime.MainWindow?.Close();
                EngineHost.ShutdownWorld();

                var selectionWindow = new WorldSelectionWindow();
                lifetime.MainWindow = selectionWindow;
                selectionWindow.Show();
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
            return new SqlitePersistenceScope(bootstrap);
        }

        private static EngineWorldSessionIdentity CreateWorldIdentity(WorldDescriptor world)
        {
            return new EngineWorldSessionIdentity(
                world.WorldId,
                world.DisplayName,
                world.DatabaseFilePath,
                world.IsNew,
                world.ProjectVersion,
                world.EngineSchemaVersion,
                world.CreatedAt,
                world.LastOpenedAt);
        }
    }
}
