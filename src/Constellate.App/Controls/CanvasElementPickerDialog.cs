using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Messaging;

namespace Constellate.App.Controls;

internal static class CanvasElementPickerDialog
{
    public static Task<PaneElementKind?> ShowAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var entries = EngineServices.ListPaneAuthoringElementEntries();
        if (entries is null || entries.Count == 0)
        {
            return Task.FromResult<PaneElementKind?>(null);
        }

        var dialog = new Window
        {
            Title = "Choose Element",
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = CreateBrush("#101722"),
            Foreground = CreateBrush("#E5EDF7")
        };

        var body = new StackPanel
        {
            Spacing = 10
        };

        body.Children.Add(CreateTextBlock(
            "Select an element to add to the authored canvas.",
            "#D5E4F1",
            fontSize: 12));

        var listBox = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Height = 260,
            ItemsSource = entries
        };

        listBox.ItemTemplate = new FuncDataTemplate<PaneAuthoringElementCatalogEntry>((entry, _) =>
        {
            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(new TextBlock
            {
                Text = $"{entry.DisplayLabel}",
                TextWrapping = TextWrapping.NoWrap,
                Foreground = CreateBrush("#E5EDF7"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = entry.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = CreateBrush("#BFD3E4"),
                FontSize = 10,
                MaxWidth = 480
            });
            return stack;
        });

        body.Children.Add(new Border
        {
            Background = CreateBrush("#152230"),
            BorderBrush = CreateBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6),
            Child = listBox
        });

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 100,
            Background = CreateBrush("#223142"),
            Foreground = CreateBrush("#E5EDF7")
        };
        cancelButton.Click += (_, _) => dialog.Close(null);

        var chooseButton = new Button
        {
            Content = "Add Element",
            MinWidth = 130,
            Background = CreateBrush("#2D5A7A"),
            Foreground = CreateBrush("#E5EDF7")
        };
        chooseButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is PaneAuthoringElementCatalogEntry selected)
            {
                dialog.Close(selected.ElementKind);
            }
            else
            {
                dialog.Close(null);
            }
        };

        listBox.DoubleTapped += (_, _) =>
        {
            if (listBox.SelectedItem is PaneAuthoringElementCatalogEntry selected)
            {
                dialog.Close(selected.ElementKind);
            }
        };

        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(chooseButton);

        var root = new StackPanel
        {
            Spacing = 16
        };
        root.Children.Add(body);
        root.Children.Add(buttonRow);

        dialog.Content = new Border
        {
            Padding = new Thickness(16),
            Background = CreateBrush("#101722"),
            Child = root
        };

        return dialog.ShowDialog<PaneElementKind?>(owner);
    }

    private static TextBlock CreateTextBlock(string text, string foregroundHex, double fontSize = 12)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            Foreground = CreateBrush(foregroundHex)
        };
    }

    private static IBrush CreateBrush(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }
}
