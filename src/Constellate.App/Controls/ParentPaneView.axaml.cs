using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Constellate.App.Controls.Panes;
using System.Diagnostics;
using Avalonia.Threading;
using System.Linq;
using Avalonia.Interactivity;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        private const double SlideScrollThreshold = 2.0;
        private double _pendingHorizontalSlideDelta;
        private double _pendingVerticalSlideDelta;

        private ScrollViewer? _headerScroll;
        private PaneChrome? _root;
        private TextBox? _inlineRenameEditor;
        private ParentPaneModel? _model;
        private Point _lastBodyContextPoint;
        private bool _hasBodyContextPoint;

        public ParentPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<PaneChrome>("ParentChrome");
            _inlineRenameEditor = this.FindControl<TextBox>("InlineRenameEditor");
            _headerScroll = _root?.FindControl<ScrollViewer>("PART_HeaderScroll");
            Debug.WriteLine($"[HeaderScroll][Parent][Wire] init: chrome={_root is not null} headerScroll={_headerScroll is not null}");

            var bodyRegion = _root?.BodyRegionControl;
            if (bodyRegion is not null)
            {
                bodyRegion.PointerEntered += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerMoved += BodyRegion_OnPointerEnteredOrMoved;
                bodyRegion.PointerExited += BodyRegion_OnPointerExited;
                bodyRegion.PointerPressed += BodyRegion_OnPointerPressed;
                Debug.WriteLine($"[HeaderScroll][Parent][Wire] bodyHook=True");
            }

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
            QueueInlineRenameRefresh();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_model is null || _root is null) return;
            if (!string.Equals(e.PropertyName, nameof(ParentPaneModel.IsMinimized), StringComparison.Ordinal)) return;
            if (!_model.IsMinimized) return;
            if (string.Equals(e.PropertyName, nameof(ParentPaneModel.IsInlineRenaming), StringComparison.Ordinal))
            {
                QueueInlineRenameRefresh();
                return;
            }

            if (!string.Equals(e.PropertyName, nameof(ParentPaneModel.IsMinimized), StringComparison.Ordinal)) return;
            if (!string.Equals(MainWindowViewModel.NormalizeHostId(_model.HostId), "floating", StringComparison.Ordinal)) return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var desired = ComputeMinimizedHeaderDesiredWidth(_root);
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

        private void QueueInlineRenameRefresh()
        {
            Dispatcher.UIThread.Post(SyncInlineRenameEditor, DispatcherPriority.Input);
        }

        private void SyncInlineRenameEditor()
        {
            if (_inlineRenameEditor is null || _model is null || !_model.IsInlineRenaming)
            {
                return;
            }

            _inlineRenameEditor.Text = _model.Title;
            _inlineRenameEditor.Focus();
            _inlineRenameEditor.SelectAll();
        }

        private void CommitInlineRename()
        {
            if (_model is null)
            {
                return;
            }

            PaneChromeInputHelper.ResolveMainWindowViewModel(this)?.TryCommitPaneRename(_model.Id, _inlineRenameEditor?.Text);
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

        private void OnInlineRenameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInlineRename();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && _model is not null)
            {
                PaneChromeInputHelper.ResolveMainWindowViewModel(this)?.TryCancelPaneRename(_model.Id);
                e.Handled = true;
            }
        }

        private void OnInlineRenameLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_model?.IsInlineRenaming == true)
            {
                CommitInlineRename();
            }
        }

        private void OnPaneChromePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_root is null)
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: missing chrome");
                return;
            }

            var srcVisual = (e.Source as Visual) ?? (sender as Visual);

            if (IsWithinBodyRegion(srcVisual))
            {
                if (_model is null)
                {
                    Debug.WriteLine("[Slide][Parent] wheel: no model; ignoring");
                    return;
                }

                var vm = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
                if (vm is null)
                {
                    Debug.WriteLine("[Slide][Parent] wheel: no VM; ignoring");
                    return;
                }

                var host = MainWindowViewModel.NormalizeHostId(_model.HostId);
                var step = ResolveSlideStepFromBodyWheel(host, e);

                if (step != 0)
                {
                    var curSlide = _model.SlideIndex;
                    var next = Math.Clamp(curSlide + step, 0, 2);
                    if (next != curSlide)
                    {
                        vm.SetParentSlideIndex(_model.Id, next);
                        Debug.WriteLine($"[Slide][Parent] host={host} step={step} {curSlide}→{next}");
                        e.Handled = true;
                        return;
                    }
                }

                return;
            }

            if (_headerScroll is null)
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: missing headerScroll");
                return;
            }

            double dx;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
            {
                dx = e.Delta.X;
                Debug.WriteLine($"[HeaderScroll][Parent] dxFrom=X dx={dx:0.00}");
            }
            else
            {
                dx = -e.Delta.Y;
                Debug.WriteLine($"[HeaderScroll][Parent] dxFrom=Y dx={dx:0.00}");
            }

            if (Math.Abs(dx) < 0.01)
            {
                Debug.WriteLine($"[HeaderScroll][Parent] wheel: negligible dx; ignored");
                return;
            }

            var headerOffset = _headerScroll.Offset;
            var factor = 20.0;

            var extent = _headerScroll.Extent;
            var viewport = _headerScroll.Viewport;
            var maxX = Math.Max(0.0, extent.Width - viewport.Width);
            var nextX = Math.Clamp(headerOffset.X + dx * factor, 0.0, maxX);

            if (Math.Abs(nextX - headerOffset.X) > 0.5)
            {
                _headerScroll.Offset = new Vector(nextX, headerOffset.Y);
                e.Handled = true;
                Debug.WriteLine($"[HeaderScroll][Parent] applied: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={headerOffset.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} factor={factor}");
            }
            else
            {
                Debug.WriteLine($"[HeaderScroll][Parent] noop: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={headerOffset.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} (no change)");
            }
        }

        private int ResolveSlideStepFromBodyWheel(string host, PointerWheelEventArgs e)
        {
            // UX intent:
            // - Left/Right docked parents:
            //   * vertical wheel/two-finger scroll belongs to lane scrolling only,
            //   * slide navigation should require horizontal intent only.
            // - Top/Bottom docked parents:
            //   * vertical wheel/two-finger scroll advances slides.
            //
            // We also reduce sensitivity by requiring accumulated wheel intent before
            // advancing a slide, instead of flipping on tiny deltas.
            if (string.Equals(host, "left", StringComparison.Ordinal) ||
                string.Equals(host, "right", StringComparison.Ordinal))
            {
                if (Math.Abs(e.Delta.X) < 0.01)
                {
                    return 0;
                }

                _pendingHorizontalSlideDelta += e.Delta.X;
                if (Math.Abs(_pendingHorizontalSlideDelta) < SlideScrollThreshold)
                {
                    return 0;
                }

                var step = _pendingHorizontalSlideDelta > 0 ? 1 : -1;
                _pendingHorizontalSlideDelta -= step * SlideScrollThreshold;
                return step;
            }

            if (Math.Abs(e.Delta.Y) < 0.01)
            {
                return 0;
            }

            _pendingVerticalSlideDelta += e.Delta.Y;
            if (Math.Abs(_pendingVerticalSlideDelta) < SlideScrollThreshold)
            {
                return 0;
            }

            var verticalStep = _pendingVerticalSlideDelta < 0 ? 1 : -1;
            _pendingVerticalSlideDelta -= -verticalStep * SlideScrollThreshold;
            return verticalStep;
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            OnPaneChromePointerWheelChanged(sender, e);
            Debug.WriteLine($"[HeaderScroll][Parent][CmdBar] delegated wheel delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00})");
        }

        private void BodyRegion_OnPointerEnteredOrMoved(object? sender, PointerEventArgs e)
        {
            if (_root is null)
            {
                return;
            }

            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinChildOrSplitter(srcVisual))
            {
                PaneChromeInputHelper.SetPaneDragHover(_root, PaneChromeRegion.Body, false);
                return;
            }

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

            // Capture right-click for 'Create child here' context menu targeting.
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsRightButtonPressed)
            {
                // Store point relative to the body region so calculations align to visible body.
                var body = _root.BodyRegionControl as Control;
                if (body is not null)
                {
                    _lastBodyContextPoint = e.GetPosition(body);
                    _hasBodyContextPoint = true;
                }
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

        private void CreateChildHere_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_root is null || _model is null || !_hasBodyContextPoint)
            {
                return;
            }

            var vm = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
            if (vm is null)
            {
                return;
            }

            var body = _root.BodyRegionControl as Control;
            if (body is null)
            {
                return;
            }

            // Visible body viewport in control space
            var bodyBounds = body.Bounds;
            if (bodyBounds.Width <= 0 || bodyBounds.Height <= 0)
            {
                return;
            }

            // Determine target lane index by orientation and SplitCount
            var splits = Math.Max(1, Math.Min(3, _model.SplitCount));
            var laneIndex = 0;
            if (_model.IsVerticalBodyOrientation)
            {
                var relX = Math.Clamp(_lastBodyContextPoint.X / Math.Max(1.0, bodyBounds.Width), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relX * splits), 0, splits - 1);
            }
            else
            {
                var relY = Math.Clamp(_lastBodyContextPoint.Y / Math.Max(1.0, bodyBounds.Height), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relY * splits), 0, splits - 1);
            }

            // Compute insert index using realized FixedSizePixels for children in the target lane
            var insertIndex = ResolveInsertIndexWithRealizedSizes(
                _model,
                bodyBounds,
                _lastBodyContextPoint,
                vm.GetChildrenInLaneForCurrentSlide(_model.Id, laneIndex));

            if (insertIndex < 0)
            {
                // Fallback to equal-slot
                var count = vm.GetChildrenCountInLaneForCurrentSlide(_model.Id, laneIndex);
                insertIndex = ResolveInsertIndexEqualSlots(
                    _model.IsVerticalBodyOrientation,
                    bodyBounds,
                    _lastBodyContextPoint,
                    count);
            }

            vm.CreateChildPaneAt(_model.Id, laneIndex, insertIndex);
            _hasBodyContextPoint = false;
        }

        private static int ResolveInsertIndexEqualSlots(bool isVerticalBodyOrientation, Rect bodyBounds, Point point, int itemCount)
        {
            var slotCount = Math.Max(1, itemCount + 1);
            if (isVerticalBodyOrientation)
            {
                var relY = Math.Clamp(point.Y / Math.Max(1.0, bodyBounds.Height), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relY * slotCount), 0, itemCount);
            }
            else
            {
                var relX = Math.Clamp(point.X / Math.Max(1.0, bodyBounds.Width), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relX * slotCount), 0, itemCount);
            }
        }

        private static int ResolveInsertIndexWithRealizedSizes(ParentPaneModel parent, Rect bodyBounds, Point point, IReadOnlyList<ChildPaneDescriptor> laneChildren)
        {
            if (laneChildren is null || laneChildren.Count == 0)
            {
                return 0;
            }

            // axis position normalized [0..1] along fixed dimension
            double axisRel = parent.IsVerticalBodyOrientation
                ? Math.Clamp(point.Y / Math.Max(1.0, bodyBounds.Height), 0.0, 1.0)
                : Math.Clamp(point.X / Math.Max(1.0, bodyBounds.Width), 0.0, 1.0);

            var sizes = laneChildren.Select(c => Math.Max(1.0, c.FixedSizePixels)).ToArray();
            var total = sizes.Sum();
            if (total <= 0.0)
            {
                return -1;
            }

            var cumul = new System.Collections.Generic.List<double>(sizes.Length + 1) { 0.0 };
            double running = 0.0;
            foreach (var s in sizes)
            {
                running += s;
                cumul.Add(running / total);
            }

            for (var i = 0; i < cumul.Count - 1; i++)
            {
                var start = cumul[i];
                var end = cumul[i + 1];
                if (axisRel <= start) return i;
                if (axisRel > start && axisRel <= end)
                {
                    var mid = (start + end) / 2.0;
                    return axisRel <= mid ? i : i + 1;
                }
            }
            return cumul.Count - 1;
        }
    }
}
