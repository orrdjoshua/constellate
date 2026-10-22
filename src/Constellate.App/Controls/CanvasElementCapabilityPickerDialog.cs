using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Templates;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Messaging;

namespace Constellate.App.Controls;

internal static class CanvasElementCapabilityPickerDialog
{
    public static Task<string?> ShowAsync(
        Window owner,
        ChildPaneDescriptor pane,
        ChildPaneCanvasElementInstance element)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(element);

        var hostClasses = element.CompatibleHostClasses ?? Array.Empty<PaneCapabilityHostClass>();
        var capabilities = EngineServices.ListPaneCapabilities();

        var candidates = hostClasses.Count == 0
            ? capabilities
            : capabilities
                .Where(capability => capability.EffectiveCompatibleHostClasses.Any(hostClasses.Contains))
                .ToArray();

        if (candidates.Count == 0)
        {
            // Nothing compatible to show; bail out quietly.
            return Task.FromResult<string?>(null);
        }

        var dialog = new Window
        {
            Title = "Choose Capability",
            Width = 520,
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
            $"Select a capability to bind to '{element.DisplayLabel}' in pane '{pane.Title}'.",
            "#D5E4F1",
            fontSize: 12));

        var listBox = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Height = 260
        };

        listBox.ItemsSource = candidates;
        listBox.ItemTemplate = new FuncDataTemplate<PaneCapabilityDescriptor>((capability, _) =>
            new TextBlock
            {
                Text = capability.DisplayLabel,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = CreateBrush("#E5EDF7"),
                FontSize = 12
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

        var bindButton = new Button
        {
            Content = "Bind Capability",
            MinWidth = 130,
            Background = CreateBrush("#2D5A7A"),
            Foreground = CreateBrush("#E5EDF7")
        };
        bindButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is PaneCapabilityDescriptor selected)
            {
                dialog.Close(selected.CapabilityId);
            }
            else
            {
                dialog.Close(null);
            }
        };

        listBox.DoubleTapped += (_, _) =>
        {
            if (listBox.SelectedItem is PaneCapabilityDescriptor selected)
            {
                dialog.Close(selected.CapabilityId);
            }
        };

        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(bindButton);

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

        return dialog.ShowDialog<string?>(owner);
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
