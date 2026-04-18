using Avalonia.Controls;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneInteractionController
    {
        public static void AttachInteractions(
            Border chrome,
            Canvas canvas,
            FloatingPaneSurfaceController surfaceController)
        {
            if (!chrome.Classes.Contains("floatingPaneInteractive"))
            {
                chrome.Classes.Add("floatingPaneInteractive");
                chrome.PointerPressed += (_, __) => surfaceController.BringToFront(chrome);
            }

            if (chrome.Child is not Panel panel)
            {
                return;
            }

            FloatingPaneResizeController.AttachResizeGrips(
                chrome,
                canvas,
                panel,
                surfaceController);
        }
    }
}
