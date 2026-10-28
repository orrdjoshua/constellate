using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Constellate.App.Worlds
{
    public enum WorldTransitionDecision
    {
        Cancel,
        Save,
        Discard
    }

    internal static class WorldTransitionConfirmationDialog
    {
        public static Task<WorldTransitionDecision> ShowAsync(
            Window owner,
            string title,
            string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 440,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            var root = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 12
            };

            root.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            });

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };

            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (_, _) => dialog.Close(WorldTransitionDecision.Cancel);

            var discardButton = new Button { Content = "Discard" };
            discardButton.Click += (_, _) => dialog.Close(WorldTransitionDecision.Discard);

            var saveButton = new Button { Content = "Save" };
            saveButton.Click += (_, _) => dialog.Close(WorldTransitionDecision.Save);

            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(discardButton);
            buttonRow.Children.Add(saveButton);

            root.Children.Add(buttonRow);
            dialog.Content = root;

            return dialog.ShowDialog<WorldTransitionDecision>(owner);
        }
    }
}
