using System;
using Avalonia.Threading;
using Constellate.Core.Messaging;

namespace Constellate.App.Worlds
{
    internal sealed class WorldAutoSaveCoordinator
    {
        public static WorldAutoSaveCoordinator? Current { get; private set; }

        private readonly WorldSettingsStore _settingsStore;
        private DispatcherTimer? _timer;

        private WorldAutoSaveCoordinator(WorldSettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }

        public static void Initialize(string projectRootPath)
        {
            if (Current is not null)
            {
                return;
            }

            Current = new WorldAutoSaveCoordinator(new WorldSettingsStore(projectRootPath));
            Current.ApplyCurrentSettings();
        }

        public void ApplySettings(WorldSaveSettings settings)
        {
            Stop();

            if (!settings.AutoSaveEnabled)
            {
                return;
            }

            var interval = settings.AutoSaveIntervalSeconds <= 0
                ? 60
                : settings.AutoSaveIntervalSeconds;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(interval)
            };

            timer.Tick += (_, _) => PersistActiveWorld();
            timer.Start();

            _timer = timer;
        }

        public void ApplyCurrentSettings()
        {
            ApplySettings(_settingsStore.LoadSettings());
        }

        public void Stop()
        {
            if (_timer is null)
            {
                return;
            }

            _timer.Stop();
            _timer.Tick -= (_, _) => PersistActiveWorld();
            _timer = null;
        }

        private static void PersistActiveWorld()
        {
            EngineServices.PersistCurrentSceneSnapshot();
        }
    }
}
