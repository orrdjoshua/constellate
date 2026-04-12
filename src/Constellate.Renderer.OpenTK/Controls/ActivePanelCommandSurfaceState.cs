using System;
using Constellate.Renderer.OpenTK.Scene;

namespace Constellate.Renderer.OpenTK.Controls
{
    internal sealed class ActivePanelCommandSurfaceState
    {
        public string? NodeId { get; private set; }
        public string? ViewRef { get; private set; }
        public int CommandIndex { get; private set; }

        public bool HasValue =>
            !string.IsNullOrWhiteSpace(NodeId) &&
            !string.IsNullOrWhiteSpace(ViewRef);

        public bool Matches(PanelSurfaceNode panel) =>
            string.Equals(NodeId, panel.NodeId, StringComparison.Ordinal) &&
            string.Equals(ViewRef, panel.ViewRef, StringComparison.Ordinal);

        public void Set(PanelSurfaceNode panel, int commandIndex, int commandCount)
        {
            NodeId = panel.NodeId;
            ViewRef = panel.ViewRef;
            CommandIndex = commandCount <= 0
                ? 0
                : Math.Clamp(commandIndex, 0, commandCount - 1);
        }

        public void Advance(int delta, int commandCount)
        {
            if (commandCount <= 0)
            {
                CommandIndex = 0;
                return;
            }

            CommandIndex = (CommandIndex + delta + commandCount) % commandCount;
        }

        public void Clamp(int commandCount)
        {
            if (commandCount <= 0)
            {
                CommandIndex = 0;
                return;
            }

            CommandIndex = Math.Clamp(CommandIndex, 0, commandCount - 1);
        }

        public void Clear()
        {
            NodeId = null;
            ViewRef = null;
            CommandIndex = 0;
        }
    }
}
