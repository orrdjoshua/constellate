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
            // Only act for header: ignore if the event is within the body region.
            if (_root is null || _commandBarScroll is null)
            {
                return;
            }

            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinBodyRegion(srcVisual))
            {
                return;
            }

            // Prefer native horizontal scrolling when present; otherwise map vertical wheel to horizontal.
            double dx;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
            {
                dx = e.Delta.X;           // two‑finger side scroll on trackpads
            }
            else
            {
                dx = -e.Delta.Y;          // wheel/trackpad vertical → horizontal
            }

            if (Math.Abs(dx) < 0.01)
            {
                return;
            }

            var current = _commandBarScroll.Offset;
            var factor = 40.0; // sensitivity multiplier

            // Clamp to the ScrollViewer’s content width.
            var extent = _commandBarScroll.Extent;
            var viewport = _commandBarScroll.Viewport;
            var maxX = Math.Max(0.0, extent.Width - viewport.Width);
            var nextX = Math.Clamp(current.X + dx * factor, 0.0, maxX);

            if (Math.Abs(nextX - current.X) > 0.5)
            {
                _commandBarScroll.Offset = new Vector(nextX, current.Y);
                e.Handled = true;
            }
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_commandBarScroll is null)
            {
                return;
            }

            // Delegate to the same logic as header-wide scrolling (handles X or Y).
            OnPaneChromePointerWheelChanged(sender, e);
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

        private bool IsWithinBodyRegion(Visual? v)
        {
            if (_root?.BodyRegionControl is null)
            {
                return false;
            }

            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (ReferenceEquals(cur, _root.BodyRegionControl))
                {
                    return true;
                }
            }
            return false;
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
