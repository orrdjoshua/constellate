using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Realizes a parent pane body as a set of equally-sized split lanes that fill
    /// the parent's full AdjustableDimension.
    ///
    /// Vertical parent orientation:
    /// - child flow inside a lane is vertical,
    /// - lanes are arranged as equal-width columns across the body's horizontal adjustable axis.
    ///
    /// Horizontal parent orientation:
    /// - child flow inside a lane is horizontal,
    /// - lanes are arranged as equal-height rows across the body's vertical adjustable axis.
    /// </summary>
    public sealed class ParentBodyPresenter : UserControl
    {
        private ParentPaneModel? _parent;

        public ParentBodyPresenter()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            DataContextChanged += (_, __) => HookParent();
            AttachedToVisualTree += (_, __) => Rebuild();
        }

        private void HookParent()
        {
            if (_parent is INotifyPropertyChanged oldNotify)
            {
                oldNotify.PropertyChanged -= OnParentPropertyChanged;
            }

            _parent = DataContext as ParentPaneModel;

            if (_parent is INotifyPropertyChanged newNotify)
            {
                newNotify.PropertyChanged += OnParentPropertyChanged;
            }

            Rebuild();
        }

        private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ParentPaneModel.LanesVisible) or
                nameof(ParentPaneModel.IsVerticalBodyOrientation) or
                nameof(ParentPaneModel.IsHorizontalBodyOrientation))
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            if (DataContext is not ParentPaneModel parent ||
                parent.LanesVisible is null ||
                parent.LanesVisible.Count == 0)
            {
                Content = null;
                return;
            }

            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true
            };

            if (parent.IsVerticalBodyOrientation)
            {
                grid.ColumnSpacing = 6;

                for (var i = 0; i < parent.LanesVisible.Count; i++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                }
            }
            else
            {
                grid.RowSpacing = 6;

                for (var i = 0; i < parent.LanesVisible.Count; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
                }
            }

            for (var laneIndex = 0; laneIndex < parent.LanesVisible.Count; laneIndex++)
            {
                var lane = parent.LanesVisible[laneIndex];

                var laneHost = new Border
                {
                    Background = Avalonia.Media.Brush.Parse("#151E28"),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ClipToBounds = true,
                    Child = new LanePresenter
                    {
                        DataContext = lane,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    }
                };

                if (parent.IsVerticalBodyOrientation)
                {
                    Grid.SetColumn(laneHost, laneIndex);
                }
                else
                {
                    Grid.SetRow(laneHost, laneIndex);
                }

                grid.Children.Add(laneHost);
            }

            Content = grid;
        }
    }
}
