using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Constellate.App.Controls
{
    internal sealed class FloatingPaneLayerController
    {
        private readonly FloatingPaneLayer _owner;
        private readonly FloatingPanePresenter _presenter;
        private readonly FloatingPaneSubscriptionController _subscriptions;
        private readonly FloatingPaneSurfaceController _surfaceController;

        public FloatingPaneLayerController(
            FloatingPaneLayer owner,
            Canvas canvas,
            Func<IEnumerable<ParentPaneModel>?> getParents,
            Func<IEnumerable<ChildPaneDescriptor>?> getChildren)
        {
            _owner = owner;
            _surfaceController = new FloatingPaneSurfaceController(owner, getParents, getChildren);
            _presenter = new FloatingPanePresenter(canvas, _surfaceController);
            _subscriptions = new FloatingPaneSubscriptionController(Synchronize);

            _owner.AttachedToVisualTree += (_, __) => OnAttachedToVisualTree();
            _owner.DetachedFromVisualTree += (_, __) => OnDetachedFromVisualTree();
            _owner.PropertyChanged += (_, change) => OnOwnerPropertyChanged(change);
        }

        private void OnAttachedToVisualTree()
        {
            _subscriptions.UpdateParents(_owner.ParentPanes);
            _subscriptions.UpdateChildren(_owner.ChildPanes);
            Synchronize();
        }

        private void OnDetachedFromVisualTree()
        {
            Clear();
        }

        private void OnOwnerPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == FloatingPaneLayer.ParentPanesProperty)
            {
                _subscriptions.UpdateParents(change.GetNewValue<IEnumerable<ParentPaneModel>?>());
                Synchronize();
                return;
            }

            if (change.Property == FloatingPaneLayer.ChildPanesProperty)
            {
                _subscriptions.UpdateChildren(change.GetNewValue<IEnumerable<ChildPaneDescriptor>?>());
                Synchronize();
            }
        }

        private void Synchronize()
        {
            var surfaceModel = _surfaceController.BuildSurfaceModel();
            _presenter.Synchronize(surfaceModel);
        }

        private void Clear()
        {
            _subscriptions.Clear();
            _presenter.Clear();
            _surfaceController.Reset();
        }
    }
}
