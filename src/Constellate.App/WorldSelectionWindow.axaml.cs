using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Constellate.App.ViewModels.Worlds;
using Constellate.App.Worlds;

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

        private async void AddRoot_OnClick(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Add World Database Folder",
                AllowMultiple = false
            });

            var folder = folders?.FirstOrDefault();
            if (folder is null)
            {
                return;
            }

            try
            {
                _worldLibraryService.AddRoot(folder.Path.LocalPath, folder.Name);
                LoadWorlds();
            }
            catch (Exception)
            {
                await ShowOpenWorldErrorAsync("The selected folder could not be added as a world library root.");
            }
        }

        private void WorldList_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            OpenSelectedWorld();
        }

        private void OpenSelected_OnClick(object? sender, RoutedEventArgs e)
        {
            OpenSelectedWorld();
        }

        private async void OpenSelectedWorld()
        {
            if (_viewModel.SelectedWorld is not { } selected)
            {
                return;
            }

            if (!WorldSessionCoordinator.TryOpenWorld(selected, out var error))
            {
                await ShowOpenWorldErrorAsync(
                    string.IsNullOrWhiteSpace(error)
                        ? "The selected world could not be opened."
                        : error);
                return;
            }

            Close();
        }

        private async System.Threading.Tasks.Task ShowOpenWorldErrorAsync(string message)
        {
            var dialog = new Window
            {
                Title = "Open World",
                Width = 470,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            var root = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 12
            };

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            var closeButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (_, _) => dialog.Close();

            root.Children.Add(messageBlock);
            root.Children.Add(closeButton);
            dialog.Content = root;

            await dialog.ShowDialog(this);
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
