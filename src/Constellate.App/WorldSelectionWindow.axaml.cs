using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Constellate.App.ViewModels.Worlds;
using Constellate.App.Worlds;
using Constellate.Core.Messaging;
using Constellate.Core.Storage;
using Constellate.Persistence.Bootstrap;

namespace Constellate.App
{
    public partial class WorldSelectionWindow : Window
    {
        private readonly WorldSelectionViewModel _viewModel = new();
        private readonly WorldLibraryService _worldLibraryService = new();

        public WorldSelectionWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            LoadWorlds();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LoadWorlds()
        {
            var worlds = _worldLibraryService.DiscoverWorlds();
            _viewModel.LoadWorlds(worlds);
        }

        private async void Browse_OnClick(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Constellate World Database",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Constellate World Database")
                    {
                        Patterns = new[] { "*.db" }
                    }
                }
            });

            var file = files?.FirstOrDefault();
            if (file is null)
            {
                return;
            }

            var descriptor = BuildDescriptorFromPath(file.Path.LocalPath, "ad-hoc");
            _viewModel.LoadWorlds(new[] { descriptor }
                .Concat(_viewModel.Worlds)
                .DistinctBy(world => world.DatabaseFilePath, StringComparer.OrdinalIgnoreCase));
            _viewModel.SelectedWorld = descriptor;
        }

        private void CreateWorld_OnClick(object? sender, RoutedEventArgs e)
        {
            NewWorldNameBox.Text = string.Empty;
            CreateWorldForm.IsVisible = true;
        }

        private void CancelCreateWorld_OnClick(object? sender, RoutedEventArgs e)
        {
            CreateWorldForm.IsVisible = false;
        }

        private void ConfirmCreateWorld_OnClick(object? sender, RoutedEventArgs e)
        {
            var name = NewWorldNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Untitled World";
            }

            var descriptor = _worldLibraryService.CreateNewWorld(name);
            _viewModel.Worlds.Add(descriptor);
            _viewModel.SelectedWorld = descriptor;
            CreateWorldForm.IsVisible = false;
        }

        private void AddRoot_OnClick(object? sender, RoutedEventArgs e)
        {
            // Future DB-root management lands in PLAN-129 Phase D.
        }

        private void OpenSelected_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedWorld is not { } selected)
            {
                return;
            }

            OpenWorld(selected);
        }

        private void OpenWorld(WorldDescriptor world)
        {
            var projectRoot = ProjectPathResolver.ResolveProjectRootPath();

            var options = string.IsNullOrWhiteSpace(world.RootId) ||
                          string.Equals(world.RootId, "ad-hoc", StringComparison.Ordinal)
                ? new PersistenceBootstrapOptions(
                    projectRoot,
                    "Constellate",
                    DatabaseFilePath: world.DatabaseFilePath)
                : new PersistenceBootstrapOptions(
                    projectRoot,
                    "Constellate",
                    Path.GetFileName(world.DatabaseFilePath));

            var bootstrap = new SqliteProjectPersistenceBootstrapper().Bootstrap(options);
            var scope = new SeedPersistenceScope(bootstrap);

            EngineServices.ConfigurePersistence(scope);
            EngineHost.ConfigurePersistenceScope(scope);
            EngineHost.EnsureWorldSession();

            var lifetime = (IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;
            if (lifetime is null)
            {
                return;
            }

            var mainWindow = new MainWindow();
            lifetime.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }

        private static WorldDescriptor BuildDescriptorFromPath(string path, string rootId)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var info = new FileInfo(path);

            return new WorldDescriptor(
                WorldId: fileName,
                DisplayName: fileName,
                DatabaseFilePath: path,
                RootId: rootId,
                Description: null,
                CreatedAt: info.Exists ? info.CreationTimeUtc : null,
                LastOpenedAt: info.Exists ? info.LastWriteTimeUtc : null,
                EngineSchemaVersion: null,
                ProjectVersion: null,
                IsNew: false,
                IsCompatible: true,
                IsReadOnly: false);
        }
    }
}
