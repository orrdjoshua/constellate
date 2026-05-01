using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.App.Controls;

internal static class PaneDefinitionRuntimeBuilder
{
    private static readonly IPaneCatalog PaneCatalog = new SeededPaneCatalog();
    private static readonly IReadOnlyDictionary<string, PaneCapabilityDescriptor> CapabilityIndex =
        PaneCatalog.GetCapabilityDescriptors()
            .ToDictionary(capability => capability.CapabilityId, StringComparer.Ordinal);

    public static Control Build(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        MainWindowViewModel? windowViewModel)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(definition);

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

    private static Control BuildElement(
        ChildPaneDescriptor pane,
        PaneDefinitionDescriptor definition,
        PaneElementDescriptor element,
        MainWindowViewModel? windowViewModel)
    {
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
                element,
                ResolveCatalogSurfaceTitle(element),
                ResolveCatalogSurfaceLines(element, windowViewModel)),
            PaneElementKind.RuntimeActivityPanel or
            PaneElementKind.EventFeed or
            PaneElementKind.StreamConsole or
            PaneElementKind.TaskMonitor => BuildCatalogListSurface(element, "Runtime Activity", GetRuntimeActivityLines(windowViewModel)),
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
            Width = 220
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
                new Binding(nameof(MainWindowViewModel.LastActivitySummary))
                {
                    Source = windowViewModel
                },

            PaneElementBindingTargetKind.ArchiveView when windowViewModel is not null =>
                new Binding(nameof(MainWindowViewModel.CommandHistorySummary))
                {
                    Source = windowViewModel
                },

            PaneElementBindingTargetKind.ResourceContext =>
                new Binding(nameof(ChildPaneDescriptor.PaneBoundResourceReadoutSubtitle)),

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
            "pane.instance.working_copy_status" => nameof(ChildPaneDescriptor.PaneWorkingCopyStatusSummary),
            "pane.instance.source_summary" => nameof(ChildPaneDescriptor.PaneWorkingCopySourceSummary),
            "pane.instance.definition_sync_summary" => nameof(ChildPaneDescriptor.PaneWorkingCopyDefinitionSyncSummary),
            "pane.instance.local_state_summary" => nameof(ChildPaneDescriptor.PaneWorkingCopyLocalStateSummary),
            "pane.instance.override_summary" => nameof(ChildPaneDescriptor.PaneLocalOverrideSummary),
            "pane.instance.lifecycle_summary" => nameof(ChildPaneDescriptor.PaneLifecycleActionSummary),
            "pane.instance.current_authored_summary" => nameof(ChildPaneDescriptor.PaneCurrentAuthoredSummary),
            "pane.instance.baseline_authored_summary" => nameof(ChildPaneDescriptor.PaneBaselineAuthoredSummary),
            "pane.instance.authored_value_status" => nameof(ChildPaneDescriptor.PaneAuthoredValueStatusSummary),
            "pane.instance.title_summary" => nameof(ChildPaneDescriptor.PaneTitleSummary),
            "pane.instance.description_summary" => nameof(ChildPaneDescriptor.PaneDescriptionSummary),
            "pane.instance.appearance_current_summary" => nameof(ChildPaneDescriptor.PaneAppearanceCurrentValueSummary),
            "pane.instance.appearance_baseline_summary" => nameof(ChildPaneDescriptor.PaneAppearanceBaselineValueSummary),
            "pane.instance.appearance_summary" => nameof(ChildPaneDescriptor.PaneAppearanceSummary),
            "pane.instance.definition_summary" => nameof(ChildPaneDescriptor.PaneDefinitionChooserSourceSummary),
            "pane.instance.definition_action_summary" => nameof(ChildPaneDescriptor.PaneDefinitionChooserActionSummary),
            _ => null
        };
    }

    private static string? TryGetStateSelectorBindingPropertyName(string targetRef)
    {
        return targetRef switch
        {
            "engine.state.current_selection" => nameof(MainWindowViewModel.SelectionSummary),
            "engine.state.focus_summary" => nameof(MainWindowViewModel.FocusSummary),
            "engine.state.interaction_mode" => nameof(MainWindowViewModel.InteractionModeSummary),
            "engine.state.group_summary" => nameof(MainWindowViewModel.GroupSummary),
            "engine.state.link_summary" => nameof(MainWindowViewModel.LinkSummary),
            "engine.state.bookmark_summary" => nameof(MainWindowViewModel.BookmarkSummary),
            "engine.state.panel_summary" => nameof(MainWindowViewModel.PanelSummary),
            "engine.state.seeded_pane_catalog_summary" => nameof(MainWindowViewModel.SeededPaneCatalogSummary),
            "engine.state.seeded_pane_catalog_primary_label" => nameof(MainWindowViewModel.SeededPaneCatalogPrimaryLabel),
            "engine.state.focus_origin" => nameof(MainWindowViewModel.FocusOriginSummary),
            "engine.state.view_summary" => nameof(MainWindowViewModel.ViewSummary),
            "engine.state.view_details" => nameof(MainWindowViewModel.ViewDetails),
            "engine.state.bookmark_details" => nameof(MainWindowViewModel.BookmarkDetails),
            "engine.state.group_details" => nameof(MainWindowViewModel.GroupDetails),
            "engine.state.panel_details" => nameof(MainWindowViewModel.PanelDetails),
            "engine.state.link_details" => nameof(MainWindowViewModel.LinkDetails),
            "engine.state.focused_transform_summary" => nameof(MainWindowViewModel.FocusedTransformSummary),
            "engine.state.focused_transform_details" => nameof(MainWindowViewModel.FocusedTransformDetails),
            "engine.state.pane_structure" => nameof(MainWindowViewModel.PaneStructureSummary),
            "engine.state.navigation_history" => nameof(MainWindowViewModel.NavigationHistorySummary),
            "engine.state.command_history" => nameof(MainWindowViewModel.CommandHistorySummary),
            "engine.state.action_readiness" => nameof(MainWindowViewModel.ActionReadinessSummary),
            "engine.state.visual_semantics_settings_summary" => nameof(MainWindowViewModel.VisualSemanticsSettingsSummary),
            "engine.state.render_surface_settings_summary" => nameof(MainWindowViewModel.RenderSurfaceSettingsSummary),
            "engine.state.settings_surface_audit_summary" => nameof(MainWindowViewModel.SettingsSurfaceAuditSummary),
            "engine.state.parent_shell_control_audit_summary" => nameof(MainWindowViewModel.ParentShellControlAuditSummary),
            "engine.state.main_window_shell_chrome_audit_summary" => nameof(MainWindowViewModel.MainWindowShellChromeAuditSummary),
            "engine.state.hardcoded_surface_audit_next_targets_summary" => nameof(MainWindowViewModel.HardcodedSurfaceAuditNextTargetsSummary),
            "engine.state.interaction_semantics" => nameof(MainWindowViewModel.InteractionSemanticsSummary),
            "engine.state.interaction_mode_badge" => nameof(MainWindowViewModel.InteractionModeBadgeLabel),
            "engine.state.pane_catalog_definition_details" => nameof(MainWindowViewModel.PaneCatalogDefinitionDetails),
            "engine.state.workspace_catalog_details" => nameof(MainWindowViewModel.WorkspaceCatalogDetails),
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

        var binding = CreateResolvedTextBinding(element, pane, windowViewModel);
        if (binding is not null)
        {
            textBox.Bind(TextBox.TextProperty, binding);
        }
        else
        {
            textBox.Text = ResolveBoundText(pane, element, windowViewModel);
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

        if (lines.Count == 0)
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
            "engine.command.focus_first_node" => nameof(MainWindowViewModel.FocusFirstNodeCommand),
            "engine.command.select_first_node" => nameof(MainWindowViewModel.SelectFirstNodeCommand),
            "engine.command.focus_first_panel" => nameof(MainWindowViewModel.FocusFirstPanelCommand),
            "engine.command.select_first_panel" => nameof(MainWindowViewModel.SelectFirstPanelCommand),
            "engine.command.home_view" => nameof(MainWindowViewModel.HomeViewCommand),
            "engine.command.frame_selection" => nameof(MainWindowViewModel.FrameSelectionCommand),
            "engine.command.center_focused_node" => nameof(MainWindowViewModel.CenterFocusedNodeCommand),
            "engine.command.clear_selection" => nameof(MainWindowViewModel.ClearSelectionCommand),
            "engine.command.activate_navigate_mode" => nameof(MainWindowViewModel.ActivateNavigateModeCommand),
            "engine.command.activate_move_mode" => nameof(MainWindowViewModel.ActivateMoveModeCommand),
            "engine.command.activate_marquee_mode" => nameof(MainWindowViewModel.ActivateMarqueeModeCommand),
            "engine.command.create_child_pane" => nameof(MainWindowViewModel.CreateChildPaneCommand),
            "engine.command.pane_save_instance_only" => nameof(MainWindowViewModel.SaveChildPaneInstanceOnlyCommand),
            "engine.command.pane_save_as_new_definition" => nameof(MainWindowViewModel.SaveChildPaneAsNewDefinitionCommand),
            "engine.command.pane_detach_from_definition" => nameof(MainWindowViewModel.DetachChildPaneFromDefinitionCommand),
            "engine.command.pane_revert_to_definition" => nameof(MainWindowViewModel.RevertChildPaneToDefinitionCommand),
            "engine.command.pane_apply_default_appearance" => nameof(MainWindowViewModel.ApplyChildPaneDefaultAppearanceCommand),
            "engine.command.pane_apply_cool_appearance" => nameof(MainWindowViewModel.ApplyChildPaneCoolAppearanceCommand),
            "engine.command.pane_apply_warm_appearance" => nameof(MainWindowViewModel.ApplyChildPaneWarmAppearanceCommand),
            "engine.command.pane_reset_appearance" => nameof(MainWindowViewModel.ResetChildPaneAppearanceVariantCommand),
            "engine.command.create_demo_node" => nameof(MainWindowViewModel.CreateDemoNodeCommand),
            "engine.command.delete_focused_node" => nameof(MainWindowViewModel.DeleteFocusedNodeCommand),
            "engine.command.group_selection" => nameof(MainWindowViewModel.GroupSelectionCommand),
            "engine.command.connect_focused_node" => nameof(MainWindowViewModel.ConnectFocusedNodeCommand),
            "engine.command.unlink_focused_node" => nameof(MainWindowViewModel.UnlinkFocusedNodeCommand),
            "engine.command.save_bookmark" => nameof(MainWindowViewModel.SaveBookmarkCommand),
            "engine.command.restore_latest_bookmark" => nameof(MainWindowViewModel.RestoreLatestBookmarkCommand),
            "engine.command.clear_links" => nameof(MainWindowViewModel.ClearLinksCommand),
            "engine.command.nudge_focused_left" => nameof(MainWindowViewModel.NudgeFocusedLeftCommand),
            "engine.command.nudge_focused_right" => nameof(MainWindowViewModel.NudgeFocusedRightCommand),
            "engine.command.nudge_focused_up" => nameof(MainWindowViewModel.NudgeFocusedUpCommand),
            "engine.command.nudge_focused_down" => nameof(MainWindowViewModel.NudgeFocusedDownCommand),
            "engine.command.nudge_focused_forward" => nameof(MainWindowViewModel.NudgeFocusedForwardCommand),
            "engine.command.nudge_focused_back" => nameof(MainWindowViewModel.NudgeFocusedBackCommand),
            "engine.command.grow_focused_node" => nameof(MainWindowViewModel.GrowFocusedNodeCommand),
            "engine.command.shrink_focused_node" => nameof(MainWindowViewModel.ShrinkFocusedNodeCommand),
            "engine.command.apply_triangle_primitive" => nameof(MainWindowViewModel.ApplyTrianglePrimitiveCommand),
            "engine.command.apply_square_primitive" => nameof(MainWindowViewModel.ApplySquarePrimitiveCommand),
            "engine.command.apply_diamond_primitive" => nameof(MainWindowViewModel.ApplyDiamondPrimitiveCommand),
            "engine.command.apply_pentagon_primitive" => nameof(MainWindowViewModel.ApplyPentagonPrimitiveCommand),
            "engine.command.apply_hexagon_primitive" => nameof(MainWindowViewModel.ApplyHexagonPrimitiveCommand),
            "engine.command.apply_cube_primitive" => nameof(MainWindowViewModel.ApplyCubePrimitiveCommand),
            "engine.command.apply_tetrahedron_primitive" => nameof(MainWindowViewModel.ApplyTetrahedronPrimitiveCommand),
            "engine.command.apply_sphere_primitive" => nameof(MainWindowViewModel.ApplySpherePrimitiveCommand),
            "engine.command.apply_box_primitive" => nameof(MainWindowViewModel.ApplyBoxPrimitiveCommand),
            "engine.command.apply_blue_appearance" => nameof(MainWindowViewModel.ApplyBlueAppearanceCommand),
            "engine.command.apply_violet_appearance" => nameof(MainWindowViewModel.ApplyVioletAppearanceCommand),
            "engine.command.apply_green_appearance" => nameof(MainWindowViewModel.ApplyGreenAppearanceCommand),
            "engine.command.increase_opacity" => nameof(MainWindowViewModel.IncreaseOpacityCommand),
            "engine.command.decrease_opacity" => nameof(MainWindowViewModel.DecreaseOpacityCommand),
            "engine.command.apply_background_deep_space" => nameof(MainWindowViewModel.ApplyBackgroundDeepSpaceCommand),
            "engine.command.apply_background_dusk" => nameof(MainWindowViewModel.ApplyBackgroundDuskCommand),
            "engine.command.apply_background_paper" => nameof(MainWindowViewModel.ApplyBackgroundPaperCommand),
            "engine.command.attach_demo_panel" => nameof(MainWindowViewModel.AttachDemoPanelCommand),
            "engine.command.attach_label_panelette" => nameof(MainWindowViewModel.AttachLabelPaneletteCommand),
            "engine.command.attach_detail_metadata_panelette" => nameof(MainWindowViewModel.AttachDetailMetadataPaneletteCommand),
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
            ? new[] { "Runtime activity summary unavailable." }
            : new[] { lastActivity };
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

        return new[] { "No pane definitions available." };
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

        return new[] { "No workspace definitions available." };
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
            return new[] { "History is currently empty." };
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
                    : new[] { singleLine };
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
