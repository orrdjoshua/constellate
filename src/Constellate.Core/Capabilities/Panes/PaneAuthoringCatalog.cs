using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Capabilities.Panes;

public sealed record PaneAuthoringElementCatalogEntry(
    string ElementTypeId,
    PaneElementKind ElementKind,
    string DisplayLabel,
    string Description,
    double DefaultWidth,
    double DefaultHeight,
    IReadOnlyList<PaneCapabilityHostClass> DefaultHostClasses);

public static class PaneAuthoringCatalog
{
    private static readonly PaneAuthoringElementCatalogEntry[] ElementEntries =
    [
        new(
            "authoring.element.text_block",
            PaneElementKind.TextBlock,
            "Text Block",
            "Static or bound readout text placed directly on the authored canvas.",
            240,
            72,
            [PaneCapabilityHostClass.TextDisplayHost]),

        new(
            "authoring.element.button",
            PaneElementKind.Button,
            "Button",
            "Invocation surface for commands or later capability bindings.",
            180,
            56,
            [PaneCapabilityHostClass.InvocationHost]),

        new(
            "authoring.element.label_value",
            PaneElementKind.LabelValueField,
            "Label / Value",
            "Compact readout for state, summary, or inspector-style values.",
            280,
            64,
            [PaneCapabilityHostClass.TextDisplayHost, PaneCapabilityHostClass.InspectorHost]),

        new(
            "authoring.element.text_editor",
            PaneElementKind.TextEditor,
            "Notes / Editor",
            "Larger text region for editor or authored-note style surfaces.",
            340,
            132,
            [PaneCapabilityHostClass.TextInputHost, PaneCapabilityHostClass.TextDisplayHost]),

        new(
            "authoring.element.command_bar",
            PaneElementKind.CommandBar,
            "Command Bar",
            "Horizontal action strip for grouped invocation surfaces.",
            340,
            72,
            [PaneCapabilityHostClass.CommandBarHost, PaneCapabilityHostClass.InvocationHost]),

        new(
            "authoring.element.status_badge",
            PaneElementKind.StatusBadge,
            "Status Badge",
            "Compact badge/readout surface for small state summaries.",
            180,
            44,
            [PaneCapabilityHostClass.StatusBadgeHost])
    ];

    public static IReadOnlyList<PaneAuthoringElementCatalogEntry> GetElementEntries()
    {
        return ElementEntries;
    }

    public static bool TryGetElementEntry(
        PaneElementKind elementKind,
        out PaneAuthoringElementCatalogEntry entry)
    {
        var resolvedEntry = ElementEntries.FirstOrDefault(candidate => candidate.ElementKind == elementKind);
        if (resolvedEntry is null)
        {
            entry = null!;
            return false;
        }

        entry = resolvedEntry;
        return true;
    }
}
