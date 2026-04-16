using System;
using System.Collections.Generic;
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
    /// The presenter applies per-child proportional sizing along the lane’s free dimension using star Grid sizing
    /// based on each child's PreferredSizeRatio (normalized with clamping), and includes GridSplitters
    /// so users can drag-resize children. After resize, ratios are recomputed and persisted to the VM.
    /// </summary>
    public sealed class LanePresenter : UserControl
    {
        private Border? _root;

        public LanePresenter()
        {
            // Build a root that we replace as DataContext changes
            _root = new Border { Background = Brushes.Transparent };
            Content = _root;
            DataContextChanged += (_, __) => Rebuild();
            AttachedToVisualTree += (_, __) => Rebuild();
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

            // Create ScrollViewer per lane with proper scroll orientation
            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = lane.IsHorizontalScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = lane.IsVerticalScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };

            // Grid that hosts children with star sizing along the lane's free dimension
            var grid = new Grid
            {
                ColumnSpacing = 6,
                RowSpacing = 6
            };

            var children = lane.Children ?? Array.Empty<ChildPaneDescriptor>();
            var ratios = lane.Ratios ?? Array.Empty<double>();
            var count = Math.Min(children.Count, ratios.Count);

            if (count == 0)
            {
                // Empty lane -> nothing to render (still have scroll container)
                scroll.Content = grid;
                _root.Child = scroll;
                return;
            }

            if (lane.IsVerticalFlow)
            {
                // Vertical flow: one column; interleave star rows with splitter rows
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                // Build 2*count-1 rows (child, splitter, child, splitter, ...)
                for (int i = 0; i < count; i++)
                {
                    var star = Math.Max(0.0001, ratios[i]); // safety clamp
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(star, GridUnitType.Star)));
                    var chrome = new Border
                    {
                        Background = Brushes.Transparent,
                        Child = new ChildPaneView { DataContext = children[i] }
                    };
                    Grid.SetColumn(chrome, 0);
                    Grid.SetRow(chrome, i * 2);
                    grid.Children.Add(chrome);

                    // Add splitter row after each child except the last
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
            }
            else
            {
                // Horizontal flow: one row; interleave star columns with splitter columns
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
            }

            scroll.Content = grid;
            _root.Child = scroll;
        }

        private void PersistRatios(LaneView lane, Grid grid)
        {
            // Measure displayed child sizes in the free dimension
            var list = new List<(string childId, double size)>();
            foreach (var child in lane.Children ?? Array.Empty<ChildPaneDescriptor>())
            {
                // Find the Border that wraps this child's ChildPaneView
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
