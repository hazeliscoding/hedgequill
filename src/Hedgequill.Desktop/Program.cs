using Avalonia;
using Hedgequill.Desktop.Themes;

namespace Hedgequill.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // shortcut: read once at launch, so a change made mid-session applies next time; re-read on window activation if writers ask.
        Motion.On = !ReducedMotion.Asked();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
