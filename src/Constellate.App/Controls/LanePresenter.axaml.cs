using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Renders a single lane (either a column of vertically-flowing children for Left/Right parents,
    /// or a row of horizontally-flowing children for Top/Bottom parents).
    ///
    /// Child ratios are interpreted as occupancy against the lane's fixed viewport size:
    /// - ratio 0.25 = 25% of the visible fixed viewport,
    /// - ratio 1.25 = 125% of the visible fixed viewport.
    ///
    /// This presenter now uses the authoritative per-child FixedSizePixels for layout. Ratios remain
    /// only as a migration fallback. When total child pixels (plus splitters) do not fill the lane's
    /// fixed viewport, filler is added; if they exceed it, the lane scrolls.
    /// </summary>
    public sealed class LanePresenter : UserControl
    {
        private const double SplitterThickness = 6.0;
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
            if (TopLevel.GetTopLevel(this) is Window w && w.DataContext is MainWindowViewModel vm)
            {
                return vm;
            }

            if (VisualRoot is Window vw && vw.DataContext is MainWindowViewModel vvm)
            {
                return vvm;
            }

            return null;
        }

        private void Rebuild()
        {
            if (_root is null)
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
            var fixedViewport = lane.FixedViewportSize > 0
                ? lane.FixedViewportSize
                : (lane.IsVerticalFlow ? viewportHeight : viewportWidth);

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
                ColumnSpacing = 0,
                RowSpacing = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = viewportWidth,
                MinHeight = viewportHeight
            };

            var children = lane.Children ?? Array.Empty<ChildPaneDescriptor>();
            var count = children.Count;

            // Determine per-child fixed pixels, migrating from historical ratios when needed.
            var childPixels = new double[count];
            for (var i = 0; i < count; i++)
            {
                var c = children[i];
                if (c.FixedSizePixels > 0.0)
                {
                    childPixels[i] = c.FixedSizePixels;
                }
                else
                {
                    // Migration path: use any prior ratio against the current fixed viewport;
                    // if no ratio was meaningful, seed a conservative default of 25%.
                    var r = 0.25;
                    if (lane.Ratios is { } rr && i < rr.Count && rr[i] > 0.0001)
                        r = Math.Clamp(rr[i], 0.05, 0.95);
                    childPixels[i] = Math.Max(1.0, fixedViewport * r);
                }
            }

            var splitterTotal = Math.Max(0, count - 1) * SplitterThickness;
            var totalPixels = childPixels.Sum();
            var occupancyPixels = totalPixels;

            Debug.WriteLine(
                $"[LaneViewport] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                $"laneViewportW={viewportWidth:0.##} laneViewportH={viewportHeight:0.##} " +
                $"fixedViewport={lane.FixedViewportSize:0.##} adjustableViewport={lane.AdjustableViewportSize:0.##} " +
                $"children={count} childPixels=[{string.Join(",", childPixels.Select(p => p.ToString("0.##")))}]");

            if (count == 0)
            {
                grid.Width = viewportWidth;
                grid.Height = viewportHeight;
                scroll.Content = grid;
                _root.Child = scroll;
                return;
            }

            // MVP rule: splitters alone should not force a scrollbar when content does not fill the lane.
            // We therefore use:
            // - when (total child pixels + splitters) <= fixedViewport: allocate filler.
            // - else: content naturally exceeds lane and scrolls.
            var fittingWithinViewport = (totalPixels + splitterTotal) <= fixedViewport + 1e-6;
            var remainingForFiller = fittingWithinViewport
                ? Math.Max(0.0, fixedViewport - splitterTotal - totalPixels)
                : 0.0;

            if (lane.IsVerticalFlow)
            {
                grid.Width = viewportWidth;
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(viewportWidth, GridUnitType.Pixel)));

                var contentHeight = 0.0;

                for (var i = 0; i < count; i++)
                {
                    var cp = Math.Max(1.0, childPixels[i]);
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(cp, GridUnitType.Pixel)));
                    contentHeight += cp;

                    var chrome = new Border
                    {
                        Background = Brushes.Transparent,
                        Width = viewportWidth,
                        Child = new ChildPaneView { DataContext = children[i] }
                    };

                    Grid.SetColumn(chrome, 0);
                    Grid.SetRow(chrome, i * 2);
                    grid.Children.Add(chrome);

                    if (i < count - 1)
                    {
                        grid.RowDefinitions.Add(new RowDefinition(new GridLength(SplitterThickness, GridUnitType.Pixel)));
                        contentHeight += SplitterThickness;

                        var splitter = new GridSplitter
                        {
                            ResizeDirection = GridResizeDirection.Rows,
                            Background = Brushes.Transparent,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = SplitterThickness,
                            Cursor = new Cursor(StandardCursorType.SizeNorthSouth)
                        };
                        // Hover affordance for intermediate splitters
                        splitter.PointerEntered += (_, __) => splitter.Background = new SolidColorBrush(Color.Parse("#20FFC48A"));
                        splitter.PointerExited  += (_, __) => splitter.Background = Brushes.Transparent;

                        AttachSplitterPersistence(splitter, lane, grid, $"mid-{i}");
                        Grid.SetColumn(splitter, 0);
                        Grid.SetRow(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                // Trailing resize between last child and remainder/filler so the last/only child is also resizable.
                if (fittingWithinViewport && remainingForFiller > 1.0)
                {
                    // Insert a splitter before the filler so last child can be grown/shrunk against remainder.
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(SplitterThickness, GridUnitType.Pixel)));
                    var tailSplitter = new GridSplitter
                    {
                        ResizeDirection = GridResizeDirection.Rows,
                        Background = Brushes.Transparent,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Height = SplitterThickness,
                        Cursor = new Cursor(StandardCursorType.SizeNorthSouth)
                    };
                    AttachSplitterPersistence(tailSplitter, lane, grid, "tail");
                    // Hover highlight for affordance
                    tailSplitter.PointerEntered += (_, __) => tailSplitter.Background = new SolidColorBrush(Color.Parse("#20FFC48A"));
                    tailSplitter.PointerExited +=  (_, __) => tailSplitter.Background = Brushes.Transparent;
                    Grid.SetColumn(tailSplitter, 0);
                    Grid.SetRow(tailSplitter, (count - 1) * 2 + 1); // after the last child
                    grid.Children.Add(tailSplitter);
                }

                if (fittingWithinViewport && remainingForFiller > 1.0)
                {
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(remainingForFiller, GridUnitType.Pixel)));
                    contentHeight += remainingForFiller;
                }

                grid.Height = Math.Max(viewportHeight, contentHeight);
            }
            else
            {
                grid.Height = viewportHeight;
                grid.RowDefinitions.Add(new RowDefinition(new GridLength(viewportHeight, GridUnitType.Pixel)));

                var contentWidth = 0.0;

                for (var i = 0; i < count; i++)
                {
                    var cp = Math.Max(1.0, childPixels[i]);
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(cp, GridUnitType.Pixel)));
                    contentWidth += cp;

                    var chrome = new Border
                    {
                        Background = Brushes.Transparent,
                        Height = viewportHeight,
                        Child = new ChildPaneView { DataContext = children[i] }
                    };

                    Grid.SetRow(chrome, 0);
                    Grid.SetColumn(chrome, i * 2);
                    grid.Children.Add(chrome);

                    if (i < count - 1)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SplitterThickness, GridUnitType.Pixel)));
                        contentWidth += SplitterThickness;

                        var splitter = new GridSplitter
                        {
                            ResizeDirection = GridResizeDirection.Columns,
                            Background = Brushes.Transparent,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Width = SplitterThickness,
                            Cursor = new Cursor(StandardCursorType.SizeWestEast)
                        };
                        // Hover affordance for intermediate splitters
                        splitter.PointerEntered += (_, __) => splitter.Background = new SolidColorBrush(Color.Parse("#20313A46"));
                        splitter.PointerExited  += (_, __) => splitter.Background = Brushes.Transparent;

                        AttachSplitterPersistence(splitter, lane, grid, $"mid-{i}");
                        Grid.SetRow(splitter, 0);
                        Grid.SetColumn(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                // Trailing resize between last child and remainder/filler so the last/only child is also resizable.
                if (fittingWithinViewport && remainingForFiller > 1.0)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SplitterThickness, GridUnitType.Pixel)));
                    var tailSplitter = new GridSplitter
                    {
                        ResizeDirection = GridResizeDirection.Columns,
                        Background = Brushes.Transparent,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Width = SplitterThickness,
                        Cursor = new Cursor(StandardCursorType.SizeWestEast)
                    };
                    AttachSplitterPersistence(tailSplitter, lane, grid, "tail");
                    // Hover highlight for affordance
                    tailSplitter.PointerEntered += (_, __) => tailSplitter.Background = new SolidColorBrush(Color.Parse("#20FFC48A"));
                    tailSplitter.PointerExited  += (_, __) => tailSplitter.Background = Brushes.Transparent;
                    Grid.SetRow(tailSplitter, 0);
                    Grid.SetColumn(tailSplitter, (count - 1) * 2 + 1); // after the last child
                    grid.Children.Add(tailSplitter);
                }

                if (fittingWithinViewport && remainingForFiller > 1.0)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(remainingForFiller, GridUnitType.Pixel)));
                    contentWidth += remainingForFiller;
                }

                grid.Width = Math.Max(viewportWidth, contentWidth);
            }

            scroll.Content = grid;
            _root.Child = scroll;

            // Migration: if any child had no FixedSizePixels, persist the resolved pixel sizes now.
            if (children.Any(c => c.FixedSizePixels <= 0.0))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    PersistFixedSizes(lane, (Grid)scroll.Content!);
                }, DispatcherPriority.Background);
            }
        }

        private void AttachSplitterPersistence(GridSplitter splitter, LaneView lane, Grid grid, string role)
        {
            splitter.PointerPressed += (_, __) =>
            {
                Debug.WriteLine(
                    $"[LanePersist][GripPress] parent={lane.ParentId} lane={lane.LaneIndex} role={role} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")}");
            };

            splitter.PointerReleased += (_, __) =>
            {
                Debug.WriteLine(
                    $"[LanePersist][GripRelease] parent={lane.ParentId} lane={lane.LaneIndex} role={role} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")}");
                QueuePersistFixedSizes(lane, grid);
            };

            splitter.PointerCaptureLost += (_, __) =>
            {
                Debug.WriteLine(
                    $"[LanePersist][GripCaptureLost] parent={lane.ParentId} lane={lane.LaneIndex} role={role} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")}");
                QueuePersistFixedSizes(lane, grid);
            };
        }

        private void QueuePersistFixedSizes(LaneView lane, Grid grid)
        {
            Debug.WriteLine(
                $"[LanePersist][Queue] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                $"children=[{string.Join(",", (lane.Children ?? Array.Empty<ChildPaneDescriptor>()).Select(c => $"{c.Id}:{c.FixedSizePixels:0.##}"))}]");

            Dispatcher.UIThread.Post(
                () => PersistFixedSizes(lane, grid),
                DispatcherPriority.Background);
        }

        private void PersistFixedSizes(LaneView lane, Grid grid)
        {
            var list = new List<(string childId, double pixels)>();
            var children = lane.Children ?? Array.Empty<ChildPaneDescriptor>();
            var currentFixed = string.Join(",", children.Select(c => $"{c.Id}:{c.FixedSizePixels:0.##}"));

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                double size;

                if (lane.IsVerticalFlow)
                {
                    var rowIndex = i * 2;
                    size = rowIndex < grid.RowDefinitions.Count
                        ? Math.Max(1.0, grid.RowDefinitions[rowIndex].ActualHeight)
                        : 0.0;
                }
                else
                {
                    var colIndex = i * 2;
                    size = colIndex < grid.ColumnDefinitions.Count
                        ? Math.Max(1.0, grid.ColumnDefinitions[colIndex].ActualWidth)
                        : 0.0;
                }

                if (size <= 0.0)
                {
                    // Fall back to 25% of current fixed viewport if we could not read a realized size
                    var fallback = Math.Max(1.0, (lane.FixedViewportSize > 0 ? lane.FixedViewportSize :
                        (lane.IsVerticalFlow ? Math.Max(1.0, lane.ViewportHeight) : Math.Max(1.0, lane.ViewportWidth))) * 0.25);
                    list.Add((child.Id, fallback));
                    continue;
                }

                list.Add((child.Id, size));
            }

            Debug.WriteLine(
                $"[LanePersist][Measure] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                $"currentFixed=[{currentFixed}] " +
                $"measured=[{string.Join(",", list.Select(x => $"{x.childId}:{x.pixels:0.##}"))}] " +
                $"rowDefs={grid.RowDefinitions.Count} colDefs={grid.ColumnDefinitions.Count}");

            var vm = GetVm();
            if (vm is null)
            {
                Debug.WriteLine(
                    $"[LanePersist][Apply][VM_NULL] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                    $"measured=[{string.Join(",", list.Select(x => $"{x.childId}:{x.pixels:0.##}"))}]");
                return;
            }

            Debug.WriteLine(
                $"[LanePersist][Apply][VM_OK] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} count={list.Count}");

            vm.UpdateLaneFixedSizesPixels(lane.ParentId, lane.LaneIndex, list);
        }
    }
}
