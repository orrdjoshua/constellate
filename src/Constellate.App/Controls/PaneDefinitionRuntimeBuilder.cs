using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.App.Controls;

internal static class PaneDefinitionRuntimeBuilder
{
    private static readonly IReadOnlyDictionary<string, PaneCapabilityDescriptor> CapabilityIndex =
        new SeededPaneCatalog()
            .GetCapabilityDescriptors()
            .ToDictionary(
                capability => capability.CapabilityId,
                capability => capability,
                StringComparer.Ordinal);

    public static Control Build(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        MainWindowViewModel? windowViewModel)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(definition);

        if (pane.IsAuthorMode)
        {
            return BuildAuthorModeCanvasSurface(pane, definition, windowViewModel);
        }

        var contentStack = new StackPanel
        {
            Spacing = 8
        };

        foreach (var element in definition.Elements)
        {
            contentStack.Children.Add(BuildElement(pane, definition, element, windowViewModel));
        }

        return new Border
        {
            Background = ParseBrush("#0F1B25"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = contentStack
        };
    }

    private static Control BuildAuthorModeCanvasSurface(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        MainWindowViewModel? windowViewModel)
    {
        var surface = ChildPaneCanvasAuthoringProjector.Project(definition, pane);
        return BuildAuthorModeCanvasSurfaceCore(pane, surface, windowViewModel);
    }

    internal static Control BuildBlankAuthorModeCanvasSurface(
        ChildPaneDescriptor pane,
        MainWindowViewModel? windowViewModel)
    {
        var surface = ChildPaneCanvasAuthoringProjector.Project(null, pane);
        return BuildAuthorModeCanvasSurfaceCore(pane, surface, windowViewModel);
    }

    private static Control BuildAuthorModeCanvasSurfaceCore(
        ChildPaneDescriptor pane,
        ChildPaneCanvasSurfaceModel surface,
        MainWindowViewModel? windowViewModel)
    {
        var canvas = new Canvas
        {
            Width = surface.Width,
            Height = surface.Height
        };

        if (windowViewModel is not null)
        {
            canvas.PointerPressed += (_, e) =>
            {
                if (e.Handled)
                {
                    return;
                }

                if (windowViewModel.TryClearChildPaneCanvasElementSelection(pane.Id))
                {
                    e.Handled = true;
                }
            };
        }

        foreach (var element in surface.Elements.OrderBy(item => item.ZIndex))
        {
            var previewCard = BuildAuthorModeCanvasCard(pane, element, windowViewModel);
            Canvas.SetLeft(previewCard, element.X);
            Canvas.SetTop(previewCard, element.Y);
            canvas.Children.Add(previewCard);
        }

        var canvasHost = new Grid
        {
            Width = surface.Width,
            Height = surface.Height
        };
        canvasHost.Children.Add(canvas);

        if (surface.Elements.Count == 0)
        {
            canvasHost.Children.Add(BuildEmptyAuthorModeCanvasPrompt(pane));
        }

        var stack = new StackPanel
        {
            Spacing = 8
        };

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneAuthorModeBadgeLabel,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = ParseBrush("#8FD2FF")
        });

        stack.Children.Add(new TextBlock
        {
            Text = surface.Summary,
            FontSize = 10,
            Foreground = ParseBrush("#BFD3E4"),
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(BuildAuthorModeQuickInsertBar(pane, windowViewModel));

        stack.Children.Add(new Border
        {
            Background = ParseBrush("#0C141C"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = canvasHost
        });

        return new Border
        {
            Background = ParseBrush("#0F1B25"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = stack
        };
    }

    private static Control BuildAuthorModeQuickInsertBar(
        ChildPaneDescriptor pane,
        MainWindowViewModel? windowViewModel)
    {
        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        foreach (var entry in PaneAuthoringCatalog.GetElementEntries())
        {
            var button = new Button
            {
                Content = $"+ {entry.DisplayLabel}",
                IsEnabled = windowViewModel is not null
            };

            ToolTip.SetTip(button, entry.Description);
            button.Click += (_, _) => windowViewModel?.TryAddChildPaneLocalCanvasElement(
                pane.Id,
                entry.ElementKind);
            wrap.Children.Add(button);
        }

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = wrap
        };
    }

    private static Control BuildEmptyAuthorModeCanvasPrompt(ChildPaneDescriptor pane)
    {
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Blank Authored Canvas",
            Foreground = ParseBrush("#D6ECFA"),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneCanvasEmptyStateSummary,
            Foreground = ParseBrush("#BFD3E4"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 320
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Use the quick-add strip above or the author-mode body context menu to place the first element.",
            Foreground = ParseBrush("#9DB3C5"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 320
        });

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack
        };
    }

    private static Control BuildAuthorModeCanvasCard(
        ChildPaneDescriptor pane,
        ChildPaneCanvasElementInstance element,
        MainWindowViewModel? windowViewModel)
    {
        var content = new StackPanel
        {
            Spacing = 2
        };

        content.Children.Add(new TextBlock
        {
            Text = element.DisplayLabel,
            Foreground = ParseBrush("#F3F8FD"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(new TextBlock
        {
            Text = element.ElementKind.ToString(),
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        content.Children.Add(new TextBlock
        {
            Text = element.BindingLabel,
            Foreground = ParseBrush("#BFD3E4"),
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = Math.Max(18, element.Height - 48)
        });

        var isSelected = string.Equals(
            pane.SelectedCanvasElementInstanceId,
            element.InstanceId,
            StringComparison.Ordinal);

        var layoutRoot = new Grid();
        layoutRoot.Children.Add(content);

        var resizeGrip = new Border
        {
            Width = 12,
            Height = 12,
            Background = ParseBrush(isSelected ? "#7DD3FC" : "#355066"),
            BorderBrush = ParseBrush("#0F1B25"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = isSelected,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner)
        };
        layoutRoot.Children.Add(resizeGrip);

        var border = new Border
        {
            Width = element.Width,
            Height = element.Height,
            Background = ParseBrush(isSelected ? "#20364A" : "#162535"),
            BorderBrush = ParseBrush(isSelected ? "#7DD3FC" : "#4A6378"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = layoutRoot,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        if (windowViewModel is not null)
        {
            Point? dragOrigin = null;
            double dragStartLeft = element.X;
            double dragStartTop = element.Y;
            var dragMoved = false;
            Point? resizeOrigin = null;
            double resizeStartWidth = element.Width;
            double resizeStartHeight = element.Height;
            var resizeMoved = false;

            ToolTip.SetTip(border, $"Select {element.DisplayLabel} and drag to move it");
            border.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                windowViewModel.TrySelectChildPaneCanvasElement(pane.Id, element.InstanceId);

                if (border.Parent is not Canvas canvas)
                {
                    e.Handled = true;
                    return;
                }

                dragOrigin = e.GetPosition(canvas);
                dragStartLeft = Canvas.GetLeft(border);
                dragStartTop = Canvas.GetTop(border);

                if (double.IsNaN(dragStartLeft))
                {
                    dragStartLeft = element.X;
                }

                if (double.IsNaN(dragStartTop))
                {
                    dragStartTop = element.Y;
                }

                dragMoved = false;
                e.Pointer.Capture(border);
                e.Handled = true;
            };

            border.PointerMoved += (_, e) =>
            {
                if (dragOrigin is null ||
                    border.Parent is not Canvas canvas ||
                    !ReferenceEquals(e.Pointer.Captured, border))
                {
                    return;
                }

                var currentPosition = e.GetPosition(canvas);
                var offsetX = currentPosition.X - dragOrigin.Value.X;
                var offsetY = currentPosition.Y - dragOrigin.Value.Y;

                if (!dragMoved &&
                    Math.Abs(offsetX) < 0.5 &&
                    Math.Abs(offsetY) < 0.5)
                {
                    return;
                }

                dragMoved = true;
                Canvas.SetLeft(border, Math.Max(0, dragStartLeft + offsetX));
                Canvas.SetTop(border, Math.Max(0, dragStartTop + offsetY));
                e.Handled = true;
            };

            border.PointerReleased += (_, e) =>
            {
                if (dragOrigin is null || !ReferenceEquals(e.Pointer.Captured, border))
                {
                    return;
                }

                e.Pointer.Capture(null);

                var finalLeft = Canvas.GetLeft(border);
                var finalTop = Canvas.GetTop(border);

                if (double.IsNaN(finalLeft))
                {
                    finalLeft = dragStartLeft;
                }

                if (double.IsNaN(finalTop))
                {
                    finalTop = dragStartTop;
                }

                if (dragMoved)
                {
                    windowViewModel.TrySetChildPaneCanvasElementPreviewPlacement(
                        pane.Id,
                        element.InstanceId,
                        finalLeft,
                        finalTop);
                    e.Handled = true;
                }

                dragOrigin = null;
                dragMoved = false;
            };

            border.PointerCaptureLost += (_, _) =>
            {
                dragOrigin = null;
                dragMoved = false;
            };

            ToolTip.SetTip(resizeGrip, $"Resize {element.DisplayLabel}");
            resizeGrip.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(resizeGrip).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                windowViewModel.TrySelectChildPaneCanvasElement(pane.Id, element.InstanceId);

                if (border.Parent is not Canvas canvas)
                {
                    e.Handled = true;
                    return;
                }

                resizeOrigin = e.GetPosition(canvas);
                resizeStartWidth = border.Width;
                resizeStartHeight = border.Height;
                resizeMoved = false;
                e.Pointer.Capture(resizeGrip);
                e.Handled = true;
            };

            resizeGrip.PointerMoved += (_, e) =>
            {
                if (resizeOrigin is null ||
                    border.Parent is not Canvas canvas ||
                    !ReferenceEquals(e.Pointer.Captured, resizeGrip))
                {
                    return;
                }

                var currentPosition = e.GetPosition(canvas);
                var offsetX = currentPosition.X - resizeOrigin.Value.X;
                var offsetY = currentPosition.Y - resizeOrigin.Value.Y;

                if (!resizeMoved &&
                    Math.Abs(offsetX) < 0.5 &&
                    Math.Abs(offsetY) < 0.5)
                {
                    return;
                }

                resizeMoved = true;
                border.Width = Math.Max(120, resizeStartWidth + offsetX);
                border.Height = Math.Max(64, resizeStartHeight + offsetY);
                e.Handled = true;
            };

            resizeGrip.PointerReleased += (_, e) =>
            {
                if (resizeOrigin is null || !ReferenceEquals(e.Pointer.Captured, resizeGrip))
                {
                    return;
                }

                e.Pointer.Capture(null);

                var finalWidth = Math.Max(120, border.Width);
                var finalHeight = Math.Max(64, border.Height);
                var currentLeft = Canvas.GetLeft(border);
                var currentTop = Canvas.GetTop(border);

                if (double.IsNaN(currentLeft))
                {
                    currentLeft = element.X;
                }

                if (double.IsNaN(currentTop))
                {
                    currentTop = element.Y;
                }

                if (resizeMoved)
                {
                    windowViewModel.TrySetChildPaneCanvasElementPreviewSize(
                        pane.Id,
                        element.InstanceId,
                        finalWidth,
                        finalHeight,
                        currentLeft,
                        currentTop);
                    e.Handled = true;
                }

                resizeOrigin = null;
                resizeMoved = false;
            };

            resizeGrip.PointerCaptureLost += (_, _) =>
            {
                resizeOrigin = null;
                resizeMoved = false;
            };
        }

        return border;
    }

    private static Control BuildElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        if (element.Binding is { TargetKind: PaneElementBindingTargetKind.Capability } &&
            !CanCapabilityBindToElement(element, out var incompatibleCapability, out var supportedHostClasses))
        {
            return BuildIncompatibleCapabilityCard(element, incompatibleCapability, supportedHostClasses);
        }

        return element.ElementKind switch
        {
            PaneElementKind.DefinitionHeader => BuildDefinitionHeader(pane, definition),
            PaneElementKind.LabelValueField => BuildLabelValueField(pane, element, windowViewModel),
            PaneElementKind.TextBlock => BuildTextBlock(pane, element, windowViewModel),
            PaneElementKind.Button => BuildButton(pane, element, windowViewModel),
            PaneElementKind.FilterBar => BuildFilterBar(pane, element, windowViewModel),
            PaneElementKind.CommandBar => BuildCommandBar(pane, definition, element, windowViewModel),
            PaneElementKind.StatusBadge => BuildStatusBadge(pane, element, windowViewModel),
            PaneElementKind.CommandBrowser or
            PaneElementKind.CapabilityBrowser or
            PaneElementKind.ListBrowser or
            PaneElementKind.ResourceBrowser or
            PaneElementKind.TreeBrowser or
            PaneElementKind.TableBrowser => BuildCatalogListSurface(
                pane,
                element,
                ResolveCatalogSurfaceTitle(element),
                ResolveCatalogSurfaceLines(element, windowViewModel)),
            PaneElementKind.RuntimeActivityPanel or
            PaneElementKind.EventFeed or
            PaneElementKind.StreamConsole or
            PaneElementKind.TaskMonitor => BuildCatalogListSurface(
                pane,
                element,
                "Runtime Activity",
                GetRuntimeActivityLines(windowViewModel)),
            PaneElementKind.ArchiveBrowser or
            PaneElementKind.MetricsReadout or
            PaneElementKind.ProjectionStatusView or
            PaneElementKind.TextEditor or
            PaneElementKind.PropertyEditor => BuildBoundTextSurface(pane, element, windowViewModel),
            PaneElementKind.Section => BuildSectionElement(pane, definition, element, windowViewModel),
            PaneElementKind.InspectorGroup => BuildInspectorGroupElement(pane, definition, element, windowViewModel),
            PaneElementKind.TabsHost => BuildTabsHostElement(pane, definition, element, windowViewModel),
            PaneElementKind.SplitHost => BuildSplitHostElement(pane, definition, element, windowViewModel),
            _ => BuildUnsupportedElementCard(element)
        };
    }

    private static string ResolveCatalogSurfaceTitle(PaneElementDescriptor element)
    {
        return element.Binding?.TargetRef switch
        {
            "browser.command_catalog" => "Command / Capability Catalog",
            "browser.pane_catalog" => "Pane Catalog",
            "browser.workspace_catalog" => "Workspace Catalog",
            "engine.state.group_details" => "Group Details",
            "engine.state.panel_details" => "Panel Details",
            _ => "Catalog Surface"
        };
    }

    private static IReadOnlyList<string> ResolveCatalogSurfaceLines(
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        return element.Binding?.TargetRef switch
        {
            "browser.command_catalog" => GetCommandCatalogLines(),
            "browser.pane_catalog" => GetPaneCatalogLines(windowViewModel),
            "browser.workspace_catalog" => GetWorkspaceCatalogLines(windowViewModel),
            "engine.state.group_details" => ResolveStateSelectorText("engine.state.group_details", windowViewModel)
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .ToArray(),
            "engine.state.panel_details" => ResolveStateSelectorText("engine.state.panel_details", windowViewModel)
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .ToArray(),
            _ => GetCommandCatalogLines()
        };
    }

    private static Control BuildDefinitionHeader(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition)
    {
        var effectiveLabel = string.IsNullOrWhiteSpace(pane.DefinitionLabel)
            ? definition.DisplayLabel
            : pane.DefinitionLabel!;

        var description = string.IsNullOrWhiteSpace(definition.Description)
            ? "Realized through the generic pane-definition runtime path."
            : definition.Description!;

        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = effectiveLabel,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = ParseBrush("#F3F8FD")
        });

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneAuthorModeBadgeLabel,
            FontSize = 10,
            Foreground = pane.IsAuthorMode
                ? ParseBrush("#8FD2FF")
                : ParseBrush("#BFD3E4")
        });

        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ParseBrush("#BFD3E4")
        });

        return new Border
        {
            Background = ParseBrush("#142636"),
            BorderBrush = ParseBrush("#486983"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildLabelValueField(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = element.DisplayLabel,
            FontSize = 10,
            Foreground = ParseBrush("#9DD1F0")
        });

        stack.Children.Add(CreateBoundOrResolvedTextBlock(
            pane,
            element,
            windowViewModel,
            12,
            ParseBrush("#F3F8FD")));

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#2E475B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildTextBlock(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        return CreateBoundOrResolvedTextBlock(
            pane,
            element,
            windowViewModel,
            11,
            ParseBrush("#D9EAF7"));
    }

    private static Control BuildButton(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var button = new Button
        {
            Content = element.DisplayLabel,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (pane.IsAuthorMode)
        {
            button.IsEnabled = false;
            ToolTip.SetTip(button, $"Author-mode preview · {element.Binding?.TargetRef ?? element.DisplayLabel}");
            return button;
        }

        var command = ResolveCommand(element.Binding, windowViewModel);
        if (command is null)
        {
            button.IsEnabled = false;
            ToolTip.SetTip(button, $"Command binding not available for '{element.Binding?.TargetRef ?? element.DisplayLabel}'.");
            return button;
        }

        button.Command = command;

        var commandParameter = ResolveCommandParameter(pane, element.Binding);
        if (commandParameter is not null)
        {
            button.CommandParameter = commandParameter;
        }

        ToolTip.SetTip(button, element.Binding?.TargetRef ?? element.DisplayLabel);
        return button;
    }

    private static Control BuildCommandBar(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var actionHost = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        if (element.Children is { Count: > 0 })
        {
            foreach (var child in element.Children)
            {
                actionHost.Children.Add(BuildElement(pane, definition, child, windowViewModel));
            }
        }
        else if (element.Binding is not null)
        {
            actionHost.Children.Add(BuildButton(pane, element, windowViewModel));
        }
        else
        {
            actionHost.Children.Add(new TextBlock
            {
                Text = "No command items configured.",
                Foreground = ParseBrush("#9DB3C5"),
                FontSize = 10
            });
        }

        var stack = new StackPanel
        {
            Spacing = 6
        };

        if (!string.IsNullOrWhiteSpace(element.DisplayLabel))
        {
            stack.Children.Add(new TextBlock
            {
                Text = element.DisplayLabel,
                Foreground = ParseBrush("#9DD1F0"),
                FontSize = 10
            });
        }

        stack.Children.Add(actionHost);

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#2E475B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildStatusBadge(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var textBlock = new TextBlock
        {
            Foreground = ParseBrush("#D8F0FF"),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold
        };

        var stringFormat = string.IsNullOrWhiteSpace(element.DisplayLabel)
            ? null
            : $"{element.DisplayLabel}: {{0}}";

        if (pane.IsAuthorMode)
        {
            textBlock.Text = string.IsNullOrWhiteSpace(element.DisplayLabel)
                ? "Status Badge"
                : element.DisplayLabel;
        }
        else
        {
            var binding = CreateResolvedTextBinding(element, pane, windowViewModel, stringFormat);
            if (binding is not null)
            {
                textBlock.Bind(TextBlock.TextProperty, binding);
            }
            else
            {
                var resolvedValue = ResolveBoundText(pane, element, windowViewModel);
                var singleLineValue = string.IsNullOrWhiteSpace(resolvedValue)
                    ? element.DisplayLabel
                    : resolvedValue.Split(
                            [Environment.NewLine],
                            StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault() ?? resolvedValue;

                textBlock.Text = string.IsNullOrWhiteSpace(element.DisplayLabel)
                    ? singleLineValue
                    : $"{element.DisplayLabel}: {singleLineValue}";
            }
        }

        return new Border
        {
            Background = ParseBrush("#25435D"),
            BorderBrush = ParseBrush("#3A6E8E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 4),
            Child = textBlock
        };
    }

    private static Control BuildFilterBar(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var placeholder = TryGetBehaviorSetting(element, "placeholder") ??
            (string.IsNullOrWhiteSpace(element.DisplayLabel)
                ? "Filter / search surface"
                : element.DisplayLabel);

        var stack = new StackPanel
        {
            Spacing = 6
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Filter Bar",
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        stack.Children.Add(new TextBox
        {
            PlaceholderText = placeholder,
            Width = 220,
            IsEnabled = !pane.IsAuthorMode
        });

        if (element.Binding is not null)
        {
            stack.Children.Add(CreateBoundOrResolvedTextBlock(
                pane,
                element,
                windowViewModel,
                10,
                ParseBrush("#BFD3E4")));
        }

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#2E475B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildSectionElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var content = BuildContainerChildrenStack(
            pane,
            definition,
            element,
            windowViewModel,
            "Section has no child elements yet.");

        return BuildStructuredContainerCard(
            element.DisplayLabel,
            TryGetBehaviorSetting(element, "description"),
            ParseBrush("#13212C"),
            ParseBrush("#2E475B"),
            ParseBrush("#D6ECFA"),
            content);
    }

    private static Control BuildInspectorGroupElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var content = BuildContainerChildrenStack(
            pane,
            definition,
            element,
            windowViewModel,
            "Inspector group has no child elements yet.");

        return BuildStructuredContainerCard(
            element.DisplayLabel,
            TryGetBehaviorSetting(element, "description"),
            ParseBrush("#142636"),
            ParseBrush("#486983"),
            ParseBrush("#E4F3FD"),
            content);
    }

    private static Control BuildTabsHostElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        if (element.Children is not { Count: > 0 })
        {
            return BuildStructuredContainerCard(
                element.DisplayLabel,
                TryGetBehaviorSetting(element, "description"),
                ParseBrush("#13212C"),
                ParseBrush("#2E475B"),
                ParseBrush("#D6ECFA"),
                new TextBlock
                {
                    Text = "Tabs host has no child tabs yet.",
                    Foreground = ParseBrush("#9DB3C5"),
                    FontSize = 10
                });
        }

        var tabControl = new TabControl();
        foreach (var child in element.Children)
        {
            tabControl.Items.Add(new TabItem
            {
                Header = string.IsNullOrWhiteSpace(child.DisplayLabel)
                    ? child.ElementKind.ToString()
                    : child.DisplayLabel,
                Content = BuildElement(pane, definition, child, windowViewModel)
            });
        }

        return BuildStructuredContainerCard(
            element.DisplayLabel,
            TryGetBehaviorSetting(element, "description"),
            ParseBrush("#13212C"),
            ParseBrush("#2E475B"),
            ParseBrush("#D6ECFA"),
            tabControl);
    }

    private static Control BuildSplitHostElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        if (element.Children is not { Count: > 0 })
        {
            return BuildStructuredContainerCard(
                element.DisplayLabel,
                TryGetBehaviorSetting(element, "description"),
                ParseBrush("#13212C"),
                ParseBrush("#2E475B"),
                ParseBrush("#D6ECFA"),
                new TextBlock
                {
                    Text = "Split host has no child regions yet.",
                    Foreground = ParseBrush("#9DB3C5"),
                    FontSize = 10
                });
        }

        var isVertical = string.Equals(
            TryGetBehaviorSetting(element, "orientation"),
            "vertical",
            StringComparison.OrdinalIgnoreCase);

        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        if (isVertical)
        {
            grid.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", element.Children.Count)));
        }
        else
        {
            grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", element.Children.Count)));
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            var region = new Border
            {
                Background = ParseBrush("#0F1B25"),
                BorderBrush = ParseBrush("#22384A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6),
                Child = BuildElement(pane, definition, element.Children[i], windowViewModel)
            };

            if (isVertical)
            {
                Grid.SetRow(region, i);
            }
            else
            {
                Grid.SetColumn(region, i);
            }

            grid.Children.Add(region);
        }

        return BuildStructuredContainerCard(
            element.DisplayLabel,
            TryGetBehaviorSetting(element, "description"),
            ParseBrush("#13212C"),
            ParseBrush("#2E475B"),
            ParseBrush("#D6ECFA"),
            grid);
    }

    private static Control BuildStructuredContainerCard(
        string title,
        string? description,
        IBrush background,
        IBrush borderBrush,
        IBrush titleBrush,
        Control content)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        if (!string.IsNullOrWhiteSpace(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = titleBrush,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
            });
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = ParseBrush("#BFD3E4"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
        }

        stack.Children.Add(content);

        return new Border
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildContainerChildrenStack(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel,
        string emptyText)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        if (element.Children is { Count: > 0 })
        {
            foreach (var child in element.Children)
            {
                stack.Children.Add(BuildElement(pane, definition, child, windowViewModel));
            }
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = emptyText,
                Foreground = ParseBrush("#9DB3C5"),
                FontSize = 10
            });
        }

        return stack;
    }

    private static bool CanCapabilityBindToElement(
        PaneElementDescriptor element,
        out PaneCapabilityDescriptor? capability,
        out IReadOnlyList<PaneCapabilityHostClass> supportedHostClasses)
    {
        capability = null;
        supportedHostClasses = ResolveElementCompatibleHostClasses(element);

        if (element.Binding is not { TargetKind: PaneElementBindingTargetKind.Capability } ||
            string.IsNullOrWhiteSpace(element.Binding.TargetRef) ||
            !CapabilityIndex.TryGetValue(element.Binding.TargetRef, out var resolvedCapability))
        {
            return true;
        }

        capability = resolvedCapability;
        var capabilityHostClasses = resolvedCapability.EffectiveCompatibleHostClasses;
        if (supportedHostClasses.Count == 0 || capabilityHostClasses.Count == 0)
        {
            return true;
        }

        return supportedHostClasses.Any(capabilityHostClasses.Contains);
    }

    private static IReadOnlyList<PaneCapabilityHostClass> ResolveElementCompatibleHostClasses(
        PaneElementDescriptor element)
    {
        if (element.CompatibleCapabilityHostClasses is { Count: > 0 })
        {
            return element.CompatibleCapabilityHostClasses;
        }

        return element.ElementKind switch
        {
            PaneElementKind.Button => [PaneCapabilityHostClass.InvocationHost],
            PaneElementKind.CommandBar => [PaneCapabilityHostClass.CommandBarHost, PaneCapabilityHostClass.InvocationHost],
            PaneElementKind.TextBlock => [PaneCapabilityHostClass.TextDisplayHost],
            PaneElementKind.LabelValueField => [PaneCapabilityHostClass.TextDisplayHost, PaneCapabilityHostClass.InspectorHost],
            PaneElementKind.TextEditor => [PaneCapabilityHostClass.TextInputHost, PaneCapabilityHostClass.TextDisplayHost],
            PaneElementKind.PropertyEditor or PaneElementKind.ProjectionStatusView or PaneElementKind.Section or PaneElementKind.InspectorGroup =>
                [PaneCapabilityHostClass.InspectorHost, PaneCapabilityHostClass.TextDisplayHost],
            PaneElementKind.ListBrowser or PaneElementKind.ResourceBrowser or PaneElementKind.CommandBrowser or PaneElementKind.CapabilityBrowser =>
                [PaneCapabilityHostClass.CollectionBrowserHost],
            PaneElementKind.TreeBrowser => [PaneCapabilityHostClass.TreeBrowserHost],
            PaneElementKind.TableBrowser => [PaneCapabilityHostClass.TableBrowserHost],
            PaneElementKind.RuntimeActivityPanel or PaneElementKind.EventFeed or PaneElementKind.StreamConsole or PaneElementKind.TaskMonitor =>
                [PaneCapabilityHostClass.StreamViewerHost],
            PaneElementKind.ArchiveBrowser => [PaneCapabilityHostClass.ArchiveViewerHost],
            PaneElementKind.StatusBadge => [PaneCapabilityHostClass.StatusBadgeHost],
            PaneElementKind.MetricsReadout => [PaneCapabilityHostClass.MetricsHost],
            _ => Array.Empty<PaneCapabilityHostClass>()
        };
    }

    private static Control BuildIncompatibleCapabilityCard(
        PaneElementDescriptor element,
        PaneCapabilityDescriptor? capability,
        IReadOnlyList<PaneCapabilityHostClass> supportedHostClasses)
    {
        var capabilityLabel = capability is null
            ? element.Binding?.TargetRef ?? "unknown capability"
            : $"{capability.DisplayLabel} ({capability.CapabilityId})";
        var supportedClassesText = supportedHostClasses.Count == 0
            ? "none declared"
            : string.Join(", ", supportedHostClasses);
        var capabilityClassesText = capability?.EffectiveCompatibleHostClasses is { Count: > 0 } capabilityClasses
            ? string.Join(", ", capabilityClasses)
            : "none declared";

        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = $"Incompatible capability binding: {element.ElementKind}",
            Foreground = ParseBrush("#FFE5B8"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"{capabilityLabel} cannot currently bind to '{element.DisplayLabel}'.",
            Foreground = ParseBrush("#D4E3EF"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"Element host classes: {supportedClassesText}",
            Foreground = ParseBrush("#D4E3EF"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"Capability host classes: {capabilityClassesText}",
            Foreground = ParseBrush("#D4E3EF"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Background = ParseBrush("#332514"),
            BorderBrush = ParseBrush("#8C6933"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static string? TryGetBehaviorSetting(PaneElementDescriptor element, string key)
    {
        if (element.BehaviorSettings is null ||
            !element.BehaviorSettings.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static Control CreateBoundOrResolvedTextBlock(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel,
        double fontSize,
        IBrush foreground)
    {
        var textBlock = new TextBlock
        {
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground
        };

        var binding = CreateResolvedTextBinding(element, pane, windowViewModel);
        if (binding is not null)
        {
            textBlock.Bind(TextBlock.TextProperty, binding);
        }
        else
        {
            textBlock.Text = ResolveBoundText(pane, element, windowViewModel);
        }

        return textBlock;
    }

    private static Binding? CreateResolvedTextBinding(
        PaneElementDescriptor element,
        ChildPaneDescriptor pane,
        MainWindowViewModel? windowViewModel,
        string? stringFormat = null)
    {
        if (element.Binding is null)
        {
            return null;
        }

        var panePropertyName = TryGetPaneStateSelectorBindingPropertyName(element.Binding.TargetRef);
        var windowPropertyName = TryGetStateSelectorBindingPropertyName(element.Binding.TargetRef);

        Binding? binding = element.Binding.TargetKind switch
        {
            PaneElementBindingTargetKind.StateSelector when
                !string.IsNullOrWhiteSpace(panePropertyName) =>
                    new Binding(panePropertyName!)
                    {
                        Source = pane
                    },

            PaneElementBindingTargetKind.StateSelector when
                windowViewModel is not null &&
                !string.IsNullOrWhiteSpace(windowPropertyName) =>
                    new Binding(windowPropertyName!)
                    {
                        Source = windowViewModel
                    },

            PaneElementBindingTargetKind.RuntimeFeed when windowViewModel is not null =>
                new Binding("LastActivitySummary")
                {
                    Source = windowViewModel
                },

            PaneElementBindingTargetKind.ArchiveView when windowViewModel is not null =>
                new Binding("CommandHistorySummary")
                {
                    Source = windowViewModel
                },

            PaneElementBindingTargetKind.ResourceContext =>
                new Binding("PaneBoundResourceReadoutSubtitle")
                {
                    Source = pane
                },

            _ => null
        };

        if (binding is not null && !string.IsNullOrWhiteSpace(stringFormat))
        {
            binding.StringFormat = stringFormat;
        }

        return binding;
    }

    private static string? TryGetPaneStateSelectorBindingPropertyName(string targetRef)
    {
        return targetRef switch
        {
            "pane.instance.working_copy_status" => "PaneWorkingCopyStatusSummary",
            "pane.instance.source_summary" => "PaneWorkingCopySourceSummary",
            "pane.instance.definition_sync_summary" => "PaneWorkingCopyDefinitionSyncSummary",
            "pane.instance.local_state_summary" => "PaneWorkingCopyLocalStateSummary",
            "pane.instance.override_summary" => "PaneLocalOverrideSummary",
            "pane.instance.lifecycle_summary" => "PaneLifecycleActionSummary",
            "pane.instance.current_authored_summary" => "PaneCurrentAuthoredSummary",
            "pane.instance.baseline_authored_summary" => "PaneBaselineAuthoredSummary",
            "pane.instance.authored_value_status" => "PaneAuthoredValueStatusSummary",
            "pane.instance.title_summary" => "PaneTitleSummary",
            "pane.instance.description_summary" => "PaneDescriptionSummary",
            "pane.instance.appearance_current_summary" => "PaneAppearanceCurrentValueSummary",
            "pane.instance.appearance_baseline_summary" => "PaneAppearanceBaselineValueSummary",
            "pane.instance.appearance_summary" => "PaneAppearanceSummary",
            "pane.instance.definition_summary" => "PaneDefinitionChooserSourceSummary",
            "pane.instance.definition_action_summary" => "PaneDefinitionChooserActionSummary",
            _ => null
        };
    }

    private static string? TryGetStateSelectorBindingPropertyName(string targetRef)
    {
        return targetRef switch
        {
            "engine.state.current_selection" => "SelectionSummary",
            "engine.state.focus_summary" => "FocusSummary",
            "engine.state.interaction_mode" => "InteractionModeSummary",
            "engine.state.group_summary" => "GroupSummary",
            "engine.state.link_summary" => "LinkSummary",
            "engine.state.bookmark_summary" => "BookmarkSummary",
            "engine.state.panel_summary" => "PanelSummary",
            "engine.state.seeded_pane_catalog_summary" => "SeededPaneCatalogSummary",
            "engine.state.seeded_pane_catalog_primary_label" => "SeededPaneCatalogPrimaryLabel",
            "engine.state.focus_origin" => "FocusOriginSummary",
            "engine.state.view_summary" => "ViewSummary",
            "engine.state.view_details" => "ViewDetails",
            "engine.state.bookmark_details" => "BookmarkDetails",
            "engine.state.group_details" => "GroupDetails",
            "engine.state.panel_details" => "PanelDetails",
            "engine.state.link_details" => "LinkDetails",
            "engine.state.focused_transform_summary" => "FocusedTransformSummary",
            "engine.state.focused_transform_details" => "FocusedTransformDetails",
            "engine.state.pane_structure" => "PaneStructureSummary",
            "engine.state.navigation_history" => "NavigationHistorySummary",
            "engine.state.command_history" => "CommandHistorySummary",
            "engine.state.action_readiness" => "ActionReadinessSummary",
            "engine.state.visual_semantics_settings_summary" => "VisualSemanticsSettingsSummary",
            "engine.state.render_surface_settings_summary" => "RenderSurfaceSettingsSummary",
            "engine.state.settings_surface_audit_summary" => "SettingsSurfaceAuditSummary",
            "engine.state.parent_shell_control_audit_summary" => "ParentShellControlAuditSummary",
            "engine.state.main_window_shell_chrome_audit_summary" => "MainWindowShellChromeAuditSummary",
            "engine.state.hardcoded_surface_audit_next_targets_summary" => "HardcodedSurfaceAuditNextTargetsSummary",
            "engine.state.interaction_semantics" => "InteractionSemanticsSummary",
            "engine.state.interaction_mode_badge" => "InteractionModeBadgeLabel",
            "engine.state.pane_catalog_definition_details" => "PaneCatalogDefinitionDetails",
            "engine.state.workspace_catalog_details" => "WorkspaceCatalogDetails",
            "engine.state.shell_command_catalog_candidate_summary" => "ShellCommandCatalogCandidateSummary",
            "engine.state.shell_command_native_chrome_summary" => "ShellCommandNativeChromeSummary",
            "engine.state.shell_command_future_capability_summary" => "ShellCommandFutureCapabilitySummary",
            "engine.state.viewport_command_surface_audit_summary" => "ViewportCommandSurfaceAuditSummary",
            "engine.state.renderer_viewport_registry_gap_summary" => "RendererViewportRegistryGapSummary",
            "engine.state.shell_native_chrome_boundary_summary" => "ShellNativeChromeBoundarySummary",
            "engine.state.renderer_parity_next_targets_summary" => "RendererParityNextTargetsSummary",
            "engine.state.active_panel_command_surface_state_summary" => "ActivePanelCommandSurfaceStateSummary",
            "engine.state.renderer_halo_and_group_effect_summary" => "RendererHaloAndGroupEffectSummary",
            "engine.state.projected_hit_testing_boundary_summary" => "ProjectedHitTestingBoundarySummary",
            "engine.state.renderer_migration_boundary_summary" => "RendererMigrationBoundarySummary",
            _ => null
        };
    }

    private static Control BuildBoundTextSurface(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var editorKind = TryGetBehaviorSetting(element, "editorKind");
        if (string.Equals(editorKind, "pane_title", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editorKind, "pane-title", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPaneTitleEditorSurface(pane, element, windowViewModel);
        }

        if (string.Equals(editorKind, "pane_description", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editorKind, "pane-description", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPaneDescriptionEditorSurface(pane, element, windowViewModel);
        }

        var stack = new StackPanel
        {
            Spacing = 4
        };

        var title = string.IsNullOrWhiteSpace(element.DisplayLabel)
            ? element.ElementKind.ToString()
            : element.DisplayLabel;

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        var textBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = ParseBrush("#0F1B25"),
            BorderBrush = ParseBrush("#355066"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Foreground = ParseBrush("#D7E6F1"),
            FontSize = 10,
            MinHeight = element.ElementKind switch
            {
                PaneElementKind.TaskMonitor or
                PaneElementKind.RuntimeActivityPanel or
                PaneElementKind.EventFeed or
                PaneElementKind.StreamConsole or
                PaneElementKind.ArchiveBrowser => 124,
                PaneElementKind.TextEditor or
                PaneElementKind.PropertyEditor or
                PaneElementKind.ProjectionStatusView => 108,
                PaneElementKind.MetricsReadout => 72,
                _ => 88
            }
        };

        if (element.ElementKind is
            PaneElementKind.TaskMonitor or
            PaneElementKind.RuntimeActivityPanel or
            PaneElementKind.EventFeed or
            PaneElementKind.StreamConsole or
            PaneElementKind.ArchiveBrowser)
        {
            textBox.FontFamily = new FontFamily("Consolas");
        }

        if (pane.IsAuthorMode)
        {
            textBox.IsEnabled = false;
            textBox.Text = BuildAuthorModePreviewText(element);
        }
        else
        {
            var binding = CreateResolvedTextBinding(element, pane, windowViewModel);
            if (binding is not null)
            {
                textBox.Bind(TextBox.TextProperty, binding);
            }
            else
            {
                textBox.Text = ResolveBoundText(pane, element, windowViewModel);
            }
        }

        stack.Children.Add(textBox);

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#2E475B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildPaneTitleEditorSurface(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        var title = string.IsNullOrWhiteSpace(element.DisplayLabel)
            ? "Local Title"
            : element.DisplayLabel;
        var description = TryGetBehaviorSetting(element, "description");
        var placeholderText = TryGetBehaviorSetting(element, "placeholder") ?? "Pane title";

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = ParseBrush("#C5D5E4"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneTitleCurrentValueSummary,
            Foreground = ParseBrush("#D5E4F1"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneTitleBaselineValueSummary,
            Foreground = ParseBrush("#D5E4F1"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        var editor = new TextBox
        {
            Text = pane.PaneAuthoredTitleEditorValue,
            PlaceholderText = placeholderText,
            Width = 260
        };

        stack.Children.Add(editor);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = 28,
            ItemWidth = 120
        };

        var applyButton = new Button
        {
            Content = "Apply Title",
            IsEnabled = windowViewModel is not null
        };
        applyButton.Click += (_, _) =>
        {
            windowViewModel?.TrySetChildPaneLocalTitle(pane.Id, editor.Text);
        };
        actions.Children.Add(applyButton);

        var resetButton = new Button
        {
            Content = "Reset Title",
            IsVisible = pane.CanResetLocalTitleOverride,
            IsEnabled = windowViewModel is not null
        };
        resetButton.Click += (_, _) =>
        {
            if (windowViewModel?.TryResetChildPaneLocalTitle(pane.Id) == true)
            {
                editor.Text = pane.TitleBaseline;
            }
        };
        actions.Children.Add(resetButton);

        stack.Children.Add(actions);

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#4A6378"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildPaneDescriptionEditorSurface(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        var title = string.IsNullOrWhiteSpace(element.DisplayLabel)
            ? "Local Description"
            : element.DisplayLabel;
        var description = TryGetBehaviorSetting(element, "description");
        var placeholderText = TryGetBehaviorSetting(element, "placeholder") ?? "Pane description";

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = ParseBrush("#C5D5E4"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneDescriptionCurrentValueSummary,
            Foreground = ParseBrush("#D5E4F1"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = pane.PaneDescriptionBaselineValueSummary,
            Foreground = ParseBrush("#D5E4F1"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        var editor = new TextBox
        {
            Text = pane.PaneAuthoredDescriptionEditorValue,
            PlaceholderText = placeholderText,
            Width = 320,
            Height = 72,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };

        stack.Children.Add(editor);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = 28,
            ItemWidth = 130
        };

        var applyButton = new Button
        {
            Content = "Apply Description",
            IsEnabled = windowViewModel is not null
        };
        applyButton.Click += (_, _) =>
        {
            windowViewModel?.TrySetChildPaneLocalDescription(pane.Id, editor.Text);
        };
        actions.Children.Add(applyButton);

        var resetButton = new Button
        {
            Content = "Reset Description",
            IsVisible = pane.CanResetLocalDescriptionOverride,
            IsEnabled = windowViewModel is not null
        };
        resetButton.Click += (_, _) =>
        {
            if (windowViewModel?.TryResetChildPaneLocalDescription(pane.Id) == true)
            {
                editor.Text = pane.DescriptionBaseline;
            }
        };
        actions.Children.Add(resetButton);

        stack.Children.Add(actions);

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#4A6378"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static Control BuildCatalogListSurface(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        string title,
        IReadOnlyList<string> lines)
    {
        var stack = new StackPanel
        {
            Spacing = 4
        };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ParseBrush("#9DD1F0"),
            FontSize = 10
        });

        stack.Children.Add(new TextBlock
        {
            Text = element.DisplayLabel,
            Foreground = ParseBrush("#F3F8FD"),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold
        });

        if (pane.IsAuthorMode)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Author-mode preview. {lines.Count} live item(s) suppressed while composing the pane body.",
                Foreground = ParseBrush("#9DB3C5"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else if (lines.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No items available.",
                Foreground = ParseBrush("#9DB3C5"),
                FontSize = 10
            });
        }
        else
        {
            foreach (var line in lines)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"• {line}",
                    Foreground = ParseBrush("#D7E6F1"),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        return new Border
        {
            Background = ParseBrush("#13212C"),
            BorderBrush = ParseBrush("#2E475B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static string BuildAuthorModePreviewText(PaneElementDescriptor element)
    {
        if (element.Binding is null)
        {
            return $"Author-mode preview for '{element.DisplayLabel}'.{Environment.NewLine}No live binding is configured.";
        }

        return $"Author-mode preview for '{element.DisplayLabel}'.{Environment.NewLine}Live content suppressed: {element.Binding.TargetKind} · {element.Binding.TargetRef}";
    }

    private static Control BuildUnsupportedElementCard(PaneElementDescriptor element)
    {
        var bindingLabel = element.Binding is null
            ? "No binding"
            : $"{element.Binding.TargetKind}: {element.Binding.TargetRef}";

        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = $"Unsupported element kind: {element.ElementKind}",
            Foreground = ParseBrush("#FFE5B8"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"{element.DisplayLabel} · {bindingLabel}",
            Foreground = ParseBrush("#D4E3EF"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Background = ParseBrush("#332514"),
            BorderBrush = ParseBrush("#8C6933"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = stack
        };
    }

    private static string ResolveBoundText(
        ChildPaneDescriptor pane,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
        if (element.Binding is null)
        {
            return element.DisplayLabel;
        }

        return element.Binding.TargetKind switch
        {
            PaneElementBindingTargetKind.LiteralText => element.Binding.TargetRef,
            PaneElementBindingTargetKind.StateSelector => ResolveStateSelectorText(element.Binding.TargetRef, windowViewModel),
            PaneElementBindingTargetKind.RuntimeFeed => string.Join(Environment.NewLine, GetRuntimeActivityLines(windowViewModel)),
            PaneElementBindingTargetKind.ArchiveView => string.Join(Environment.NewLine, GetArchiveHistoryLines(windowViewModel)),
            PaneElementBindingTargetKind.ResourceContext => pane.PaneBoundResourceReadoutSubtitle,
            _ => $"{element.DisplayLabel} · {element.Binding.TargetRef}"
        };
    }

    private static string ResolveStateSelectorText(
        string targetRef,
        MainWindowViewModel? windowViewModel)
    {
        return targetRef switch
        {
            "engine.state.current_selection" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "CurrentSelectionSummary",
                    "SelectionSummary",
                    "SelectionReadout",
                    "_selectionSummary")
                ?? "Current selection summary unavailable.",

            "engine.state.focus_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "FocusSummary",
                    "FocusedNodeSummary",
                    "FocusReadout",
                    "_focusSummary")
                ?? "Focus summary unavailable.",

            "engine.state.interaction_mode" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "InteractionModeSummary",
                    "CurrentInteractionModeSummary",
                    "InteractionModeReadout",
                    "_interactionModeSummary")
                ?? "Interaction mode summary unavailable.",

            "engine.state.group_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "GroupSummary",
                    "CurrentGroupSummary",
                    "GroupReadout",
                    "_groupSummary")
                ?? "Group summary unavailable.",

            "engine.state.link_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "LinkSummary",
                    "CurrentLinkSummary",
                    "LinkReadout",
                    "_linkSummary")
                ?? "Link summary unavailable.",

            "engine.state.bookmark_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "BookmarkSummary",
                    "BookmarkReadout",
                    "_bookmarkSummary")
                ?? "Bookmark summary unavailable.",

            "engine.state.panel_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "PanelSummary",
                    "PanelReadout",
                    "_panelSummary")
                ?? "Panel summary unavailable.",

            "engine.state.seeded_pane_catalog_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "SeededPaneCatalogSummary",
                    "_seededPaneCatalogSummary")
                ?? "Seeded pane catalog summary unavailable.",

            "engine.state.seeded_pane_catalog_primary_label" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "SeededPaneCatalogPrimaryLabel",
                    "_seededPaneCatalogPrimaryLabel")
                ?? "Seeded pane catalog primary label unavailable.",

            "engine.state.focus_origin" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "FocusOriginSummary",
                    "_focusOriginSummary")
                ?? "Focus origin summary unavailable.",

            "engine.state.view_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ViewSummary",
                    "_viewSummary")
                ?? "View summary unavailable.",

            "engine.state.view_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ViewDetails",
                    "_viewDetails")
                ?? "View details unavailable.",

            "engine.state.bookmark_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "BookmarkDetails",
                    "_bookmarkDetails")
                ?? "Bookmark details unavailable.",

            "engine.state.group_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "GroupDetails",
                    "_groupDetails")
                ?? "Group details unavailable.",

            "engine.state.panel_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "PanelDetails",
                    "_panelDetails")
                ?? "Panel details unavailable.",

            "engine.state.link_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "LinkDetails",
                    "_linkDetails")
                ?? "Link details unavailable.",

            "engine.state.focused_transform_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "FocusedTransformSummary",
                    "_focusedTransformSummary")
                ?? "Focused transform summary unavailable.",

            "engine.state.focused_transform_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "FocusedTransformDetails",
                    "_focusedTransformDetails")
                ?? "Focused transform details unavailable.",

            "engine.state.pane_structure" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "PaneStructureSummary",
                    "_paneStructureSummary")
                ?? "Pane structure summary unavailable.",

            "engine.state.navigation_history" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "NavigationHistorySummary",
                    "_navigationHistorySummary")
                ?? "Navigation history unavailable.",

            "engine.state.command_history" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "CommandHistorySummary",
                    "_commandHistorySummary")
                ?? "Command history unavailable.",

            "engine.state.action_readiness" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ActionReadinessSummary",
                    "_actionReadinessSummary")
                ?? "Action readiness summary unavailable.",

            "engine.state.interaction_semantics" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "InteractionSemanticsSummary",
                    "_interactionSemanticsSummary")
                ?? "Interaction semantics summary unavailable.",

            "engine.state.interaction_mode_badge" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "InteractionModeBadgeLabel",
                    "_interactionModeBadgeLabel")
                ?? "Navigate",

            "engine.state.visual_semantics_settings_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "VisualSemanticsSettingsSummary",
                    "_visualSemanticsSettingsSummary")
                ?? "Visual semantics settings summary unavailable.",

            "engine.state.render_surface_settings_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "RenderSurfaceSettingsSummary",
                    "_renderSurfaceSettingsSummary")
                ?? "Render surface settings summary unavailable.",

            "engine.state.settings_surface_audit_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "SettingsSurfaceAuditSummary",
                    "_settingsSurfaceAuditSummary")
                ?? "Settings surface audit summary unavailable.",

            "engine.state.parent_shell_control_audit_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ParentShellControlAuditSummary",
                    "_parentShellControlAuditSummary")
                ?? "Parent shell control audit summary unavailable.",

            "engine.state.main_window_shell_chrome_audit_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "MainWindowShellChromeAuditSummary",
                    "_mainWindowShellChromeAuditSummary")
                ?? "Main window shell chrome audit summary unavailable.",

            "engine.state.hardcoded_surface_audit_next_targets_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "HardcodedSurfaceAuditNextTargetsSummary",
                    "_hardcodedSurfaceAuditNextTargetsSummary")
                ?? "Hardcoded-surface audit next targets unavailable.",

            "engine.state.pane_catalog_definition_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "PaneCatalogDefinitionDetails",
                    "_paneCatalogDefinitionDetails")
                ?? "Pane catalog definition details unavailable.",

            "engine.state.workspace_catalog_details" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "WorkspaceCatalogDetails",
                    "_workspaceCatalogDetails")
                ?? "Workspace catalog details unavailable.",

            "engine.state.shell_command_catalog_candidate_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ShellCommandCatalogCandidateSummary",
                    "_shellCommandCatalogCandidateSummary")
                ?? "Shell command catalog candidate summary unavailable.",

            "engine.state.shell_command_native_chrome_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ShellCommandNativeChromeSummary",
                    "_shellCommandNativeChromeSummary")
                ?? "Shell command native chrome summary unavailable.",

            "engine.state.shell_command_future_capability_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ShellCommandFutureCapabilitySummary",
                    "_shellCommandFutureCapabilitySummary")
                ?? "Shell command future capability summary unavailable.",

            "engine.state.viewport_command_surface_audit_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ViewportCommandSurfaceAuditSummary",
                    "_viewportCommandSurfaceAuditSummary")
                ?? "Viewport command surface audit summary unavailable.",

            "engine.state.renderer_viewport_registry_gap_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "RendererViewportRegistryGapSummary",
                    "_rendererViewportRegistryGapSummary")
                ?? "Renderer viewport registry gap summary unavailable.",

            "engine.state.shell_native_chrome_boundary_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ShellNativeChromeBoundarySummary",
                    "_shellNativeChromeBoundarySummary")
                ?? "Shell native chrome boundary summary unavailable.",

            "engine.state.renderer_parity_next_targets_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "RendererParityNextTargetsSummary",
                    "_rendererParityNextTargetsSummary")
                ?? "Renderer parity next targets summary unavailable.",

            "engine.state.active_panel_command_surface_state_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ActivePanelCommandSurfaceStateSummary",
                    "_activePanelCommandSurfaceStateSummary")
                ?? "Active panel command surface state summary unavailable.",

            "engine.state.renderer_halo_and_group_effect_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "RendererHaloAndGroupEffectSummary",
                    "_rendererHaloAndGroupEffectSummary")
                ?? "Renderer halo and group effect summary unavailable.",

            "engine.state.projected_hit_testing_boundary_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "ProjectedHitTestingBoundarySummary",
                    "_projectedHitTestingBoundarySummary")
                ?? "Projected hit-testing boundary summary unavailable.",

            "engine.state.renderer_migration_boundary_summary" =>
                ResolveFirstStringMember(
                    windowViewModel,
                    "RendererMigrationBoundarySummary",
                    "_rendererMigrationBoundarySummary")
                ?? "Renderer migration boundary summary unavailable.",

            _ => targetRef
        };
    }

    private static ICommand? ResolveCommand(
        PaneElementBindingDescriptor? binding,
        MainWindowViewModel? windowViewModel)
    {
        if (binding is null || windowViewModel is null)
        {
            return null;
        }

        var propertyName = binding.TargetRef switch
        {
            "engine.command.focus_first_node" => "FocusFirstNodeCommand",
            "engine.command.select_first_node" => "SelectFirstNodeCommand",
            "engine.command.focus_first_panel" => "FocusFirstPanelCommand",
            "engine.command.select_first_panel" => "SelectFirstPanelCommand",
            "engine.command.home_view" => "HomeViewCommand",
            "engine.command.frame_selection" => "FrameSelectionCommand",
            "engine.command.center_focused_node" => "CenterFocusedNodeCommand",
            "engine.command.clear_selection" => "ClearSelectionCommand",
            "engine.command.activate_navigate_mode" => "ActivateNavigateModeCommand",
            "engine.command.activate_move_mode" => "ActivateMoveModeCommand",
            "engine.command.activate_marquee_mode" => "ActivateMarqueeModeCommand",
            "engine.command.create_child_pane" => "CreateChildPaneCommand",
            "engine.command.pane_save_instance_only" => "SaveChildPaneInstanceOnlyCommand",
            "engine.command.pane_save_as_new_definition" => "SaveChildPaneAsNewDefinitionCommand",
            "engine.command.pane_detach_from_definition" => "DetachChildPaneFromDefinitionCommand",
            "engine.command.pane_revert_to_definition" => "RevertChildPaneToDefinitionCommand",
            "engine.command.pane_apply_default_appearance" => "ApplyChildPaneDefaultAppearanceCommand",
            "engine.command.pane_apply_cool_appearance" => "ApplyChildPaneCoolAppearanceCommand",
            "engine.command.pane_apply_warm_appearance" => "ApplyChildPaneWarmAppearanceCommand",
            "engine.command.pane_reset_appearance" => "ResetChildPaneAppearanceVariantCommand",
            "engine.command.create_demo_node" => "CreateDemoNodeCommand",
            "engine.command.delete_focused_node" => "DeleteFocusedNodeCommand",
            "engine.command.group_selection" => "GroupSelectionCommand",
            "engine.command.connect_focused_node" => "ConnectFocusedNodeCommand",
            "engine.command.unlink_focused_node" => "UnlinkFocusedNodeCommand",
            "engine.command.save_bookmark" => "SaveBookmarkCommand",
            "engine.command.restore_latest_bookmark" => "RestoreLatestBookmarkCommand",
            "engine.command.clear_links" => "ClearLinksCommand",
            "engine.command.nudge_focused_left" => "NudgeFocusedLeftCommand",
            "engine.command.nudge_focused_right" => "NudgeFocusedRightCommand",
            "engine.command.nudge_focused_up" => "NudgeFocusedUpCommand",
            "engine.command.nudge_focused_down" => "NudgeFocusedDownCommand",
            "engine.command.nudge_focused_forward" => "NudgeFocusedForwardCommand",
            "engine.command.nudge_focused_back" => "NudgeFocusedBackCommand",
            "engine.command.grow_focused_node" => "GrowFocusedNodeCommand",
            "engine.command.shrink_focused_node" => "ShrinkFocusedNodeCommand",
            "engine.command.apply_triangle_primitive" => "ApplyTrianglePrimitiveCommand",
            "engine.command.apply_square_primitive" => "ApplySquarePrimitiveCommand",
            "engine.command.apply_diamond_primitive" => "ApplyDiamondPrimitiveCommand",
            "engine.command.apply_pentagon_primitive" => "ApplyPentagonPrimitiveCommand",
            "engine.command.apply_hexagon_primitive" => "ApplyHexagonPrimitiveCommand",
            "engine.command.apply_cube_primitive" => "ApplyCubePrimitiveCommand",
            "engine.command.apply_tetrahedron_primitive" => "ApplyTetrahedronPrimitiveCommand",
            "engine.command.apply_sphere_primitive" => "ApplySpherePrimitiveCommand",
            "engine.command.apply_box_primitive" => "ApplyBoxPrimitiveCommand",
            "engine.command.apply_blue_appearance" => "ApplyBlueAppearanceCommand",
            "engine.command.apply_violet_appearance" => "ApplyVioletAppearanceCommand",
            "engine.command.apply_green_appearance" => "ApplyGreenAppearanceCommand",
            "engine.command.increase_opacity" => "IncreaseOpacityCommand",
            "engine.command.decrease_opacity" => "DecreaseOpacityCommand",
            "engine.command.apply_background_deep_space" => "ApplyBackgroundDeepSpaceCommand",
            "engine.command.apply_background_dusk" => "ApplyBackgroundDuskCommand",
            "engine.command.apply_background_paper" => "ApplyBackgroundPaperCommand",
            "engine.command.attach_demo_panel" => "AttachDemoPanelCommand",
            "engine.command.attach_label_panelette" => "AttachLabelPaneletteCommand",
            "engine.command.attach_detail_metadata_panelette" => "AttachDetailMetadataPaneletteCommand",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        var property = typeof(MainWindowViewModel).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(windowViewModel) is ICommand command)
        {
            return command;
        }

        return null;
    }

    private static object? ResolveCommandParameter(
        ChildPaneDescriptor pane,
        PaneElementBindingDescriptor? binding)
    {
        if (binding is null)
        {
            return null;
        }

        return binding.TargetRef switch
        {
            "engine.command.create_child_pane" => pane.ParentId,
            "engine.command.pane_save_instance_only" => pane.Id,
            "engine.command.pane_save_as_new_definition" => pane.Id,
            "engine.command.pane_detach_from_definition" => pane.Id,
            "engine.command.pane_revert_to_definition" => pane.Id,
            "engine.command.pane_apply_default_appearance" => pane.Id,
            "engine.command.pane_apply_cool_appearance" => pane.Id,
            "engine.command.pane_apply_warm_appearance" => pane.Id,
            "engine.command.pane_reset_appearance" => pane.Id,
            _ => null
        };
    }

    private static IReadOnlyList<string> GetCommandCatalogLines()
    {
        return CapabilityIndex.Values
            .OrderBy(capability => capability.DisplayLabel, StringComparer.Ordinal)
            .Take(8)
            .Select(capability => $"{capability.DisplayLabel} · {capability.CapabilityKind}")
            .ToArray();
    }

    private static IReadOnlyList<string> GetRuntimeActivityLines(
        MainWindowViewModel? windowViewModel)
    {
        var lastActivity = ResolveFirstStringMember(
            windowViewModel,
            "LastActivitySummary",
            "_lastActivitySummary");

        return string.IsNullOrWhiteSpace(lastActivity)
            ? ["Runtime activity summary unavailable."]
            : [lastActivity];
    }

    private static IReadOnlyList<string> GetPaneCatalogLines(
        MainWindowViewModel? windowViewModel)
    {
        if (windowViewModel?.SeededPaneDefinitions.Count > 0)
        {
            return windowViewModel.SeededPaneDefinitions
                .OrderBy(definition => definition.DisplayLabel, StringComparer.Ordinal)
                .Take(10)
                .Select(definition =>
                    $"{definition.DisplayLabel} · {(definition.IsSeeded ? "Seeded" : "User")} · {definition.Elements.Count} elements")
                .ToArray();
        }

        return ["No pane definitions available."];
    }

    private static IReadOnlyList<string> GetWorkspaceCatalogLines(
        MainWindowViewModel? windowViewModel)
    {
        if (windowViewModel?.SeededPaneWorkspaces.Count > 0)
        {
            return windowViewModel.SeededPaneWorkspaces
                .OrderBy(workspace => workspace.DisplayLabel, StringComparer.Ordinal)
                .Take(6)
                .Select(workspace =>
                    $"{workspace.DisplayLabel} · {(workspace.IsSeeded ? "Seeded" : "User")} · {workspace.Members.Count} members")
                .ToArray();
        }

        return ["No workspace definitions available."];
    }

    private static IReadOnlyList<string> GetArchiveHistoryLines(
        MainWindowViewModel? windowViewModel)
    {
        var history = ResolveEnumerableMember(
            windowViewModel,
            "_commandHistory",
            "CommandHistoryLines",
            "RecentCommandHistory");

        if (history.Count == 0)
        {
            return ["History is currently empty."];
        }

        return history
            .Reverse()
            .Take(6)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveEnumerableMember(
        object? target,
        params string[] memberNames)
    {
        if (target is null)
        {
            return Array.Empty<string>();
        }

        foreach (var memberName in memberNames)
        {
            var memberValue = TryGetMemberValue(target, memberName);
            if (memberValue is string singleLine)
            {
                return string.IsNullOrWhiteSpace(singleLine)
                    ? Array.Empty<string>()
                    : [singleLine];
            }

            if (memberValue is not IEnumerable enumerable)
            {
                continue;
            }

            var lines = new List<string>();
            foreach (var entry in enumerable)
            {
                if (entry is null)
                {
                    continue;
                }

                var line = entry.ToString();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (lines.Count > 0)
            {
                return lines;
            }
        }

        return Array.Empty<string>();
    }

    private static string? ResolveFirstStringMember(
        object? target,
        params string[] memberNames)
    {
        if (target is null)
        {
            return null;
        }

        foreach (var memberName in memberNames)
        {
            var memberValue = TryGetMemberValue(target, memberName);
            if (memberValue is null)
            {
                continue;
            }

            if (memberValue is string stringValue)
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    return stringValue;
                }

                continue;
            }

            var converted = memberValue.ToString();
            if (!string.IsNullOrWhiteSpace(converted))
            {
                return converted;
            }
        }

        return null;
    }

    private static object? TryGetMemberValue(
        object target,
        string memberName)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        var type = target.GetType();

        var property = type.GetProperty(memberName, flags);
        if (property is not null)
        {
            return property.GetValue(target);
        }

        var field = type.GetField(memberName, flags);
        return field?.GetValue(target);
    }

    private static IBrush ParseBrush(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }
}
