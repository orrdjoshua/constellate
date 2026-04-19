using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Constellate.App.Controls.Panes;
using System.Diagnostics;
using Avalonia.Threading;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        private ScrollViewer? _headerScroll;
        private PaneChrome? _root;
        private ParentPaneModel? _model;

        public ParentPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<PaneChrome>("ParentChrome");
            _headerScroll = _root?.FindControl<ScrollViewer>("PART_HeaderScroll");
            Debug.WriteLine($"[HeaderScroll][Parent][Wire] init: chrome={_root is not null} headerScroll={_headerScroll is not null}");

            // Wire body-region hover so empty body space (not over child/splitter) lights the outer shell halo.
            var bodyRegion = _root?.BodyRegionControl;
            if (bodyRegion is not null)
            {
                bodyRegion.PointerEntered += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerMoved += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerExited += BodyRegion_OnPointerExited;
                bodyRegion.PointerPressed += BodyRegion_OnPointerPressed;
                Debug.WriteLine($"[HeaderScroll][Parent][Wire] bodyHook=True");
            }

            // Track model changes so we can react to minimize events in floating context.
            DataContextChanged += OnDataContextChanged;
            HookModel();
        }

        private void OnDataContextChanged(object? sender, EventArgs e) => HookModel();

        private void HookModel()
        {
            if (_model is INotifyPropertyChanged oldPc)
            {
                oldPc.PropertyChanged -= OnModelPropertyChanged;
            }

            _model = DataContext as ParentPaneModel;
            if (_model is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnModelPropertyChanged;
            }
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_model is null || _root is null) return;
            if (!string.Equals(e.PropertyName, nameof(ParentPaneModel.IsMinimized), StringComparison.Ordinal)) return;
            if (!_model.IsMinimized) return;
            // Only apply auto-width logic for floating parents.
            if (!string.Equals(MainWindowViewModel.NormalizeHostId(_model.HostId), "floating", StringComparison.Ordinal)) return;

            // Defer to ensure header layout metrics are valid.
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var desired = ComputeMinimizedHeaderDesiredWidth(_root);
                    // Reasonable floor to avoid collapsing in edge cases
                    desired = Math.Max(120.0, desired);
                    _model.FloatingWidth = desired;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ParentPaneView] minimized width compute error: {ex.Message}");
                }
            }, DispatcherPriority.Background);
        }

        private static double ComputeMinimizedHeaderDesiredWidth(PaneChrome chrome)
        {
            // Label + CommandBar + trailing spacer (8), plus header + root paddings and borders
            var labelW = chrome.LabelRegionControl?.Bounds.Width ?? 0.0;
            var cmdW = chrome.CommandBarRegionControl?.Bounds.Width ?? 0.0;
            const double trailing = 8.0;

            var headerPad = chrome.HeaderBorder?.Padding ?? new Thickness(0);
            var rootPad = chrome.RootBorder?.Padding ?? new Thickness(0);
            var rootBorder = chrome.RootBorder?.BorderThickness ?? new Thickness(0);

            var sum = labelW + cmdW + trailing
                      + headerPad.Left + headerPad.Right
                      + rootPad.Left + rootPad.Right
                      + rootBorder.Left + rootBorder.Right;
            return sum;
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
            if (_root is null || _headerScroll is null)
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: missing refs chrome={_root is not null} headerScroll={_headerScroll is not null}");
                return;
            }

            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinBodyRegion(srcVisual))
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) region=Body ignored");
                return;
            }

            // Prefer native horizontal scrolling when present; otherwise map vertical wheel to horizontal.
            double dx;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
            {
                dx = e.Delta.X;           // two‑finger side scroll on trackpads
                Debug.WriteLine($"[HeaderScroll][Parent] dxFrom=X dx={dx:0.00}");
            }
            else
            {
                dx = -e.Delta.Y;          // wheel/trackpad vertical → horizontal
                Debug.WriteLine($"[HeaderScroll][Parent] dxFrom=Y dx={dx:0.00}");
            }

            if (Math.Abs(dx) < 0.01)
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: negligible dx; ignored");
                return;
            }

            var current = _headerScroll.Offset;
            var factor = 20.0; // sensitivity multiplier (reduced by ~50%)

            // Clamp to the ScrollViewer’s content width.
            var extent = _headerScroll.Extent;
            var viewport = _headerScroll.Viewport;
            var maxX = Math.Max(0.0, extent.Width - viewport.Width);
            var nextX = Math.Clamp(current.X + dx * factor, 0.0, maxX);

            if (Math.Abs(nextX - current.X) > 0.5)
            {
                _headerScroll.Offset = new Vector(nextX, current.Y);
                e.Handled = true;
                Debug.WriteLine($"[HeaderScroll][Parent] applied: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} factor={factor}");
            }
            else
            {
                Debug.WriteLine($"[HeaderScroll][Parent] noop: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} (no change)");
            }
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Delegate to the same logic as header-wide scrolling (handles X or Y).
            OnPaneChromePointerWheelChanged(sender, e);
            Debug.WriteLine($"[HeaderScroll][Parent][CmdBar] delegated wheel delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00})");
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
                Debug.WriteLine($"[HeaderScroll][Parent] bodyCheck: bodyRegion=null");
                return false;
            }

            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (ReferenceEquals(cur, _root.BodyRegionControl))
                {
                    Debug.WriteLine($"[HeaderScroll][Parent] bodyCheck: hit BodyRegion");
                    return true;
                }
            }
            Debug.WriteLine($"[HeaderScroll][Parent] bodyCheck: not in body");
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
