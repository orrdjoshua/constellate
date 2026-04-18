using System;
using Avalonia.Input;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class PaneGestureSessionCoordinator
{
    public static bool Start<TSession>(
        ref TSession? session,
        TSession newSession,
        PointerPressedEventArgs e,
        Action<PointerEventArgs> capturePointer,
        bool markHandled = true)
        where TSession : PaneGestureSession
    {
        session = newSession ?? throw new ArgumentNullException(nameof(newSession));
        capturePointer(e);

        if (markHandled)
        {
            e.Handled = true;
        }

        return true;
    }

    public static void Finish<TSession>(ref TSession? session, bool commit)
        where TSession : PaneGestureSession
    {
        if (session is null)
        {
            return;
        }

        if (commit)
        {
            session.Commit();
        }
        else
        {
            session.Cancel();
        }

        session = null;
    }
}
