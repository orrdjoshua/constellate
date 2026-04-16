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
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = new ChildPaneView
                    {
                        DataContext = child
                    }
                };

                Canvas.SetLeft(chrome, Math.Max(0, child.FloatingX));
                Canvas.SetTop(chrome, Math.Max(0, child.FloatingY));
                chrome.ZIndex = _zCounter++;
                chrome.PointerPressed += (_, __) =>
                {
                    chrome.ZIndex = _zCounter++;
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
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = new ParentPaneView
                    {
                        DataContext = parent
                    }
                };

                Canvas.SetLeft(chrome, Math.Max(0, parent.FloatingX));
                Canvas.SetTop(chrome, Math.Max(0, parent.FloatingY));
                chrome.ZIndex = _zCounter++;
                chrome.PointerPressed += (_, __) =>
                {
                    chrome.ZIndex = _zCounter++;
                };
                _canvas.Children.Add(chrome);
            }
        }
    }
}
