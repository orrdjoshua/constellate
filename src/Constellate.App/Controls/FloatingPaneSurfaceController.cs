using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal sealed class FloatingPaneSurfaceController
    {
        private readonly Control _owner;
        private readonly Func<IEnumerable<ParentPaneModel>?> _getParents;
        private readonly Func<IEnumerable<ChildPaneDescriptor>?> _getChildren;
        private int _zCounter = 1;

        public FloatingPaneSurfaceController(
            Control owner,
            Func<IEnumerable<ParentPaneModel>?> getParents,
            Func<IEnumerable<ChildPaneDescriptor>?> getChildren)
        {
            _owner = owner;
            _getParents = getParents;
            _getChildren = getChildren;
        }

        public void Reset()
        {
            _zCounter = 1;
        }

        public FloatingPaneSurfaceModel BuildSurfaceModel()
        {
            var nextZCounter = _zCounter;

            var surfaceModel = FloatingPaneSurfaceBuilder.BuildSurfaceModel(
                _getParents(),
                _getChildren(),
                ref nextZCounter);

            if (nextZCounter > _zCounter)
            {
                _zCounter = nextZCounter;
            }

            return surfaceModel;
        }

        public void BringToFront(Control chrome)
        {
            FloatingPaneStateController.BringToFront(
                _owner,
                chrome,
                _getParents(),
                _getChildren(),
                ref _zCounter);
        }

        public void CommitFloatingGeometry(
            Control chrome,
            double x,
            double y,
            double width,
            double height)
        {
            FloatingPaneStateController.SetFloatingGeometry(
                _owner,
                chrome.DataContext,
                x,
                y,
                width,
                height);
        }
    }
}
