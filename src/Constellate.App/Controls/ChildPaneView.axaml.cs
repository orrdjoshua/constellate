using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Constellate.App.Controls.Panes;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;
using Avalonia.Threading;
using Avalonia.Layout;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        private PaneChrome? _root;
        private ScrollViewer? _headerScroll;
        private ChildPaneDescriptor? _model;

        public ChildPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<PaneChrome>("ChildChrome");
            _headerScroll = _root?.FindControl<ScrollViewer>("PART_HeaderScroll");
            Debug.WriteLine($"[HeaderScroll][Child][Wire] init: chrome={_root is not null} headerScroll={_headerScroll is not null}");

            // React to item replacement in the ItemsControl:
            // ChildPaneDescriptor is a record; VM replaces the instance in the collection,
            // which triggers DataContextChanged for this view.
            DataContextChanged += OnDataContextChanged;
            // Track header-height to enforce min size: header must always remain fully visible.
            if (_root?.HeaderBorder is { } hdr)
                hdr.PropertyChanged += OnHeaderMetricChanged;

            // Also run on initial attach (in case DataContext was available before this view attached).
            AttachedToVisualTree += (_, __) => Dispatcher.UIThread.Post(ApplyFloatingMinimizedWidthIfNeeded, DispatcherPriority.Background);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            _model = DataContext as ChildPaneDescriptor;
            // Defer to ensure region measurements are valid before computing desired width.
            Dispatcher.UIThread.Post(ApplyFloatingMinimizedWidthIfNeeded, DispatcherPriority.Background);
            // Apply min-height rule initially after data context update
            Dispatcher.UIThread.Post(ApplyMinHeightFromHeader, DispatcherPriority.Background);
        }

        private void OnHeaderMetricChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty ||
                e.Property?.Name == "Bounds" ||
                e.Property == BoundsProperty)
            {
                ApplyMinHeightFromHeader();
            }
        }

        private void ApplyMinHeightFromHeader()
        {
            if (_root?.HeaderBorder is null) return;
            var headerH = _root.HeaderBorder.Bounds.Height;
            // Provide a small cushion for borders/padding; header must remain visible.
            MinHeight = Math.Max(1.0, headerH + 2.0);
        }

        private void ApplyFloatingMinimizedWidthIfNeeded()
        {
            if (_root is null || _model is null) return;

            // Only apply to floating minimized children (ParentId == null && IsMinimized)
            if (_model.ParentId is not null || !_model.IsMinimized) return;

            try
            {
                var desired = ComputeMinimizedHeaderDesiredWidth(_root);
                const double headerOnlyHeight = 56.0;
                desired = Math.Max(120.0, desired);
                var vm = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
                vm?.SetFloatingChildGeometry(_model.Id, _model.FloatingX, _model.FloatingY, desired, headerOnlyHeight);
                Debug.WriteLine($"[ChildPaneView] minimized floating child width set -> {desired:0.0} (id={_model.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChildPaneView] minimized width compute error: {ex.Message}");
            }
        }

        private static double ComputeMinimizedHeaderDesiredWidth(PaneChrome chrome)
        {
            // Measure: Label + CommandBar + trailing spacer (8), plus header/root padding and border thickness.
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

        private void Body_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e))
            {
                e.Handled = true;
            }
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void Body_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Body_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void OnPaneChromePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_root is null || _headerScroll is null)
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: missing refs chrome={_root is not null} headerScroll={_headerScroll is not null}");
                return;
            }

            // Ignore if pointer is over body region; header-only behavior.
            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinBodyRegion(srcVisual))
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) region=Body ignored");
                return;
            }

            // Prefer native horizontal (Delta.X), else map vertical (Delta.Y) to horizontal.
            double dx;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
            {
                dx = e.Delta.X;
                Debug.WriteLine($"[HeaderScroll][Child] dxFrom=X dx={dx:0.00}");
            }
            else
            {
                dx = -e.Delta.Y;
                Debug.WriteLine($"[HeaderScroll][Child] dxFrom=Y dx={dx:0.00}");
            }

            if (Math.Abs(dx) < 0.01)
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: negligible dx; ignored");
                return;
            }

            var current = _headerScroll.Offset;
            var factor = 20.0; // lower sensitivity
            var extent = _headerScroll.Extent;
            var viewport = _headerScroll.Viewport;
            var maxX = Math.Max(0.0, extent.Width - viewport.Width);
            var nextX = Math.Clamp(current.X + dx * factor, 0.0, maxX);

            if (Math.Abs(nextX - current.X) > 0.5)
            {
                _headerScroll.Offset = new Vector(nextX, current.Y);
                e.Handled = true;
                Debug.WriteLine($"[HeaderScroll][Child] applied: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} factor={factor}");
            }
            else
            {
                Debug.WriteLine($"[HeaderScroll][Child] noop: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} (no change)");
            }
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            OnPaneChromePointerWheelChanged(sender, e);
            Debug.WriteLine($"[HeaderScroll][Child][CmdBar] delegated wheel delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00})");
        }

        private bool IsWithinBodyRegion(Visual? v)
        {
            if (_root?.BodyRegionControl is null)
            {
                Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: bodyRegion=null");
                return false;
            }

            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (ReferenceEquals(cur, _root.BodyRegionControl))
                {
                    Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: hit BodyRegion");
                    return true;
                }
            }
            Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: not in body");
            return false;
        }
    }
}
