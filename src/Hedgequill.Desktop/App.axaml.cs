using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hedgequill.Desktop.Themes;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (Motion.On)
        {
            Styles.Add(new MotionStyles());
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // HEDGEQUILL_SETTINGS points the Recent list and settings.json elsewhere, so the end-to-end tests never touch the writer's.
            var window = Environment.GetEnvironmentVariable("HEDGEQUILL_SETTINGS") is { Length: > 0 } settings ? new MainWindow(settings) : new MainWindow();
            // A project folder to open: Restart to finish updating passes the one that was open.
            if (desktop.Args is [var folder] && Directory.Exists(folder))
            {
                window.Open(folder);
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
