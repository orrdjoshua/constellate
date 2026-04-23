using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Unified overlay layer that renders:
    /// - Docked parent panes at absolute overlay rects (from VM.CurrentShellLayout.*Dock.Bounds)
    /// - Floating parents/children using the existing floating-stack controller
    ///
    /// A single Canvas is used for all surfaces to keep one overlay coordinate system and z-policy.
    /// </summary>
    public partial class OverlayPaneLayer : UserControl
    {
        // Reuse the stable floating presenter/stack (controller) by exposing the same styled props here.
        public static readonly StyledProperty<IEnumerable<ParentPaneModel>?> ParentPanesProperty =
            AvaloniaProperty.Register<OverlayPaneLayer, IEnumerable<ParentPaneModel>?>(nameof(ParentPanes));

        public static readonly StyledProperty<IEnumerable<ChildPaneDescriptor>?> ChildPanesProperty =
            AvaloniaProperty.Register<OverlayPaneLayer, IEnumerable<ChildPaneDescriptor>?>(nameof(ChildPanes));

        private Canvas? _canvas;
        private MainWindowViewModel? _vm;

        // One realized ParentPaneView per dock host id: "left","top","right","bottom"
        private readonly Dictionary<string, ParentPaneView> _dockedHostViews = new(StringComparer.Ordinal);

        // Compose the existing floating presenter/controller stack on the same canvas.
        private FloatingPaneLayerController? _floatingController;

        public OverlayPaneLayer()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
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

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            HookVm();
            EnsureFloatingController();
            SyncAll();
            _floatingController?.Sync();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            UnhookVm();
            _dockedHostViews.Clear();
            _canvas?.Children.Clear();
            _floatingController = null;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            HookVm();
            EnsureFloatingController();
            SyncAll();
        }

        private void HookVm()
        {
            UnhookVm();
            _vm = DataContext as MainWindowViewModel;
            if (_vm is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnVmPropertyChanged;
            }

            // Surface the VM floating collections via the styled properties so the floating controller can observe them.
            if (_vm is not null)
            {
                ParentPanes = _vm.ParentPaneModelsFloating;
                ChildPanes = _vm.FloatingChildPanes;
            }
            else
            {
                ParentPanes = null;
                ChildPanes = null;
            }
        }

        private void UnhookVm()
        {
            if (_vm is INotifyPropertyChanged oldPc)
            {
                oldPc.PropertyChanged -= OnVmPropertyChanged;
            }
            _vm = null;
        }

        private void EnsureFloatingController()
        {
            if (_canvas is null)
            {
                return;
            }

            if (_floatingController is not null)
            {
                return;
            }

            // Reuse the stable floating controller, but bind it against our canvas and styled properties.
            _floatingController = new FloatingPaneLayerController(
                owner: this,
                canvas: _canvas,
                parentPanesSelector: () => ParentPanes,
                childPanesSelector: () => ChildPanes);
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Docked rects/active parent changes → resync docked host views.
            if (e.PropertyName is nameof(MainWindowViewModel.CurrentShellLayout) or
                                    nameof(MainWindowViewModel.ActiveParentPaneLeft) or
                                    nameof(MainWindowViewModel.ActiveParentPaneTop) or
                                    nameof(MainWindowViewModel.ActiveParentPaneRight) or
                                    nameof(MainWindowViewModel.ActiveParentPaneBottom) or
                                    nameof(MainWindowViewModel.ParentPaneModels))
            {
                SyncDocked();
            }

            // Floating collections changed → keep our styled props in sync so the floating controller sees it.
            if (e.PropertyName is nameof(MainWindowViewModel.ParentPaneModelsFloating) or
                                    nameof(MainWindowViewModel.FloatingChildPanes))
            {
                if (_vm is not null)
                {
                    ParentPanes = _vm.ParentPaneModelsFloating;
                    ChildPanes = _vm.FloatingChildPanes;
                }
                _floatingController?.Sync();
            }
        }

        private void SyncAll()
        {
            SyncDocked();

            // Ensure floating visuals are in sync as well.
            _floatingController?.Sync();
        }

        private void SyncDocked()
        {
            if (_vm is null || _canvas is null)
            {
                return;
            }

            // Left
            SyncDockedHost("left", _vm.CurrentShellLayout.LeftDock, _vm.ActiveParentPaneLeft);

            // Top
            SyncDockedHost("top", _vm.CurrentShellLayout.TopDock, _vm.ActiveParentPaneTop);

            // Right
            SyncDockedHost("right", _vm.CurrentShellLayout.RightDock, _vm.ActiveParentPaneRight);

            // Bottom
            SyncDockedHost("bottom", _vm.CurrentShellLayout.BottomDock, _vm.ActiveParentPaneBottom);
        }

        private void SyncDockedHost(string hostId, DockHostLayout? layout, ParentPaneModel? parent)
        {
            if (_vm is null || _canvas is null)
            {
                return;
            }

            var hasVisible = layout is { IsVisible: true } && parent is not null && !parent.IsMinimized;

            if (!hasVisible)
            {
                RemoveDockedHostView(hostId);
                return;
            }

            EnsureDockedHostView(hostId, parent!);
            ApplyBounds(hostId, layout!.Bounds);
        }

        private void EnsureDockedHostView(string hostId, ParentPaneModel parent)
        {
            if (_canvas is null)
            {
                return;
            }

            if (_dockedHostViews.TryGetValue(hostId, out var existing))
            {
                if (!ReferenceEquals(existing.DataContext, parent))
                {
                    existing.DataContext = parent;
                }
                return;
            }

            var view = new ParentPaneView
            {
                DataContext = parent
            };

            _dockedHostViews[hostId] = view;
            _canvas.Children.Add(view);
        }

        private void RemoveDockedHostView(string hostId)
        {
            if (!_dockedHostViews.TryGetValue(hostId, out var view) || _canvas is null)
            {
                return;
            }

            _canvas.Children.Remove(view);
            _dockedHostViews.Remove(hostId);
        }

        private static void ApplyBounds(Control control, Rect rect)
        {
            Canvas.SetLeft(control, rect.X);
            Canvas.SetTop(control, rect.Y);
            control.Width = rect.Width;
            control.Height = rect.Height;
        }

        private void ApplyBounds(string hostId, Rect rect)
        {
            if (_dockedHostViews.TryGetValue(hostId, out var view))
            {
                ApplyBounds(view, rect);
            }
        }
    }
}
