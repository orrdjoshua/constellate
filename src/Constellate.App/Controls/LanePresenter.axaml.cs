using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Renders a single lane (either a column of vertically-flowing children for Left/Right parents,
    /// or a row of horizontally-flowing children for Top/Bottom parents).
    /// The presenter applies per-child proportional sizing along the lane’s fixed dimension using star Grid sizing
    /// based on each child's PreferredSizeRatio, and includes GridSplitters
    /// so users can drag-resize children. The lane viewport itself is explicitly constrained
    /// to the measured visible body viewport of the parent pane so header size and content extent
    /// do not affect initial child sizing semantics.
    /// </summary>
    public sealed class LanePresenter : UserControl
    {
        private Border? _root;

        public LanePresenter()
        {
            _root = new Border { Background = Brushes.Transparent };
            Content = _root;
            DataContextChanged += (_, __) => Rebuild();
            AttachedToVisualTree += (_, __) => Rebuild();
            SizeChanged += (_, __) => Rebuild();
        }

        private MainWindowViewModel? GetVm()
        {
            if (VisualRoot is Window w && w.DataContext is MainWindowViewModel vm)
                return vm;
            return null;
        }

        private void Rebuild()
        {
            if (_root == null)
            {
                _root = new Border { Background = Brushes.Transparent };
                Content = _root;
            }

            if (DataContext is not LaneView lane)
            {
                _root.Child = null;
                return;
            }

            var viewportWidth = lane.ViewportWidth > 0 ? lane.ViewportWidth : Math.Max(1.0, Bounds.Width);
            var viewportHeight = lane.ViewportHeight > 0 ? lane.ViewportHeight : Math.Max(1.0, Bounds.Height);

            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = lane.IsHorizontalScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = lane.IsVerticalScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = viewportWidth,
                Height = viewportHeight
            };

            var grid = new Grid
            {
                ColumnSpacing = 6,
                RowSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinWidth = viewportWidth,
                MinHeight = viewportHeight
            };

            var children = lane.Children ?? Array.Empty<ChildPaneDescriptor>();
            var ratios = lane.Ratios ?? Array.Empty<double>();
            var count = Math.Min(children.Count, ratios.Count);

            Debug.WriteLine(
                $"[LaneViewport] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                $"laneViewportW={viewportWidth:0.##} laneViewportH={viewportHeight:0.##} " +
                $"fixedViewport={lane.FixedViewportSize:0.##} adjustableViewport={lane.AdjustableViewportSize:0.##} " +
                $"children={count} ratios=[{string.Join(",", ratios.Take(count).Select(r => r.ToString("0.###")))}]");

            if (count == 0)
            {
                grid.Width = viewportWidth;
                grid.Height = viewportHeight;
                scroll.Content = grid;
                _root.Child = scroll;
                return;
            }

            var consumedRatio = ratios
                .Take(count)
                .Select(r => Math.Max(0.0, r))
                .Sum();

            var remainderRatio = Math.Max(0.0, 1.0 - consumedRatio);

            if (lane.IsVerticalFlow)
            {
                grid.Width = viewportWidth;
                grid.Height = viewportHeight;
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                for (int i = 0; i < count; i++)
                {
                    var star = Math.Max(0.0001, ratios[i]);
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(star, GridUnitType.Star)));

                    var chrome = new Border
                    {
                        Background = Brushes.Transparent,
                        Child = new ChildPaneView { DataContext = children[i] }
                    };

                    Grid.SetColumn(chrome, 0);
                    Grid.SetRow(chrome, i * 2);
                    grid.Children.Add(chrome);

                    if (i < count - 1)
                    {
                        grid.RowDefinitions.Add(new RowDefinition(new GridLength(6, GridUnitType.Pixel)));

                        var splitter = new GridSplitter
                        {
                            ResizeDirection = GridResizeDirection.Rows,
                            Background = Brushes.Transparent,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = 6,
                            Cursor = new Cursor(StandardCursorType.SizeNorthSouth)
                        };

                        splitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                        Grid.SetColumn(splitter, 0);
                        Grid.SetRow(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                if (remainderRatio > 1e-6)
                {
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(remainderRatio, GridUnitType.Star)));
                }
            }
            else
            {
                grid.Width = viewportWidth;
                grid.Height = viewportHeight;
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                for (int i = 0; i < count; i++)
                {
                    var star = Math.Max(0.0001, ratios[i]);
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(star, GridUnitType.Star)));

                    var chrome = new Border
                    {
                        Background = Brushes.Transparent,
                        Child = new ChildPaneView { DataContext = children[i] }
                    };

                    Grid.SetRow(chrome, 0);
                    Grid.SetColumn(chrome, i * 2);
                    grid.Children.Add(chrome);

                    if (i < count - 1)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(6, GridUnitType.Pixel)));

                        var splitter = new GridSplitter
                        {
                            ResizeDirection = GridResizeDirection.Columns,
                            Background = Brushes.Transparent,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Width = 6,
                            Cursor = new Cursor(StandardCursorType.SizeWestEast)
                        };

                        splitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                        Grid.SetRow(splitter, 0);
                        Grid.SetColumn(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                if (remainderRatio > 1e-6)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(remainderRatio, GridUnitType.Star)));
                }
            }

            scroll.Content = grid;
            _root.Child = scroll;
        }

        private void PersistRatios(LaneView lane, Grid grid)
        {
            var list = new List<(string childId, double size)>();

            foreach (var child in lane.Children ?? Array.Empty<ChildPaneDescriptor>())
            {
                var chrome = grid.Children
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Child is ChildPaneView cpv && ReferenceEquals(cpv.DataContext, child));

                if (chrome is null)
                {
                    list.Add((child.Id, 1.0));
                }
                else
                {
                    var sz = lane.IsVerticalFlow ? Math.Max(1.0, chrome.Bounds.Height) : Math.Max(1.0, chrome.Bounds.Width);
                    list.Add((child.Id, sz));
                }
            }

            var total = list.Sum(x => x.size);
            if (total <= 1e-6) return;

            var updates = list.Select(x => (x.childId, x.size / total)).ToArray();
            var vm = GetVm();
            if (vm is null) return;

            vm.UpdateLanePreferredRatios(lane.ParentId, lane.LaneIndex, updates);
        }
    }
}
