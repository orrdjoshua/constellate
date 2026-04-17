using System;
using Avalonia;
using Avalonia.Input;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public enum PaneGestureKind
    {
        None = 0,
        ParentMove = 1,
        ParentResize = 2,
        ChildDrag = 3
    }

    public abstract class PaneGestureSession
    {
        protected PaneGestureSession(
            PaneGestureKind kind,
            string paneId,
            long pointerId,
            Point startPoint)
        {
            Kind = kind;
            PaneId = paneId ?? string.Empty;
            PointerId = pointerId;
            StartPoint = startPoint;
            CurrentPoint = startPoint;
            StartedAtUtc = DateTimeOffset.UtcNow;
            IsActive = true;
        }

        public PaneGestureKind Kind { get; }

        public string PaneId { get; }

        public long PointerId { get; }

        public Point StartPoint { get; }

        public Point CurrentPoint { get; private set; }

        public DateTimeOffset StartedAtUtc { get; }

        public bool IsActive { get; private set; }

        public bool IsCommitted { get; private set; }

        public bool MatchesPointer(long pointerId)
        {
            return PointerId == pointerId;
        }

        public bool MatchesPointer(PointerEventArgs e)
        {
            return e is not null && MatchesPointer((long)e.Pointer.Id);
        }

        public virtual void UpdatePointer(Point currentPoint)
        {
            CurrentPoint = currentPoint;
        }

        public virtual void Commit()
        {
            IsActive = false;
            IsCommitted = true;
        }

        public virtual void Cancel()
        {
            IsActive = false;
            IsCommitted = false;
        }
    }
}
