using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;

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
        private int _zCounter = 1;

        public FloatingPaneLayer()
        {
            InitializeComponent();
            AttachedToVisualTree += (_, __) => RebuildCanvas();
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
                RebuildCanvas();
                return;
            }

            if (change.Property == ChildPanesProperty)
            {
                RewireChildSubscriptions(change.GetNewValue<IEnumerable<ChildPaneDescriptor>?>());
                RebuildCanvas();
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
                    RebuildCanvas();
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
                PropertyChangedEventHandler handler = (_, __) => RebuildCanvas();
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
                _childCollectionChangedHandler ??= (_, __) => RebuildCanvas();
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
        }

        private void RebuildCanvas()
        {
            if (_canvas is null)
            {
                return;
            }

            _canvas.Children.Clear();

            foreach (var child in ChildPanes ?? Enumerable.Empty<ChildPaneDescriptor>())
            {
                // Only render parentless (floating) children; include minimized so header chrome remains visible
                if (child.ParentId is not null)
                {
                    continue;
                }

                var chrome = new Border
                {
                    Background = Brushes.Transparent,
                    Width = Math.Max(80, child.FloatingWidth),
                    Height = Math.Max(80, child.FloatingHeight),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // Content host + grips overlay
                var grid = new Grid();
                grid.Children.Add(new ChildPaneView { DataContext = child });
                chrome.Child = grid;
                AttachResizeGrips(chrome, isParent: false, parent: null, child: child);

                Canvas.SetLeft(chrome, Math.Max(0, child.FloatingX));
                Canvas.SetTop(chrome, Math.Max(0, child.FloatingY));
                try { chrome.ZIndex = _zCounter++; } catch { }
                chrome.PointerPressed += (_, __) =>
                {
                    try { chrome.ZIndex = _zCounter++; } catch { }
                };
                _canvas.Children.Add(chrome);
            }

            foreach (var parent in ParentPanes ?? Enumerable.Empty<ParentPaneModel>())
            {
                if (!string.Equals(parent.HostId, "floating", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var chrome = new Border
                {
                    Background = Brushes.Transparent,
                    Width = Math.Max(80, parent.FloatingWidth),
                    Height = Math.Max(80, parent.FloatingHeight),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // Content host + grips overlay
                var grid = new Grid();
                grid.Children.Add(new ParentPaneView { DataContext = parent });
                chrome.Child = grid;
                AttachResizeGrips(chrome, isParent: true, parent: parent, child: null);

                Canvas.SetLeft(chrome, Math.Max(0, parent.FloatingX));
                Canvas.SetTop(chrome, Math.Max(0, parent.FloatingY));
                try { chrome.ZIndex = _zCounter++; } catch { }
                chrome.PointerPressed += (_, __) =>
                {
                    try { chrome.ZIndex = _zCounter++; } catch { }
                };
                _canvas.Children.Add(chrome);
            }
        }

        // Adds 8 resize grips (N,S,E,W + NW,NE,SW,SE) to the floating chrome.
        private void AttachResizeGrips(Border chrome, bool isParent, ParentPaneModel? parent, ChildPaneDescriptor? child)
        {
            if (_canvas is null) return;
            if (chrome.Child is not Panel panel) return;

            void AddGrip(HorizontalAlignment ha, VerticalAlignment va, double w, double h, StandardCursorType cursor,
                         bool left, bool top, bool right, bool bottom)
            {
                var grip = new Border
                {
                    Background = Brushes.Transparent,
                    HorizontalAlignment = ha,
                    VerticalAlignment = va,
                    Width = w > 0 ? w : double.NaN,
                    Height = h > 0 ? h : double.NaN,
                    Cursor = new Cursor(cursor)
                };
                grip.PointerPressed += (s, e) =>
                {
                    TryBringToFront(chrome);
                    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
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

                        // Clamp position so rect remains inside container
                        nx = Math.Clamp(nx, 0, Math.Max(0, container.Width - nw));
                        ny = Math.Clamp(ny, 0, Math.max(0, container.Height - nh));

                        Canvas.SetLeft(chrome, nx);
                        Canvas.SetTop(chrome, ny);
                        chrome.Width = nw;
                        chrome.Height = nh;

                        // Commit back to models
                        if (isParent && parent is not null)
                        {
                            parent.FloatingX = nx;
                            parent.FloatingY = ny;
                            parent.FloatingWidth = nw;
                            parent.FloatingHeight = nh;
                        }
                        else if (!isParent && child is not null)
                        {
                            // Route via VM to update immutable record in collection
                            if (this.VisualRoot is MainWindow mw && mw.DataContext is MainWindowViewModel vm)
                            {
                                vm.SetFloatingChildGeometry(child.Id, nx, ny, nw, nh);
                            }
                        }
                    }

                    void OnRelease(object? _, PointerReleasedEventArgs args2)
                    {
                        try { args2.Pointer.Capture(null); } catch { }
                        grip.PointerMoved -= OnMove;
                        grip.PointerReleased -= OnRelease;
                    }

                    grip.PointerMoved += OnMove;
                    grip.PointerReleased += OnRelease;
                    e.Handled = true;
                };

                panel.Children.Add(grip);
            }

            // Edges
            AddGrip(HorizontalAlignment.Stretch, VerticalAlignment.Top,    0, 6,  StandardCursorType.SizeNorthSouth, false, true,  false, false);
            AddGrip(HorizontalAlignment.Stretch, VerticalAlignment.Bottom, 0, 6,  StandardCursorType.SizeNorthSouth, false, false, false, true);
            AddGrip(HorizontalAlignment.Left,    VerticalAlignment.Stretch,6, 0,  StandardCursorType.SizeWestEast,   true,  false, false, false);
            AddGrip(HorizontalAlignment.Right,   VerticalAlignment.Stretch,6, 0,  StandardCursorType.SizeWestEast,   false, false, true,  false);
            // Corners
            AddGrip(HorizontalAlignment.Left,    VerticalAlignment.Top,    10,10, StandardCursorType.SizeNorthWestSouthEast, true,  true,  false, false);
            AddGrip(HorizontalAlignment.Right,   VerticalAlignment.Top,    10,10, StandardCursorType.SizeNorthEastSouthWest, false, true,  true,  false);
            AddGrip(HorizontalAlignment.Left,    VerticalAlignment.Bottom, 10,10, StandardCursorType.SizeNorthEastSouthWest, true,  false, false, true );
            AddGrip(HorizontalAlignment.Right,   VerticalAlignment.Bottom, 10,10, StandardCursorType.SizeNorthWestSouthEast, false, false, true,  true );
        }

        private void TryBringToFront(Control chrome)
        {
            try { chrome.ZIndex = _zCounter++; } catch { }
        }
    }
}
