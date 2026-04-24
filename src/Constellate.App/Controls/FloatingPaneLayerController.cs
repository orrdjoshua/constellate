using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Constellate.App.Controls.Panes;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Functional controller used by OverlayPaneLayer to render floating parents/children
    /// directly on the provided Canvas using current pane state.
    ///
    /// Responsibilities (v1):
    /// - realize ParentPaneView/ChildPaneView for panes on the floating host
    /// - position/size them from pane model geometry
    /// - keep visuals in sync with model property changes (including z-order)
    ///
    /// Notes:
    /// - 8-way floating resize grips are not attached in this minimal pass; a follow-up
    ///   can add them via our existing FloatingPane* helpers after parity is verified.
    /// </summary>
    internal sealed class FloatingPaneLayerController
    {
        private readonly Control _owner;
        private readonly Canvas _canvas;
        private readonly Func<IEnumerable<ParentPaneModel>?> _parentPanesSelector;
        private readonly Func<IEnumerable<ChildPaneDescriptor>?> _childPanesSelector;

        private MainWindowViewModel? _vm;
        private FloatingPaneSurfaceController? _surfaceController;

        private readonly Dictionary<string, ParentPaneView> _parentViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ChildPaneView> _childViews = new(StringComparer.Ordinal);

        private readonly Dictionary<string, INotifyPropertyChanged> _parentSubs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, INotifyPropertyChanged> _childSubs = new(StringComparer.Ordinal);

        public FloatingPaneLayerController(
            Control owner,
            Canvas canvas,
            Func<IEnumerable<ParentPaneModel>?> parentPanesSelector,
            Func<IEnumerable<ChildPaneDescriptor>?> childPanesSelector)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _parentPanesSelector = parentPanesSelector ?? throw new ArgumentNullException(nameof(parentPanesSelector));
            _childPanesSelector = childPanesSelector ?? throw new ArgumentNullException(nameof(childPanesSelector));

            _owner.AttachedToVisualTree += OnAttachedToVisualTree;
            _owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _vm = (TopLevel.GetTopLevel(_owner) as MainWindow)?.DataContext as MainWindowViewModel;
            if (_vm is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnVmPropertyChanged;
            }
            // Shared controller used by resize grips (commit geometry + bring-to-front).
            // Use positional args to match the ctor signature and avoid CS1739.
            _surfaceController = new FloatingPaneSurfaceController(
                _owner,
                _parentPanesSelector,
                _childPanesSelector);
            Sync();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_vm is INotifyPropertyChanged oldPc)
            {
                oldPc.PropertyChanged -= OnVmPropertyChanged;
            }
            _vm = null;
            _surfaceController = null;

            foreach (var kv in _parentSubs)
            {
                kv.Value.PropertyChanged -= OnParentPropertyChanged;
            }
            foreach (var kv in _childSubs)
            {
                kv.Value.PropertyChanged -= OnChildPropertyChanged;
            }
            _parentSubs.Clear();
            _childSubs.Clear();

            _canvas.Children.Clear();
            _parentViews.Clear();
            _childViews.Clear();
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When floating collections change or pane z/coords change (propagated through Sync),
            // we simply rebuild/update the realized set.
            if (e.PropertyName is nameof(MainWindowViewModel.ParentPaneModelsFloating) or
                                     nameof(MainWindowViewModel.FloatingChildPanes))
            {
                Sync();
            }
        }

        public void Sync()
        {
            var parents = _parentPanesSelector()?.ToArray() ?? Array.Empty<ParentPaneModel>();
            var children = _childPanesSelector()?.ToArray() ?? Array.Empty<ChildPaneDescriptor>();

            // Remove stale parents
            var parentIds = new HashSet<string>(parents.Select(p => p.Id), StringComparer.Ordinal);
            foreach (var stale in _parentViews.Keys.Where(id => !parentIds.Contains(id)).ToArray())
            {
                if (_parentViews.Remove(stale, out var view))
                {
                    _canvas.Children.Remove(view);
                }
                if (_parentSubs.Remove(stale, out var sub))
                {
                    sub.PropertyChanged -= OnParentPropertyChanged;
                }
            }

            // Remove stale children
            var childIds = new HashSet<string>(children.Select(c => c.Id), StringComparer.Ordinal);
            foreach (var stale in _childViews.Keys.Where(id => !childIds.Contains(id)).ToArray())
            {
                if (_childViews.Remove(stale, out var view))
                {
                    _canvas.Children.Remove(view);
                }
                if (_childSubs.Remove(stale, out var sub))
                {
                    sub.PropertyChanged -= OnChildPropertyChanged;
                }
            }

            // Ensure/Update parents
            foreach (var p in parents)
            {
                if (!_parentViews.TryGetValue(p.Id, out var view))
                {
                    view = new ParentPaneView { DataContext = p };
                    _parentViews[p.Id] = view;
                    _canvas.Children.Add(view);

                    // subscribe for geometry/z updates
                    if (p is INotifyPropertyChanged sub && !_parentSubs.ContainsKey(p.Id))
                    {
                        sub.PropertyChanged += OnParentPropertyChanged;
                        _parentSubs[p.Id] = sub;
                    }
                }
                else
                {
                    if (!ReferenceEquals(view.DataContext, p))
                    {
                        view.DataContext = p;
                    }
                }

                ApplyParentGeometry(view, p);
                AttachFloatingResizeGripsIfNeeded(view);
            }

            // Ensure/Update children
            foreach (var c in children)
            {
                if (!_childViews.TryGetValue(c.Id, out var view))
                {
                    view = new ChildPaneView { DataContext = c };
                    _childViews[c.Id] = view;
                    _canvas.Children.Add(view);

                    if (view.DataContext is INotifyPropertyChanged sub && !_childSubs.ContainsKey(c.Id))
                    {
                        sub.PropertyChanged += OnChildPropertyChanged;
                        _childSubs[c.Id] = sub;
                    }
                }
                else
                {
                    if (!ReferenceEquals(view.DataContext, c))
                    {
                        view.DataContext = c;
                    }
                }

                ApplyChildGeometry(view, c);
                AttachFloatingResizeGripsIfNeeded(view);
            }
        }

        private static void ApplyParentGeometry(Control view, ParentPaneModel p)
        {
            Canvas.SetLeft(view, Math.Max(0.0, p.FloatingX));
            Canvas.SetTop(view, Math.Max(0.0, p.FloatingY));
            view.Width = Math.Max(1.0, p.FloatingWidth);
            view.Height = Math.Max(1.0, p.FloatingHeight);
            view.ZIndex = p.FloatingZIndex;
        }

        private static void ApplyChildGeometry(Control view, ChildPaneDescriptor c)
        {
            Canvas.SetLeft(view, Math.Max(0.0, c.FloatingX));
            Canvas.SetTop(view, Math.Max(0.0, c.FloatingY));
            view.Width = Math.Max(1.0, c.FloatingWidth);
            view.Height = Math.Max(1.0, c.FloatingHeight);
            view.ZIndex = c.FloatingZIndex;
        }

        private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ParentPaneModel p) return;
            if (!_parentViews.TryGetValue(p.Id, out var view)) return;

            switch (e.PropertyName)
            {
                case nameof(ParentPaneModel.FloatingX):
                case nameof(ParentPaneModel.FloatingY):
                case nameof(ParentPaneModel.FloatingWidth):
                case nameof(ParentPaneModel.FloatingHeight):
                case nameof(ParentPaneModel.FloatingZIndex):
                case nameof(ParentPaneModel.IsMinimized):
                    ApplyParentGeometry(view, p);
                    break;
            }
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ChildPaneDescriptor c) return;
            if (!_childViews.TryGetValue(c.Id, out var view)) return;

            switch (e.PropertyName)
            {
                case nameof(ChildPaneDescriptor.FloatingX):
                case nameof(ChildPaneDescriptor.FloatingY):
                case nameof(ChildPaneDescriptor.FloatingWidth):
                case nameof(ChildPaneDescriptor.FloatingHeight):
                case nameof(ChildPaneDescriptor.FloatingZIndex):
                case nameof(ChildPaneDescriptor.IsMinimized):
                    ApplyChildGeometry(view, c);
                    break;
            }
        }

        private void AttachFloatingResizeGripsIfNeeded(Control view)
        {
            if (_surfaceController is null) return;

            try
            {
                // System.Diagnostics.Debug.WriteLine($"[FloatingGrip][Probe] view={view.GetType().Name} dc={view.DataContext?.GetType().Name ?? "null"}");
            }
            catch
            {
            }

            // Resolve the pane's PaneChrome (parent vs child).
            PaneChrome? chrome = null;
            if (view is ParentPaneView pv)
            {
                chrome = pv.FindControl<PaneChrome>("ParentChrome");
            }
            else if (view is ChildPaneView cv)
            {
                chrome = cv.FindControl<PaneChrome>("ChildChrome");
            }

            if (chrome is null)
            {
                try
                {
                    // System.Diagnostics.Debug.WriteLine("[FloatingGrip][Probe][MISS] chrome=null");
                }
                catch
                {
                }
                return;
            }

            if (chrome.RootBorder is not Border rootBorder)
            {
                try
                {
                    // System.Diagnostics.Debug.WriteLine("[FloatingGrip][Probe][MISS] rootBorder=null");
                }
                catch
                {
                }
                return;
            }

            // The grips need a Panel host to add children into; PaneChrome's root Border's parent is the top-level Grid.
            if (rootBorder.Parent is not Panel panelHost)
            {
                try
                {
                    // System.Diagnostics.Debug.WriteLine($"[FloatingGrip][Probe][MISS] panelHostType={(rootBorder.Parent?.GetType().Name ?? "null")}");
                }
                catch
                {
                }
                return;
            }

            try
            {
                // System.Diagnostics.Debug.WriteLine($"[FloatingGrip][Probe][OK] panelHost={panelHost.GetType().Name} width={view.Bounds.Width:0.##} height={view.Bounds.Height:0.##}");
            }
            catch
            {
            }

            // Idempotent: the controller guards by class "floatingPaneResizeHost".
            // Important: grips are injected into the pane-local panel host, but geometry mutation
            // must target the OUTER floating pane surface control (the Canvas child), not the
            // inner PaneChrome RootBorder.
            FloatingPaneResizeController.AttachResizeGrips(
                view,
                _canvas,
                panelHost,
                _surfaceController);
        }
    }
}
