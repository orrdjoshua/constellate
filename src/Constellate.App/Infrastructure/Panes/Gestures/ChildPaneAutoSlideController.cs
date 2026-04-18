using System;
using Avalonia.Threading;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    internal sealed class ChildPaneAutoSlideController
    {
        private readonly DispatcherTimer _timer;
        private string? _parentId;
        private int _direction;
        private DateTime _lastTriggeredAtUtc;
        private Action<string, int>? _onTriggered;

        public ChildPaneAutoSlideController()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += OnTimerTick;
        }

        public void Request(string parentId, int direction, Action<string, int> onTriggered)
        {
            if (string.IsNullOrWhiteSpace(parentId) || direction == 0)
            {
                Stop();
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastTriggeredAtUtc).TotalMilliseconds < 450 &&
                string.Equals(_parentId, parentId, StringComparison.Ordinal) &&
                _direction == direction)
            {
                return;
            }

            _parentId = parentId;
            _direction = direction;
            _onTriggered = onTriggered;

            _timer.Stop();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            _parentId = null;
            _direction = 0;
            _onTriggered = null;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _timer.Stop();

            if (string.IsNullOrWhiteSpace(_parentId) ||
                _direction == 0 ||
                _onTriggered is null)
            {
                return;
            }

            var parentId = _parentId;
            var direction = _direction;
            var callback = _onTriggered;

            _lastTriggeredAtUtc = DateTime.UtcNow;
            callback(parentId, direction);
        }
    }
}
