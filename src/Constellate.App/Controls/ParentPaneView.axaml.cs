using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Constellate.App.Controls.Panes;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        private ScrollViewer? _commandBarScroll;
        private PaneChrome? _root;

        public ParentPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _commandBarScroll = this.FindControl<ScrollViewer>("CommandBarScroll");
            _root = this.FindControl<PaneChrome>("ParentChrome");

            // Wire body-region hover so empty body space (not over child/splitter) lights the outer shell halo.
            var bodyRegion = _root?.BodyRegionControl;
            if (bodyRegion is not null)
            {
                bodyRegion.PointerEntered += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerMoved += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerExited += BodyRegion_OnPointerExited;
                bodyRegion.PointerPressed += BodyRegion_OnPointerPressed;
            }
        }

        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e))
            {
                e.Handled = true;
            }
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            PaneChromeInputHelper.TryHandleEmptyHeaderDoubleTap(this, DataContext, e);
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void OnPaneChromePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Route wheel input over the pane shell (typically header region) to the command bar scroll viewer.
            var scroll = this.FindControl<ScrollViewer>("CommandBarScroll");
            if (scroll is null)
            {
                return;
            }

            if (Math.Abs(e.Delta.Y) > 0.01)
            {
                var current = scroll.Offset;
                // Map vertical wheel to horizontal scroll: positive delta.Y (wheel up) scrolls left, negative scrolls right.
                var deltaX = -e.Delta.Y * 40;
                scroll.Offset = new Vector(current.X + deltaX, current.Y);
                e.Handled = true;
            }
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_commandBarScroll is null)
            {
                return;
            }

            var dx = -e.Delta.Y * 24.0;
            _commandBarScroll.Offset = new Vector(_commandBarScroll.Offset.X + dx, 0);
            e.Handled = true;
        }

        // Body-region hover helpers: light halo only when over empty parent body (not over child panes or splitters).
        private void BodyRegion_OnPointerEnteredOrMoved(object? sender, PointerEventArgs e)
        {
            if (_root is null)
            {
                return;
            }

            // Determine the deepest visual under the pointer and see if it lives inside a ChildPaneView or GridSplitter.
            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinChildOrSplitter(srcVisual))
            {
                // Over a child or splitter – do not show shell-level drag-hover.
                PaneChromeInputHelper.SetPaneDragHover(_root, PaneChromeRegion.Body, false);
                return;
            }

            // Empty body surface – show shell-level halo to advertise valid drag-start.
            PaneChromeInputHelper.SetPaneDragHover(_root, PaneChromeRegion.Body, true);
        }

        private void BodyRegion_OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (_root is null)
            {
                return;
            }

            PaneChromeInputHelper.SetPaneDragHover(_root, PaneChromeRegion.Body, false);
        }

        // Begin a pane-centric parent-drag from empty body (not over child/splitter).
        private void BodyRegion_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_root is null)
            {
                return;
            }

            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinChildOrSplitter(srcVisual))
            {
                return;
            }

            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e)) e.Handled = true;
        }

        private static bool IsWithinChildOrSplitter(Visual? v)
        {
            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (cur is ChildPaneView || cur is GridSplitter) return true;
            }
            return false;
        }
    }
}
