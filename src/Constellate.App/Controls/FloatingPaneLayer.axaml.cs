using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    public partial class FloatingPaneLayer : UserControl
    {
        public static readonly StyledProperty<IEnumerable<ParentPaneModel>?> ParentPanesProperty =
            AvaloniaProperty.Register<FloatingPaneLayer, IEnumerable<ParentPaneModel>?>(nameof(ParentPanes));

        public static readonly StyledProperty<IEnumerable<ChildPaneDescriptor>?> ChildPanesProperty =
            AvaloniaProperty.Register<FloatingPaneLayer, IEnumerable<ChildPaneDescriptor>?>(nameof(ChildPanes));

        private Canvas? _canvas;
        private INotifyCollectionChanged? _parentCollection;
        private INotifyCollectionChanged? _childCollection;
        private NotifyCollectionChangedEventHandler? _parentCollectionChangedHandler;
        private NotifyCollectionChangedEventHandler? _childCollectionChangedHandler;
        private readonly Dictionary<ParentPaneModel, PropertyChangedEventHandler> _parentHandlers = new();
        private readonly Dictionary<string, Border> _chromeBySurfaceKey = new(StringComparer.Ordinal);
        private int _zCounter = 1;

        public FloatingPaneLayer()
        {
            InitializeComponent();
            AttachedToVisualTree += (_, __) => SynchronizeCanvas();
            DetachedFromVisualTree += (_, __) => DetachAllSubscriptions();
        }

        public IEnumerable<ParentPaneModel>? ParentPanes
        {
            get => GetValue(ParentPanesProperty);
            set => SetValue(ParentPanesProperty, value);
        }

        public IEnumerable<ChildPaneDescriptor>? ChildPanes
        {
            get => GetValue(ChildPanesProperty);
            set => SetValue(ChildPanesProperty, value);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _canvas = this.FindControl<Canvas>("PART_Canvas");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ParentPanesProperty)
            {
                RewireParentSubscriptions(change.GetNewValue<IEnumerable<ParentPaneModel>?>());
                SynchronizeCanvas();
                return;
            }

            if (change.Property == ChildPanesProperty)
            {
                RewireChildSubscriptions(change.GetNewValue<IEnumerable<ChildPaneDescriptor>?>());
                SynchronizeCanvas();
            }
        }

        private void RewireParentSubscriptions(IEnumerable<ParentPaneModel>? parents)
        {
            if (_parentCollection is not null && _parentCollectionChangedHandler is not null)
            {
                _parentCollection.CollectionChanged -= _parentCollectionChangedHandler;
            }

            foreach (var pair in _parentHandlers.ToArray())
            {
                pair.Key.PropertyChanged -= pair.Value;
            }

            _parentHandlers.Clear();
            _parentCollection = parents as INotifyCollectionChanged;

            if (_parentCollection is not null)
            {
                _parentCollectionChangedHandler ??= (_, __) =>
                {
                    AttachParentItemHandlers(ParentPanes);
                    SynchronizeCanvas();
                };

                _parentCollection.CollectionChanged += _parentCollectionChangedHandler;
            }

            AttachParentItemHandlers(parents);
        }

        private void AttachParentItemHandlers(IEnumerable<ParentPaneModel>? parents)
        {
            foreach (var pair in _parentHandlers.ToArray())
            {
                pair.Key.PropertyChanged -= pair.Value;
            }

            _parentHandlers.Clear();

            if (parents is null)
            {
                return;
            }

            foreach (var parent in parents)
            {
                PropertyChangedEventHandler handler = (_, __) => SynchronizeCanvas();
                parent.PropertyChanged += handler;
                _parentHandlers[parent] = handler;
            }
        }

        private void RewireChildSubscriptions(IEnumerable<ChildPaneDescriptor>? children)
        {
            if (_childCollection is not null && _childCollectionChangedHandler is not null)
            {
                _childCollection.CollectionChanged -= _childCollectionChangedHandler;
            }

            _childCollection = children as INotifyCollectionChanged;

            if (_childCollection is not null)
            {
                _childCollectionChangedHandler ??= (_, __) => SynchronizeCanvas();
                _childCollection.CollectionChanged += _childCollectionChangedHandler;
            }
        }

        private void DetachAllSubscriptions()
        {
            if (_parentCollection is not null && _parentCollectionChangedHandler is not null)
            {
                _parentCollection.CollectionChanged -= _parentCollectionChangedHandler;
            }

            if (_childCollection is not null && _childCollectionChangedHandler is not null)
            {
                _childCollection.CollectionChanged -= _childCollectionChangedHandler;
            }

            foreach (var pair in _parentHandlers.ToArray())
            {
                pair.Key.PropertyChanged -= pair.Value;
            }

            _parentHandlers.Clear();
            _parentCollection = null;
            _childCollection = null;
            ClearFloatingChromeCache();
        }

        private void ClearFloatingChromeCache()
        {
            if (_canvas is not null)
            {
                _canvas.Children.Clear();
            }

            _chromeBySurfaceKey.Clear();
        }

        private void SynchronizeCanvas()
        {
            if (_canvas is null)
            {
                return;
            }

            var surfaceModel = FloatingPaneSurfaceBuilder.BuildSurfaceModel(ParentPanes, ChildPanes, ref _zCounter);
            var desiredKeys = new HashSet<string>(
                surfaceModel.Entries.Select(entry => entry.SurfaceKey),
                StringComparer.Ordinal);

            foreach (var entry in surfaceModel.Entries)
            {
                if (!_chromeBySurfaceKey.TryGetValue(entry.SurfaceKey, out var chrome))
                {
                    chrome = FloatingPaneChromeFactory.CreateChromeForEntry(
                        entry,
                        ParentPanes,
                        ChildPanes,
                        TryBringToFront,
                        AttachResizeGrips);

                    if (chrome is null)
                    {
                        continue;
                    }

                    _chromeBySurfaceKey[entry.SurfaceKey] = chrome;
                }
                else
                {
                    FloatingPaneChromeFactory.UpdateChromeDataContext(chrome, entry, ParentPanes, ChildPanes);
                }

                FloatingPaneChromeFactory.ApplySurfaceEntry(chrome, entry);

                if (_canvas.Children.IndexOf(chrome) < 0)
                {
                    _canvas.Children.Add(chrome);
                }
            }

            foreach (var pair in _chromeBySurfaceKey.ToArray())
            {
                if (desiredKeys.Contains(pair.Key))
                {
                    continue;
                }

                RemoveChrome(pair.Key, pair.Value);
            }
        }

        private void RemoveChrome(string surfaceKey, Border chrome)
        {
            if (_canvas is not null)
            {
                _canvas.Children.Remove(chrome);
            }

            _chromeBySurfaceKey.Remove(surfaceKey);
        }

        private void AttachResizeGrips(Border chrome, bool isParent, ParentPaneModel? parent, ChildPaneDescriptor? child)
        {
            if (_canvas is null) return;
            if (chrome.Child is not Panel panel) return;

            void AddGrip(HorizontalAlignment ha, VerticalAlignment va, double w, double h, StandardCursorType cursor,
                         bool left, bool top, bool right, bool bottom)
            {
                var normalBrush = new SolidColorBrush(Color.FromArgb(1, 255, 196, 138));
                var hoverBrush = new SolidColorBrush(Color.FromArgb(110, 255, 196, 138));
                var pressedBrush = new SolidColorBrush(Color.FromArgb(150, 0, 211, 255));
                var isPressed = false;
                var grip = new Border
                {
                    Background = normalBrush,
                    HorizontalAlignment = ha,
                    VerticalAlignment = va,
                    Width = w > 0 ? w : double.NaN,
                    Height = h > 0 ? h : double.NaN,
                    Cursor = new Cursor(cursor),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };
                grip.PointerEntered += (_, __) =>
                {
                    TryBringToFront(chrome);
                    grip.Background = hoverBrush;
                    grip.BorderBrush = hoverBrush;
                };
                grip.PointerExited += (_, __) =>
                {
                    if (!isPressed)
                    {
                        grip.Background = normalBrush;
                        grip.BorderBrush = Brushes.Transparent;
                    }
                };
                grip.PointerPressed += (s, e) =>
                {
                    TryBringToFront(chrome);
                    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                    isPressed = true;
                    grip.Background = pressedBrush;
                    grip.BorderBrush = pressedBrush;
                    try { e.Pointer.Capture(grip); } catch { }
                    var start = e.GetPosition(_canvas);
                    var container = _canvas.Bounds;

                    double ix = Canvas.GetLeft(chrome);
                    double iy = Canvas.GetTop(chrome);
                    double iw = chrome.Bounds.Width;
                    double ih = chrome.Bounds.Height;
                    const double minW = 80.0;
                    const double minH = 80.0;

                    void OnMove(object? _, PointerEventArgs args)
                    {
                        if (!ReferenceEquals(args.Pointer.Captured, grip)) return;
                        var p = args.GetPosition(_canvas);
                        var dx = p.X - start.X;
                        var dy = p.Y - start.Y;

                        double nx = ix, ny = iy, nw = iw, nh = ih;
                        if (left)
                        {
                            nx = Math.Clamp(ix + dx, 0, ix + iw - minW);
                            nw = Math.Max(minW, (ix + iw) - nx);
                        }
                        if (top)
                        {
                            ny = Math.Clamp(iy + dy, 0, iy + ih - minH);
                            nh = Math.Max(minH, (iy + ih) - ny);
                        }
                        if (right)
                        {
                            nw = Math.Clamp(iw + dx, minW, Math.Max(minW, container.Width - ix));
                        }
                        if (bottom)
                        {
                            nh = Math.Clamp(ih + dy, minH, Math.Max(minH, container.Height - iy));
                        }

                        nx = Math.Clamp(nx, 0, Math.Max(0, container.Width - nw));
                        ny = Math.Clamp(ny, 0, Math.Max(0, container.Height - nh));

                        Canvas.SetLeft(chrome, nx);
                        Canvas.SetTop(chrome, ny);
                        chrome.Width = nw;
                        chrome.Height = nh;

                        if (isParent && parent is not null)
                        {
                            parent.FloatingX = nx;
                            parent.FloatingY = ny;
                            parent.FloatingWidth = nw;
                            parent.FloatingHeight = nh;
                        }
                        else if (!isParent && child is not null)
                        {
                            if (this.VisualRoot is MainWindow mw && mw.DataContext is MainWindowViewModel vm)
                            {
                                vm.SetFloatingChildGeometry(child.Id, nx, ny, nw, nh);
                            }
                        }
                    }

                    void OnRelease(object? _, PointerReleasedEventArgs args2)
                    {
                        try { args2.Pointer.Capture(null); } catch { }
                        isPressed = false;
                        grip.Background = hoverBrush;
                        grip.BorderBrush = hoverBrush;
                        grip.PointerMoved -= OnMove;
                        grip.PointerReleased -= OnRelease;
                    }

                    grip.PointerMoved += OnMove;
                    grip.PointerReleased += OnRelease;
                    e.Handled = true;
                };

                panel.Children.Add(grip);
            }

            AddGrip(HorizontalAlignment.Stretch, VerticalAlignment.Top, 0, 8, StandardCursorType.SizeNorthSouth, false, true, false, false);
            AddGrip(HorizontalAlignment.Stretch, VerticalAlignment.Bottom, 0, 8, StandardCursorType.SizeNorthSouth, false, false, false, true);
            AddGrip(HorizontalAlignment.Left, VerticalAlignment.Stretch, 8, 0, StandardCursorType.SizeWestEast, true, false, false, false);
            AddGrip(HorizontalAlignment.Right, VerticalAlignment.Stretch, 8, 0, StandardCursorType.SizeWestEast, false, false, true, false);
            AddGrip(HorizontalAlignment.Left, VerticalAlignment.Top, 14, 14, StandardCursorType.TopLeftCorner, true, true, false, false);
            AddGrip(HorizontalAlignment.Right, VerticalAlignment.Top, 14, 14, StandardCursorType.TopRightCorner, false, true, true, false);
            AddGrip(HorizontalAlignment.Left, VerticalAlignment.Bottom, 14, 14, StandardCursorType.BottomLeftCorner, true, false, false, true);
            AddGrip(HorizontalAlignment.Right, VerticalAlignment.Bottom, 14, 14, StandardCursorType.BottomRightCorner, false, false, true, true);
        }

        private void TryBringToFront(Control chrome)
        {
            var nextZIndex = FloatingPaneSurfaceBuilder.GetNextFloatingZIndex(ParentPanes, ChildPanes, ref _zCounter);

            switch (chrome.DataContext)
            {
                case ParentPaneModel parent:
                    parent.FloatingZIndex = nextZIndex;
                    break;
                case ChildPaneDescriptor child:
                    if (this.VisualRoot is MainWindow mw && mw.DataContext is MainWindowViewModel vm)
                    {
                        vm.SetFloatingChildZIndex(child.Id, nextZIndex);
                    }
                    break;
            }

            try { chrome.ZIndex = nextZIndex; } catch { }
        }
    }
}
