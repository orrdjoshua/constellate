using System.Diagnostics;
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
    ///
    /// Child ratios are interpreted as occupancy against the lane's fixed viewport size:
    /// - ratio 0.25 = 25% of the visible fixed viewport,
    /// - ratio 1.25 = 125% of the visible fixed viewport.
    ///
    /// Therefore:
    /// - total occupancy <= 1.0 leaves unused remainder/filler inside the lane viewport,
    /// - total occupancy > 1.0 makes the lane content larger than the viewport and the lane scrolls,
    /// - the ParentPane header remains sticky because scrolling is isolated to the lane/body region.
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
            if (VisualRoot is Window w && w.DataContext is MainWindowViewModel vm)
            {
                return vm;
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
            var ratios = lane.Ratios ?? Array.Empty<double>();
            var count = Math.Min(children.Count, ratios.Count);

            var occupancySum = ratios.Take(count).Sum(r => Math.Max(0.05, r));

            Debug.WriteLine(
                $"[LaneViewport] parent={lane.ParentId} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                $"laneViewportW={viewportWidth:0.##} laneViewportH={viewportHeight:0.##} " +
                $"fixedViewport={lane.FixedViewportSize:0.##} adjustableViewport={lane.AdjustableViewportSize:0.##} " +
                $"children={count} occupancy={occupancySum:0.###} ratios=[{string.Join(",", ratios.Take(count).Select(r => r.ToString("0.###")))}]");

            if (count == 0)
            {
                grid.Width = viewportWidth;
                grid.Height = viewportHeight;
                scroll.Content = grid;
                _root.Child = scroll;
                return;
            }

            var splitterTotal = Math.Max(0, count - 1) * SplitterThickness;

            // Important MVP rule:
            // - until content genuinely exceeds the visible lane fixed viewport,
            //   the presence of splitters alone should not force a scrollbar.
            // Therefore, when total occupancy <= 1.0, we fit children + splitters inside
            // the visible fixed viewport by reserving splitter space first and then applying
            // ratios to the remaining fixed-size budget.
            //
            // Once occupancy > 1.0, children are realized directly against the full fixed viewport,
            // causing content extent to exceed the lane and naturally enabling scrolling.
            var fittingWithinViewport = occupancySum <= 1.0 + 1e-6;
            var childBudgetForViewport = fittingWithinViewport
                ? Math.Max(0.0, fixedViewport - splitterTotal)
                : fixedViewport;

            if (lane.IsVerticalFlow)
            {
                grid.Width = viewportWidth;
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(viewportWidth, GridUnitType.Pixel)));

                var contentHeight = 0.0;

                for (var i = 0; i < count; i++)
                {
                    var ratio = Math.Max(0.05, ratios[i]);
                    var childPixels = childBudgetForViewport * ratio;
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(childPixels, GridUnitType.Pixel)));
                    contentHeight += childPixels;

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

                        splitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                        Grid.SetColumn(splitter, 0);
                        Grid.SetRow(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                // Trailing resize between last child and remainder/filler so the last/only child is also resizable.
                if (fittingWithinViewport && occupancySum < 1.0 - 1e-6)
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
                    tailSplitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                    // Hover highlight for affordance
                    tailSplitter.PointerEntered += (_, __) => tailSplitter.Background = new SolidColorBrush(Color.Parse("#20FFC48A"));
                    tailSplitter.PointerExited +=  (_, __) => tailSplitter.Background = Brushes.Transparent;
                    Grid.SetColumn(tailSplitter, 0);
                    Grid.SetRow(tailSplitter, (count - 1) * 2 + 1); // after the last child
                    grid.Children.Add(tailSplitter);
                }

                if (fittingWithinViewport && occupancySum < 1.0 - 1e-6)
                {
                    var fillerPixels = childBudgetForViewport * (1.0 - occupancySum);
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(fillerPixels, GridUnitType.Pixel)));
                    contentHeight += fillerPixels;
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
                    var ratio = Math.Max(0.05, ratios[i]);
                    var childPixels = childBudgetForViewport * ratio;
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(childPixels, GridUnitType.Pixel)));
                    contentWidth += childPixels;

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

                        splitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                        Grid.SetRow(splitter, 0);
                        Grid.SetColumn(splitter, i * 2 + 1);
                        grid.Children.Add(splitter);
                    }
                }

                // Trailing resize between last child and remainder/filler so the last/only child is also resizable.
                if (fittingWithinViewport && occupancySum < 1.0 - 1e-6)
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
                    tailSplitter.PointerReleased += (_, __) => PersistRatios(lane, grid);
                    // Hover highlight for affordance
                    tailSplitter.PointerEntered += (_, __) => tailSplitter.Background = new SolidColorBrush(Color.Parse("#20FFC48A"));
                    tailSplitter.PointerExited  += (_, __) => tailSplitter.Background = Brushes.Transparent;
                    Grid.SetRow(tailSplitter, 0);
                    Grid.SetColumn(tailSplitter, (count - 1) * 2 + 1); // after the last child
                    grid.Children.Add(tailSplitter);
                }

                if (fittingWithinViewport && occupancySum < 1.0 - 1e-6)
                {
                    var fillerPixels = childBudgetForViewport * (1.0 - occupancySum);
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(fillerPixels, GridUnitType.Pixel)));
                    contentWidth += fillerPixels;
                }

                grid.Width = Math.Max(viewportWidth, contentWidth);
            }

            scroll.Content = grid;
            _root.Child = scroll;
        }

        private void PersistRatios(LaneView lane, Grid grid)
        {
            var fixedViewport = lane.FixedViewportSize > 0
                ? lane.FixedViewportSize
                : (lane.IsVerticalFlow ? Math.Max(1.0, lane.ViewportHeight) : Math.Max(1.0, lane.ViewportWidth));

            if (fixedViewport <= 1e-6)
            {
                return;
            }

            var list = new List<(string childId, double occupancy)>();

            foreach (var child in lane.Children ?? Array.Empty<ChildPaneDescriptor>())
            {
                var chrome = grid.Children
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Child is ChildPaneView cpv && ReferenceEquals(cpv.DataContext, child));

                if (chrome is null)
                {
                    list.Add((child.Id, 0.25));
                }
                else
                {
                    var size = lane.IsVerticalFlow
                        ? Math.Max(1.0, chrome.Bounds.Height)
                        : Math.Max(1.0, chrome.Bounds.Width);

                    list.Add((child.Id, size / fixedViewport));
                }
            }

            var vm = GetVm();
            if (vm is null)
            {
                return;
            }

            vm.UpdateLanePreferredRatios(lane.ParentId, lane.LaneIndex, list);
        }
    }
}
