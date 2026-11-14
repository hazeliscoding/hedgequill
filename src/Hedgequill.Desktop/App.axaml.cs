using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // HEDGEQUILL_SETTINGS points the Recent list and settings.json elsewhere, so the end-to-end tests never touch the writer's.
            desktop.MainWindow = Environment.GetEnvironmentVariable("HEDGEQUILL_SETTINGS") is { Length: > 0 } settings ? new MainWindow(settings) : new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
