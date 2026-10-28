using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Constellate.App.Worlds;
using Constellate.Core.Messaging;

namespace Constellate.App.ViewModels.Worlds
{
    public sealed class WorldOverlayViewModel : INotifyPropertyChanged
    {
        private readonly WorldLibraryService _worldLibraryService;
        private readonly WorldSettingsStore _worldSettingsStore;

        private WorldDescriptor? _selectedWorld;
        private bool _autoSaveEnabled;
        private int _autoSaveIntervalSeconds;

        public WorldOverlayViewModel()
        {
            _worldLibraryService = new WorldLibraryService();
            _worldSettingsStore = new WorldSettingsStore(ProjectPathResolver.ResolveProjectRootPath());

            var settings = _worldSettingsStore.LoadSettings();
            _autoSaveEnabled = settings.AutoSaveEnabled;
            _autoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
        }

        public ObservableCollection<WorldDescriptor> Worlds { get; } = new();

        public string? CurrentWorldId => EngineHost.CurrentWorldId;
        public string? CurrentWorldDisplayName => EngineHost.CurrentWorldDisplayName;
        public string? CurrentWorldDatabasePath => EngineHost.CurrentWorldDatabasePath;
        public bool CurrentWorldIsNewWorld => EngineHost.CurrentWorldIsNewWorld;

        public WorldDescriptor? SelectedWorld
        {
            get => _selectedWorld;
            set
            {
                if (Equals(_selectedWorld, value))
                {
                    return;
                }

                _selectedWorld = value;
                OnPropertyChanged(nameof(SelectedWorld));
                OnPropertyChanged(nameof(HasSelectedWorld));
            }
        }

        public bool HasSelectedWorld => SelectedWorld is not null;

        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                if (_autoSaveEnabled == value)
                {
                    return;
                }

                _autoSaveEnabled = value;
                OnPropertyChanged(nameof(AutoSaveEnabled));
            }
        }

        public int AutoSaveIntervalSeconds
        {
            get => _autoSaveIntervalSeconds;
            set
            {
                if (_autoSaveIntervalSeconds == value)
                {
                    return;
                }

                _autoSaveIntervalSeconds = value;
                OnPropertyChanged(nameof(AutoSaveIntervalSeconds));
            }
        }

        public void Load()
        {
            RefreshCurrentWorld();

            Worlds.Clear();

            foreach (var world in _worldLibraryService.DiscoverWorlds())
            {
                Worlds.Add(world);
            }

            SelectedWorld = Worlds.FirstOrDefault(world =>
                !string.Equals(world.WorldId, CurrentWorldId, StringComparison.Ordinal))
                ?? Worlds.FirstOrDefault();
        }

        public void RefreshCurrentWorld()
        {
            OnPropertyChanged(nameof(CurrentWorldId));
            OnPropertyChanged(nameof(CurrentWorldDisplayName));
            OnPropertyChanged(nameof(CurrentWorldDatabasePath));
            OnPropertyChanged(nameof(CurrentWorldIsNewWorld));
        }

        public void SaveWorldNow()
        {
            EngineServices.PersistCurrentSceneSnapshot();
        }

        public void ApplySettings()
        {
            var settings = new WorldSaveSettings(
                _autoSaveEnabled,
                _autoSaveIntervalSeconds,
                WorldSaveBehavior.Ask,
                WorldSaveBehavior.Ask);

            _worldSettingsStore.SaveSettings(settings);
            WorldAutoSaveCoordinator.Current?.ApplySettings(settings);
        }

        public bool TrySwitchSelectedWorld(out string? error)
        {
            if (SelectedWorld is null)
            {
                error = "No world selected.";
                return false;
            }

            var succeeded = WorldSessionCoordinator.TrySwitchWorld(SelectedWorld, out error);

            if (succeeded)
            {
                RefreshCurrentWorld();
            }

            return succeeded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
