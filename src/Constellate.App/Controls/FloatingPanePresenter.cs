using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal sealed class FloatingPanePresenter
    {
        private readonly Canvas _canvas;
        private readonly FloatingPaneSurfaceController _surfaceController;
        private readonly Dictionary<string, Border> _chromeBySurfaceKey = new(StringComparer.Ordinal);

        public FloatingPanePresenter(
            Canvas canvas,
            FloatingPaneSurfaceController surfaceController)
        {
            _canvas = canvas;
            _surfaceController = surfaceController;
        }

        public void Clear()
        {
            _canvas.Children.Clear();
            _chromeBySurfaceKey.Clear();
        }

        public void Synchronize(FloatingPaneSurfaceModel surfaceModel)
        {
            var desiredKeys = new HashSet<string>(
                surfaceModel.Entries.Select(entry => entry.SurfaceKey),
                StringComparer.Ordinal);

            foreach (var entry in surfaceModel.Entries)
            {
                if (!_chromeBySurfaceKey.TryGetValue(entry.SurfaceKey, out var chrome))
                {
                    chrome = FloatingPaneChromeFactory.CreateChromeForEntry(entry);

                    if (chrome is null)
                    {
                        continue;
                    }

                    _chromeBySurfaceKey[entry.SurfaceKey] = chrome;
                }
                else
                {
                    FloatingPaneChromeFactory.UpdateChromeDataContext(chrome, entry);
                }

                FloatingPaneInteractionController.AttachInteractions(
                    chrome,
                    _canvas,
                    _surfaceController);

                FloatingPaneChromeFactory.ApplySurfaceEntry(chrome, entry);

                if (_canvas.Children.IndexOf(chrome) < 0)
                {
                    _canvas.Children.Add(chrome);
                }
            }

            foreach (var pair in _chromeBySurfaceKey.ToArray())
            {
                if (desiredKeys.Contains(pair.Key))
                {
                    continue;
                }

                RemoveChrome(pair.Key, pair.Value);
            }
        }

        private void RemoveChrome(string surfaceKey, Border chrome)
        {
            _canvas.Children.Remove(chrome);
            _chromeBySurfaceKey.Remove(surfaceKey);
        }
    }
}
