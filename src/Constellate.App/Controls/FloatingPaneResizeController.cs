using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneResizeController
    {
        public static void AttachResizeGrips(
            Border chrome,
            Canvas canvas,
            Panel panel,
            FloatingPaneSurfaceController surfaceController)
        {
            if (panel.Classes.Contains("floatingPaneResizeHost"))
            {
                return;
            }

            panel.Classes.Add("floatingPaneResizeHost");

            foreach (var spec in GripSpecs.All)
            {
                AddGrip(panel, chrome, canvas, surfaceController, spec);
            }
        }

        private static void AddGrip(
            Panel panel,
            Border chrome,
            Canvas canvas,
            FloatingPaneSurfaceController surfaceController,
            GripSpec spec)
        {
            var normalBrush = new SolidColorBrush(Color.FromArgb(1, 255, 196, 138));
            var hoverBrush = new SolidColorBrush(Color.FromArgb(110, 255, 196, 138));
            var pressedBrush = new SolidColorBrush(Color.FromArgb(150, 0, 211, 255));
            var transparentBrush = Brushes.Transparent;

            var grip = new Border
            {
                Background = normalBrush,
                HorizontalAlignment = spec.HorizontalAlignment,
                VerticalAlignment = spec.VerticalAlignment,
                Width = spec.Width > 0 ? spec.Width : double.NaN,
                Height = spec.Height > 0 ? spec.Height : double.NaN,
                Cursor = new Cursor(spec.Cursor),
                BorderBrush = transparentBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            grip.Classes.Add("floatingPaneResizeGrip");

            var isPressed = false;

            grip.PointerEntered += (_, __) =>
            {
                surfaceController.BringToFront(chrome);
                SetGripVisual(grip, hoverBrush, hoverBrush);
            };

            grip.PointerExited += (_, __) =>
            {
                if (!isPressed)
                {
                    SetGripVisual(grip, normalBrush, transparentBrush);
                }
            };

            grip.PointerPressed += (_, e) =>
            {
                surfaceController.BringToFront(chrome);

                if (!e.GetCurrentPoint(chrome).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                isPressed = true;
                SetGripVisual(grip, pressedBrush, pressedBrush);

                try
                {
                    e.Pointer.Capture(grip);
                }
                catch
                {
                }

                var start = e.GetPosition(canvas);
                var container = canvas.Bounds;

                var initialX = Canvas.GetLeft(chrome);
                var initialY = Canvas.GetTop(chrome);
                var initialWidth = chrome.Bounds.Width;
                var initialHeight = chrome.Bounds.Height;

                const double minWidth = 80.0;
                const double minHeight = 80.0;

                void OnMove(object? _, PointerEventArgs args)
                {
                    if (!ReferenceEquals(args.Pointer.Captured, grip))
                    {
                        return;
                    }

                    var point = args.GetPosition(canvas);
                    var dx = point.X - start.X;
                    var dy = point.Y - start.Y;

                    var nextX = initialX;
                    var nextY = initialY;
                    var nextWidth = initialWidth;
                    var nextHeight = initialHeight;

                    if (spec.Left)
                    {
                        nextX = Math.Clamp(initialX + dx, 0, initialX + initialWidth - minWidth);
                        nextWidth = Math.Max(minWidth, (initialX + initialWidth) - nextX);
                    }

                    if (spec.Top)
                    {
                        nextY = Math.Clamp(initialY + dy, 0, initialY + initialHeight - minHeight);
                        nextHeight = Math.Max(minHeight, (initialY + initialHeight) - nextY);
                    }

                    if (spec.Right)
                    {
                        nextWidth = Math.Clamp(initialWidth + dx, minWidth, Math.Max(minWidth, container.Width - initialX));
                    }

                    if (spec.Bottom)
                    {
                        nextHeight = Math.Clamp(initialHeight + dy, minHeight, Math.Max(minHeight, container.Height - initialY));
                    }

                    nextX = Math.Clamp(nextX, 0, Math.Max(0, container.Width - nextWidth));
                    nextY = Math.Clamp(nextY, 0, Math.Max(0, container.Height - nextHeight));

                    Canvas.SetLeft(chrome, nextX);
                    Canvas.SetTop(chrome, nextY);
                    chrome.Width = nextWidth;
                    chrome.Height = nextHeight;

                    surfaceController.CommitFloatingGeometry(chrome, nextX, nextY, nextWidth, nextHeight);
                }

                void OnRelease(object? _, PointerReleasedEventArgs args)
                {
                    try
                    {
                        args.Pointer.Capture(null);
                    }
                    catch
                    {
                    }

                    isPressed = false;
                    SetGripVisual(
                        grip,
                        grip.IsPointerOver ? hoverBrush : normalBrush,
                        grip.IsPointerOver ? hoverBrush : transparentBrush);

                    grip.PointerMoved -= OnMove;
                    grip.PointerReleased -= OnRelease;
                }

                grip.PointerMoved += OnMove;
                grip.PointerReleased += OnRelease;
                e.Handled = true;
            };

            panel.Children.Add(grip);
        }

        private static void SetGripVisual(Border grip, IBrush background, IBrush borderBrush)
        {
            grip.Background = background;
            grip.BorderBrush = borderBrush;
        }

        private readonly struct GripSpec
        {
            public GripSpec(
                HorizontalAlignment horizontalAlignment,
                VerticalAlignment verticalAlignment,
                double width,
                double height,
                StandardCursorType cursor,
                bool left,
                bool top,
                bool right,
                bool bottom)
            {
                HorizontalAlignment = horizontalAlignment;
                VerticalAlignment = verticalAlignment;
                Width = width;
                Height = height;
                Cursor = cursor;
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public HorizontalAlignment HorizontalAlignment { get; }

            public VerticalAlignment VerticalAlignment { get; }

            public double Width { get; }

            public double Height { get; }

            public StandardCursorType Cursor { get; }

            public bool Left { get; }

            public bool Top { get; }

            public bool Right { get; }

            public bool Bottom { get; }
        }

        private static class GripSpecs
        {
            public static readonly GripSpec[] All =
            {
                new(
                    HorizontalAlignment.Stretch,
                    VerticalAlignment.Top,
                    0,
                    8,
                    StandardCursorType.SizeNorthSouth,
                    left: false,
                    top: true,
                    right: false,
                    bottom: false),
                new(
                    HorizontalAlignment.Stretch,
                    VerticalAlignment.Bottom,
                    0,
                    8,
                    StandardCursorType.SizeNorthSouth,
                    left: false,
                    top: false,
                    right: false,
                    bottom: true),
                new(
                    HorizontalAlignment.Left,
                    VerticalAlignment.Stretch,
                    8,
                    0,
                    StandardCursorType.SizeWestEast,
                    left: true,
                    top: false,
                    right: false,
                    bottom: false),
                new(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Stretch,
                    8,
                    0,
                    StandardCursorType.SizeWestEast,
                    left: false,
                    top: false,
                    right: true,
                    bottom: false),
                new(
                    HorizontalAlignment.Left,
                    VerticalAlignment.Top,
                    14,
                    14,
                    StandardCursorType.TopLeftCorner,
                    left: true,
                    top: true,
                    right: false,
                    bottom: false),
                new(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Top,
                    14,
                    14,
                    StandardCursorType.TopRightCorner,
                    left: false,
                    top: true,
                    right: true,
                    bottom: false),
                new(
                    HorizontalAlignment.Left,
                    VerticalAlignment.Bottom,
                    14,
                    14,
                    StandardCursorType.BottomLeftCorner,
                    left: true,
                    top: false,
                    right: false,
                    bottom: true),
                new(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Bottom,
                    14,
                    14,
                    StandardCursorType.BottomRightCorner,
                    left: false,
                    top: false,
                    right: true,
                    bottom: true)
            };
        }
    }
}
