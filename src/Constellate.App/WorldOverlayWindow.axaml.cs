using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Constellate.App.ViewModels.Worlds;

namespace Constellate.App
{
    public partial class WorldOverlayWindow : Window
    {
        private readonly WorldOverlayViewModel _viewModel = new();

        public WorldOverlayWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            _viewModel.Load();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SaveWorldNow_OnClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.SaveWorldNow();
        }

        private void ApplySettings_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(AutoSaveIntervalTextBox.Text, out var interval))
            {
                return;
            }

            _viewModel.AutoSaveIntervalSeconds = interval;
            _viewModel.ApplySettings();
        }

        private void SwitchSelected_OnClick(object? sender, RoutedEventArgs e)
        {
            _ = _viewModel.TrySwitchSelectedWorld(out _);
        }

        private void ExitToWorldSelection_OnClick(object? sender, RoutedEventArgs e)
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime is not null)
            {
                lifetime.MainWindow = new WorldSelectionWindow();
                lifetime.MainWindow.Show();
            }

            Close();
        }

        private void Close_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
