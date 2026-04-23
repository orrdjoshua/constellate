using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    ///
    /// This presenter also measures the actual visible body viewport and writes that
    /// geometry back to the ParentPaneModel. Child creation/sizing semantics must be
    /// based on this visible body viewport only, never on header size or content extent.
    /// </summary>
    public sealed class ParentBodyPresenter : UserControl
    {
        private ParentPaneModel? _parent;

        public ParentBodyPresenter()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            DataContextChanged += (_, __) => HookParent();
            AttachedToVisualTree += (_, __) =>
            {
                UpdateMeasuredViewport();
                Rebuild();
            };
            SizeChanged += (_, __) =>
            {
                UpdateMeasuredViewport();
                Rebuild();
            };
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

            UpdateMeasuredViewport();
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

        private void UpdateMeasuredViewport()
        {
            if (_parent is null)
            {
                return;
            }

            var width = Math.Max(0.0, Bounds.Width);
            var height = Math.Max(0.0, Bounds.Height);

            var changed = false;

            if (TopLevel.GetTopLevel(this) is Window window &&
                window.DataContext is MainWindowViewModel vm)
            {
                changed = vm.UpdateParentBodyViewport(_parent.Id, width, height);
            }
            else
            {
                changed =
                    Math.Abs(_parent.BodyViewportWidth - width) > double.Epsilon ||
                    Math.Abs(_parent.BodyViewportHeight - height) > double.Epsilon;

                if (changed)
                {
                    _parent.BodyViewportWidth = width;
                    _parent.BodyViewportHeight = height;
                }
            }

            if (!changed)
            {
                return;
            }

            Debug.WriteLine(
                $"[ParentBodyViewport] parent={_parent.Id} orientation={(_parent.IsVerticalBodyOrientation ? "vertical" : "horizontal")} " +
                $"bodyW={_parent.BodyViewportWidth:0.##} bodyH={_parent.BodyViewportHeight:0.##} " +
                $"fixed={_parent.BodyViewportFixedSize:0.##} adjustable={_parent.BodyViewportAdjustableSize:0.##} " +
                $"lanes={_parent.LanesVisible.Count} slide={_parent.SlideIndex} splits={_parent.SplitCount}");
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
