using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Scene
{
    public sealed record PanelCommandDescriptor(
        string CommandId,
        string DisplayLabel)
    {
        public static PanelCommandDescriptor? Create(string? commandId, string? displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return null;
            }

            var normalizedCommandId = commandId.Trim();
            var normalizedDisplayLabel = string.IsNullOrWhiteSpace(displayLabel)
                ? normalizedCommandId
                : displayLabel.Trim();

            return new PanelCommandDescriptor(normalizedCommandId, normalizedDisplayLabel);
        }
    }

    public sealed record PanelCommandSurfaceMetadata(
        string SurfaceName,
        string SurfaceGroup,
        IReadOnlyList<PanelCommandDescriptor> Commands,
        string SurfaceSource = "engine")
    {
        public bool HasCommands => Commands.Count > 0;
        public bool IsEngineDefined =>
            string.Equals(SurfaceSource, "engine", StringComparison.Ordinal);

        public IReadOnlyList<string> CommandIds =>
            Commands.Select(command => command.CommandId).ToArray();

        public string DescribeIdentity() =>
            $"{SurfaceName}/{SurfaceGroup}/{SurfaceSource}";

        public static PanelCommandSurfaceMetadata? FromPayload(
            string? surfaceName,
            string? surfaceGroup,
            IReadOnlyList<string>? commandIds,
            string? surfaceSource = null,
            IReadOnlyList<PanelCommandDescriptor>? commands = null)
        {
            var normalizedSurfaceName = string.IsNullOrWhiteSpace(surfaceName)
                ? null
                : surfaceName.Trim();
            var normalizedSurfaceGroup = string.IsNullOrWhiteSpace(surfaceGroup)
                ? "default"
                : surfaceGroup.Trim();
            var normalizedSurfaceSource = string.IsNullOrWhiteSpace(surfaceSource)
                ? "app"
                : surfaceSource.Trim().ToLowerInvariant();
            var normalizedCommands = NormalizeCommands(commands, commandIds);

            if (normalizedSurfaceName is null && normalizedCommands.Length == 0)
            {
                return null;
            }

            return new PanelCommandSurfaceMetadata(
                normalizedSurfaceName ?? "default",
                normalizedSurfaceGroup,
                normalizedCommands,
                normalizedSurfaceSource);
        }

        public string DescribeSummary()
        {
            return HasCommands
                ? $"{DescribeIdentity()}: {DescribeCommandsSummary()}"
                : $"{DescribeIdentity()}: no commands";
        }

        public string DescribeCommandsSummary(int maxCount = int.MaxValue)
        {
            return string.Join(", ", Commands
                .Take(Math.Max(1, maxCount))
                .Select(command => command.DisplayLabel));
        }

        private static PanelCommandDescriptor[] NormalizeCommands(
            IReadOnlyList<PanelCommandDescriptor>? commands,
            IReadOnlyList<string>? commandIds)
        {
            var normalized = new List<PanelCommandDescriptor>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var command in commands ?? Array.Empty<PanelCommandDescriptor>())
            {
                var normalizedCommand = PanelCommandDescriptor.Create(command.CommandId, command.DisplayLabel);
                if (normalizedCommand is not null && seen.Add(normalizedCommand.CommandId))
                {
                    normalized.Add(normalizedCommand);
                }
            }

            if (normalized.Count == 0)
            {
                foreach (var commandId in commandIds ?? Array.Empty<string>())
                {
                    var normalizedCommand = PanelCommandDescriptor.Create(commandId);
                    if (normalizedCommand is not null && seen.Add(normalizedCommand.CommandId))
                    {
                        normalized.Add(normalizedCommand);
                    }
                }
            }

            return normalized.ToArray();
        }
    }
}
