using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.App;

public sealed record ChildPaneCanvasElementInstance(
    string InstanceId,
    string SourceElementId,
    PaneElementKind ElementKind,
    string DisplayLabel,
    string BindingLabel,
    double X,
    double Y,
    double Width,
    double Height,
    int ZIndex,
    int Depth,
    bool IsLiveContentSuppressedInAuthorMode = true);

public sealed record ChildPaneCanvasSurfaceModel(
    double Width,
    double Height,
    IReadOnlyList<ChildPaneCanvasElementInstance> Elements)
{
    public string Summary =>
        $"Canvas surface: {Elements.Count} element instance(s) · extent {Width:0.#}×{Height:0.#}";
}

internal static class ChildPaneCanvasAuthoringProjector
{
    public static ChildPaneCanvasSurfaceModel Project(PaneDefinitionDescriptor definition, ChildPaneDescriptor? pane = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var elements = new List<ChildPaneCanvasElementInstance>();
        var currentY = 24.0;

        AppendElements(definition.Elements, pane, depth: 0, ref currentY, elements);

        var extentWidth = elements.Count == 0
            ? 420
            : Math.Max(420, elements.Max(element => element.X + element.Width) + 24);
        var extentHeight = elements.Count == 0
            ? 220
            : Math.Max(220, currentY);

        return new ChildPaneCanvasSurfaceModel(
            extentWidth,
            extentHeight,
            elements);
    }

    private static void AppendElements(
        IReadOnlyList<PaneElementDescriptor> sourceElements,
        ChildPaneDescriptor? pane,
        int depth,
        ref double currentY,
        List<ChildPaneCanvasElementInstance> realizedElements)
    {
        var authoringState = pane?.CanvasAuthoringState;

        foreach (var element in sourceElements)
        {
            var x = 24 + (depth * 28);
            var width = Math.Max(180, ResolveWidth(element) - (depth * 20));
            var height = ResolveHeight(element);
            var instanceId = $"canvas.{realizedElements.Count + 1}";
            var previewPlacement = authoringState?.TryGetElementPlacement(instanceId);
            var elementInstance = new ChildPaneCanvasElementInstance(
                InstanceId: instanceId,
                SourceElementId: element.ElementId,
                ElementKind: element.ElementKind,
                DisplayLabel: string.IsNullOrWhiteSpace(element.DisplayLabel)
                    ? element.ElementKind.ToString()
                    : element.DisplayLabel,
                BindingLabel: ResolveBindingLabel(element.Binding),
                X: previewPlacement?.X ?? x,
                Y: previewPlacement?.Y ?? currentY,
                Width: previewPlacement?.ResolveWidth(width) ?? width,
                Height: previewPlacement?.ResolveHeight(height) ?? height,
                ZIndex: realizedElements.Count,
                Depth: depth);

            realizedElements.Add(elementInstance);
            currentY += height + 16;

            if (element.Children is { Count: > 0 })
            {
                AppendElements(element.Children, pane, depth + 1, ref currentY, realizedElements);
            }
        }
    }

    private static string ResolveBindingLabel(PaneElementBindingDescriptor? binding)
    {
        return binding is null
            ? "No binding"
            : $"{binding.TargetKind} · {binding.TargetRef}";
    }

    private static double ResolveWidth(PaneElementDescriptor element)
    {
        return element.ElementKind switch
        {
            PaneElementKind.DefinitionHeader => 420,
            PaneElementKind.CommandBar => 420,
            PaneElementKind.Section => 420,
            PaneElementKind.InspectorGroup => 440,
            PaneElementKind.FilterBar => 320,
            PaneElementKind.LabelValueField => 300,
            PaneElementKind.TextBlock => 320,
            PaneElementKind.Button => 220,
            PaneElementKind.StatusBadge => 220,
            PaneElementKind.TextEditor => 500,
            PaneElementKind.PropertyEditor => 500,
            PaneElementKind.ProjectionStatusView => 520,
            PaneElementKind.RuntimeActivityPanel => 520,
            PaneElementKind.EventFeed => 520,
            PaneElementKind.StreamConsole => 520,
            PaneElementKind.TaskMonitor => 480,
            PaneElementKind.ListBrowser => 520,
            PaneElementKind.ResourceBrowser => 520,
            PaneElementKind.CommandBrowser => 520,
            PaneElementKind.CapabilityBrowser => 520,
            PaneElementKind.ArchiveBrowser => 520,
            PaneElementKind.TreeBrowser => 540,
            PaneElementKind.TableBrowser => 560,
            PaneElementKind.MetricsReadout => 320,
            PaneElementKind.TabsHost => 580,
            PaneElementKind.SplitHost => 620,
            _ => 320
        };
    }

    private static double ResolveHeight(PaneElementDescriptor element)
    {
        return element.ElementKind switch
        {
            PaneElementKind.DefinitionHeader => 72,
            PaneElementKind.CommandBar => 70,
            PaneElementKind.Section => 84,
            PaneElementKind.InspectorGroup => 96,
            PaneElementKind.FilterBar => 58,
            PaneElementKind.LabelValueField => 58,
            PaneElementKind.TextBlock => 58,
            PaneElementKind.Button => 48,
            PaneElementKind.StatusBadge => 40,
            PaneElementKind.TextEditor => 120,
            PaneElementKind.PropertyEditor => 128,
            PaneElementKind.ProjectionStatusView => 120,
            PaneElementKind.RuntimeActivityPanel => 140,
            PaneElementKind.EventFeed => 140,
            PaneElementKind.StreamConsole => 140,
            PaneElementKind.TaskMonitor => 110,
            PaneElementKind.ListBrowser => 120,
            PaneElementKind.ResourceBrowser => 120,
            PaneElementKind.CommandBrowser => 120,
            PaneElementKind.CapabilityBrowser => 120,
            PaneElementKind.ArchiveBrowser => 120,
            PaneElementKind.TreeBrowser => 132,
            PaneElementKind.TableBrowser => 132,
            PaneElementKind.MetricsReadout => 72,
            PaneElementKind.TabsHost => 168,
            PaneElementKind.SplitHost => 184,
            _ => 72
        };
    }
}
