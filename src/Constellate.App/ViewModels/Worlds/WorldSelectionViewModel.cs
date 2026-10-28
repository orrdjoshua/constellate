using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Constellate.App.Worlds;

namespace Constellate.App.ViewModels.Worlds
{
    public sealed class WorldSelectionViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<WorldDescriptor> Worlds { get; } = new();

        private WorldDescriptor? _selectedWorld;

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
                OnPropertyChanged(nameof(SelectedWorldSummary));
            }
        }

        public bool HasSelectedWorld => SelectedWorld is not null;

        public string? SelectedWorldSummary
        {
            get
            {
                if (SelectedWorld is null)
                {
                    return null;
                }

                return $"{SelectedWorld.DisplayName} — {SelectedWorld.DatabaseFilePath}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void LoadWorlds(IEnumerable<WorldDescriptor> worlds)
        {
            Worlds.Clear();

            foreach (var world in worlds)
            {
                Worlds.Add(world);
            }

            if (Worlds.Count > 0)
            {
                SelectedWorld = Worlds[0];
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
