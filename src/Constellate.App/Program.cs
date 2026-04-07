using Avalonia;

namespace Constellate.App
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
#if DEBUG
            try
            {
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
                System.Diagnostics.Trace.AutoFlush = true;
            }
            catch { /* ignore */ }
#endif
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToTrace();
    }
}
