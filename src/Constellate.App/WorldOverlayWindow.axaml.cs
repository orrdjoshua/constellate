using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Constellate.App.ViewModels.Worlds;
using Constellate.App.Worlds;

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

        private async void SwitchSelected_OnClick(object? sender, RoutedEventArgs e)
        {
            var saveCurrentWorld = await ResolveSaveCurrentWorldAsync(
                _viewModel.ResolveSwitchBehavior(),
                "Switch World",
                "Save changes to the current world before switching?");
            if (saveCurrentWorld is null)
            {
                return;
            }

            _ = _viewModel.TrySwitchSelectedWorld(saveCurrentWorld.Value, out _);
        }

        private async void ExitToWorldSelection_OnClick(object? sender, RoutedEventArgs e)
        {
            var saveCurrentWorld = await ResolveSaveCurrentWorldAsync(
                _viewModel.ResolveExitBehavior(),
                "Exit to World Selection",
                "Save changes to the current world before returning to world selection?");
            if (saveCurrentWorld is null)
            {
                return;
            }

            if (!WorldSessionCoordinator.TryExitToWorldSelection(saveCurrentWorld.Value, out _))
            {
                return;
            }

            Close();
        }

        private async Task<bool?> ResolveSaveCurrentWorldAsync(
            WorldSaveBehavior behavior,
            string title,
            string message)
        {
            if (behavior == WorldSaveBehavior.Save)
            {
                return true;
            }

            if (behavior == WorldSaveBehavior.Discard)
            {
                return false;
            }

            var decision = await WorldTransitionConfirmationDialog.ShowAsync(this, title, message);
            return decision switch
            {
                WorldTransitionDecision.Save => true,
                WorldTransitionDecision.Discard => false,
                _ => (bool?)null
            };
        }

        private void Close_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
