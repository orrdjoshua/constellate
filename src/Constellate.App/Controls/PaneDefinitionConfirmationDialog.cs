using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.App.Controls;

internal enum PaneDefinitionLoadWarningDecision
{
    Cancel,
    LoadWithoutSaving,
    SaveAsNewThenLoad,
    UpdateCurrentDefinitionThenLoad
}

internal enum PaneDefinitionSaveWarningDecision
{
    Cancel,
    SaveAsNewDefinition,
    UpdateCurrentDefinition
}

internal static class PaneDefinitionConfirmationDialog
{
    public static Task<PaneDefinitionLoadWarningDecision> ShowLoadWarningAsync(
        Window owner,
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor targetDefinition,
        bool canUpdateCurrentDefinition)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(targetDefinition);

        var dialog = CreateBaseDialogWindow("Load Pane Definition?");
        var body = new StackPanel
        {
            Spacing = 12
        };

        body.Children.Add(CreateTextBlock(
            $"This will erase the current local pane instance entirely and load an instance of '{targetDefinition.DisplayLabel}'.",
            "#FFE5B8",
            fontSize: 13,
            fontWeight: FontWeight.SemiBold));

        body.Children.Add(CreateTextBlock(
            "Choose whether to load immediately, cancel, or preserve current work first before replacing this pane instance.",
            "#D5E4F1"));

        body.Children.Add(CreateInfoCard(
            title: "Current Pane",
            text: $"Title: {pane.Title}{Environment.NewLine}Source: {pane.PaneWorkingCopySourceSummary}{Environment.NewLine}State: {pane.PaneWorkingCopyStatusSummary}"));

        body.Children.Add(CreateInfoCard(
            title: "Selected Definition",
            text: $"{targetDefinition.DisplayLabel}{(string.IsNullOrWhiteSpace(targetDefinition.Description) ? string.Empty : $"{Environment.NewLine}{targetDefinition.Description}")}"));

        var buttonRow = CreateButtonRow();

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Cancel",
            PaneDefinitionLoadWarningDecision.Cancel));

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Load Without Saving",
            PaneDefinitionLoadWarningDecision.LoadWithoutSaving,
            isPrimary: true));

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Save As New Then Load",
            PaneDefinitionLoadWarningDecision.SaveAsNewThenLoad));

        if (canUpdateCurrentDefinition)
        {
            buttonRow.Children.Add(CreateDialogButton(
                dialog,
                "Update Current Definition Then Load",
                PaneDefinitionLoadWarningDecision.UpdateCurrentDefinitionThenLoad));
        }

        dialog.Content = CreateDialogRoot(body, buttonRow);
        return dialog.ShowDialog<PaneDefinitionLoadWarningDecision>(owner);
    }

    public static Task<PaneDefinitionSaveWarningDecision> ShowOverwriteWarningAsync(
        Window owner,
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor existingDefinition)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(existingDefinition);

        var dialog = CreateBaseDialogWindow("Overwrite Existing Pane Definition?");
        var body = new StackPanel
        {
            Spacing = 12
        };

        var definitionKind = existingDefinition.IsSeeded
            ? "seeded"
            : "user-authored";

        body.Children.Add(CreateTextBlock(
            $"Saving under the current definition identity would overwrite the existing {definitionKind} pane definition '{existingDefinition.DisplayLabel}'.",
            "#FFE5B8",
            fontSize: 13,
            fontWeight: FontWeight.SemiBold));

        body.Children.Add(CreateTextBlock(
            "Choose whether to update the current definition in place, save a new definition instead, or cancel.",
            "#D5E4F1"));

        body.Children.Add(CreateInfoCard(
            title: "Current Pane State",
            text: $"Title: {pane.Title}{Environment.NewLine}Description: {(string.IsNullOrWhiteSpace(pane.EffectiveDescription) ? "(empty)" : pane.EffectiveDescription)}"));

        body.Children.Add(CreateInfoCard(
            title: "Existing Definition Target",
            text: $"{existingDefinition.DisplayLabel}{Environment.NewLine}Origin: {(existingDefinition.IsSeeded ? "Seeded" : "User-authored")}"));

        var buttonRow = CreateButtonRow();

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Cancel",
            PaneDefinitionSaveWarningDecision.Cancel));

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Save As New Definition",
            PaneDefinitionSaveWarningDecision.SaveAsNewDefinition));

        buttonRow.Children.Add(CreateDialogButton(
            dialog,
            "Update Existing Definition",
            PaneDefinitionSaveWarningDecision.UpdateCurrentDefinition,
            isPrimary: true));

        dialog.Content = CreateDialogRoot(body, buttonRow);
        return dialog.ShowDialog<PaneDefinitionSaveWarningDecision>(owner);
    }

    private static Window CreateBaseDialogWindow(string title)
    {
        return new Window
        {
            Title = title,
            Width = 640,
            CanResize = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = CreateBrush("#101722"),
            Foreground = CreateBrush("#E5EDF7")
        };
    }

    private static Control CreateDialogRoot(Control body, Control buttonRow)
    {
        var stack = new StackPanel
        {
            Spacing = 16
        };
        stack.Children.Add(body);
        stack.Children.Add(buttonRow);

        return new Border
        {
            Padding = new Thickness(20),
            Background = CreateBrush("#101722"),
            Child = stack
        };
    }

    private static StackPanel CreateButtonRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }

    private static Button CreateDialogButton<TResult>(
        Window dialog,
        string text,
        TResult result,
        bool isPrimary = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 130,
            Background = isPrimary ? CreateBrush("#2D5A7A") : CreateBrush("#223142"),
            Foreground = CreateBrush("#E5EDF7")
        };

        button.Click += (_, _) => dialog.Close(result);
        return button;
    }

    private static Border CreateInfoCard(string title, string text)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        stack.Children.Add(CreateTextBlock(
            title,
            "#9DD1F0",
            fontSize: 11,
            fontWeight: FontWeight.SemiBold));

        stack.Children.Add(CreateTextBlock(
            text,
            "#D5E4F1"));

        return new Border
        {
            Background = CreateBrush("#152230"),
            BorderBrush = CreateBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = stack
        };
    }

    private static TextBlock CreateTextBlock(
        string text,
        string foregroundHex,
        double fontSize = 12,
        FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeight.Normal,
            Foreground = CreateBrush(foregroundHex)
        };
    }

    private static IBrush CreateBrush(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }
}
