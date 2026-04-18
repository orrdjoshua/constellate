using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Constellate.App.Controls
{
    internal sealed class FloatingPaneSubscriptionController
    {
        private readonly Action _onChanged;
        private INotifyCollectionChanged? _parentCollection;
        private INotifyCollectionChanged? _childCollection;
        private NotifyCollectionChangedEventHandler? _parentCollectionChangedHandler;
        private NotifyCollectionChangedEventHandler? _childCollectionChangedHandler;
        private readonly Dictionary<ParentPaneModel, PropertyChangedEventHandler> _parentHandlers = new();
        private IEnumerable<ParentPaneModel>? _currentParents;

        public FloatingPaneSubscriptionController(Action onChanged)
        {
            _onChanged = onChanged;
        }

        public void UpdateParents(IEnumerable<ParentPaneModel>? parents)
        {
            if (_parentCollection is not null && _parentCollectionChangedHandler is not null)
            {
                _parentCollection.CollectionChanged -= _parentCollectionChangedHandler;
            }

            DetachParentItemHandlers();

            _currentParents = parents;
            _parentCollection = parents as INotifyCollectionChanged;

            if (_parentCollection is not null)
            {
                _parentCollectionChangedHandler ??= (_, __) =>
                {
                    AttachParentItemHandlers(_currentParents);
                    _onChanged();
                };

                _parentCollection.CollectionChanged += _parentCollectionChangedHandler;
            }

            AttachParentItemHandlers(parents);
        }

        public void UpdateChildren(IEnumerable<ChildPaneDescriptor>? children)
        {
            if (_childCollection is not null && _childCollectionChangedHandler is not null)
            {
                _childCollection.CollectionChanged -= _childCollectionChangedHandler;
            }

            _childCollection = children as INotifyCollectionChanged;

            if (_childCollection is not null)
            {
                _childCollectionChangedHandler ??= (_, __) => _onChanged();
                _childCollection.CollectionChanged += _childCollectionChangedHandler;
            }
        }

        public void Clear()
        {
            if (_parentCollection is not null && _parentCollectionChangedHandler is not null)
            {
                _parentCollection.CollectionChanged -= _parentCollectionChangedHandler;
            }

            if (_childCollection is not null && _childCollectionChangedHandler is not null)
            {
                _childCollection.CollectionChanged -= _childCollectionChangedHandler;
            }

            DetachParentItemHandlers();

            _currentParents = null;
            _parentCollection = null;
            _childCollection = null;
        }

        private void AttachParentItemHandlers(IEnumerable<ParentPaneModel>? parents)
        {
            DetachParentItemHandlers();

            if (parents is null)
            {
                return;
            }

            foreach (var parent in parents)
            {
                PropertyChangedEventHandler handler = (_, __) => _onChanged();
                parent.PropertyChanged += handler;
                _parentHandlers[parent] = handler;
            }
        }

        private void DetachParentItemHandlers()
        {
            foreach (var pair in _parentHandlers.ToArray())
            {
                pair.Key.PropertyChanged -= pair.Value;
            }

            _parentHandlers.Clear();
        }
    }
}
