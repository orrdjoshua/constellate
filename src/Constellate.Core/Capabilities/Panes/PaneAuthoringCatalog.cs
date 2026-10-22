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

public sealed record PaneAuthoringBundleElementSpec(
    string ElementTypeId,
    PaneElementKind ElementKind,
    string DisplayLabel,
    string Description,
    double DefaultWidth,
    double DefaultHeight,
    IReadOnlyList<PaneCapabilityHostClass> DefaultHostClasses,
    string? DefaultCapabilityId = null);

public sealed record PaneAuthoringBundleCatalogEntry(
    string BundleId,
    string DisplayLabel,
    string Description,
    IReadOnlyList<PaneAuthoringBundleElementSpec> Elements);

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

    private static readonly PaneAuthoringBundleCatalogEntry[] BundleEntries =
    [
        CreateSceneOverviewBasicsBundle(),
        CreateAuthoringQuickControlsBundle()
    ];

    private static PaneAuthoringBundleCatalogEntry CreateSceneOverviewBasicsBundle()
    {
        PaneAuthoringElementCatalogEntry FindElement(string elementTypeId)
        {
            var entry = ElementEntries.FirstOrDefault(candidate =>
                string.Equals(candidate.ElementTypeId, elementTypeId, StringComparison.Ordinal));
            if (entry is null)
            {
                throw new InvalidOperationException($"Authoring element catalog entry not found for '{elementTypeId}'.");
            }

            return entry;
        }

        var textBlock = FindElement("authoring.element.text_block");
        var labelValue = FindElement("authoring.element.label_value");

        return new PaneAuthoringBundleCatalogEntry(
            "authoring.bundle.scene_overview",
            "Scene Overview Basics",
            "Starter bundle with a heading and two label/value readout slots for scene- or selection-related metrics.",
            [
                new PaneAuthoringBundleElementSpec(
                    textBlock.ElementTypeId,
                    textBlock.ElementKind,
                    "Scene Overview",
                    "Canvas heading element for scene overview bundles.",
                    textBlock.DefaultWidth,
                    textBlock.DefaultHeight,
                    textBlock.DefaultHostClasses),
                new PaneAuthoringBundleElementSpec(
                    labelValue.ElementTypeId,
                    labelValue.ElementKind,
                    "Primary Metric",
                    "First label/value slot for scene or selection metrics.",
                    labelValue.DefaultWidth,
                    labelValue.DefaultHeight,
                    labelValue.DefaultHostClasses),
                new PaneAuthoringBundleElementSpec(
                    labelValue.ElementTypeId,
                    labelValue.ElementKind,
                    "Secondary Metric",
                    "Second label/value slot for scene or selection metrics.",
                    labelValue.DefaultWidth,
                    labelValue.DefaultHeight,
                    labelValue.DefaultHostClasses)
            ]);
    }

    private static PaneAuthoringBundleCatalogEntry CreateAuthoringQuickControlsBundle()
    {
        PaneAuthoringElementCatalogEntry Find(string id)
        {
            var entry = ElementEntries.FirstOrDefault(e => string.Equals(e.ElementTypeId, id, StringComparison.Ordinal));
            if (entry is null)
                throw new InvalidOperationException($"Authoring element catalog entry not found for '{id}'.");
            return entry;
        }

        var text = Find("authoring.element.text_block");
        var bar = Find("authoring.element.command_bar");
        var badge = Find("authoring.element.status_badge");

        return new PaneAuthoringBundleCatalogEntry(
            "authoring.bundle.quick_controls",
            "Authoring Quick Controls",
            "Heading + action strip + status badge to quickly seed authoring controls onto the canvas.",
            [
                new PaneAuthoringBundleElementSpec(
                    text.ElementTypeId,
                    text.ElementKind,
                    "Quick Controls",
                    "Canvas heading element for quick controls.",
                    text.DefaultWidth,
                    text.DefaultHeight,
                    text.DefaultHostClasses),
                new PaneAuthoringBundleElementSpec(
                    bar.ElementTypeId,
                    bar.ElementKind,
                    "Actions",
                    "Action strip for authoring operations (unbound by default).",
                    bar.DefaultWidth,
                    bar.DefaultHeight,
                    bar.DefaultHostClasses),
                new PaneAuthoringBundleElementSpec(
                    badge.ElementTypeId,
                    badge.ElementKind,
                    "Status",
                    "Compact status/readout badge (unbound by default).",
                    badge.DefaultWidth,
                    badge.DefaultHeight,
                    badge.DefaultHostClasses)
            ]);
    }

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

    public static IReadOnlyList<PaneAuthoringBundleCatalogEntry> GetBundleEntries()
    {
        return BundleEntries;
    }

    public static bool TryGetBundleEntry(
        string bundleId,
        out PaneAuthoringBundleCatalogEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        var normalized = bundleId.Trim();

        var resolvedEntry = BundleEntries.FirstOrDefault(candidate =>
            string.Equals(candidate.BundleId, normalized, StringComparison.Ordinal));
        if (resolvedEntry is null)
        {
            entry = null!;
            return false;
        }

        entry = resolvedEntry;
        return true;
    }
}
