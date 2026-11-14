using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Screenshots;

internal static class Program
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "screenshots");
        Directory.CreateDirectory(output);
        // An empty Recent list and an invented project, never the real ones.
        var scratch = Directory.CreateTempSubdirectory("hedgequill-screenshots-");
        try
        {
            var sample = Sample(Path.Combine(scratch.FullName, "stories"));
            BuildAvaloniaApp().SetupWithoutStarting();

            foreach (var (theme, suffix) in new[] { (ThemeVariant.Light, "light"), (ThemeVariant.Dark, "dark") })
            {
                Application.Current!.RequestedThemeVariant = theme;
                Capture(output, $"welcome-{suffix}.png", Path.Combine(scratch.FullName, "settings-empty"), _ => { });
                Capture(output, $"workspace-{suffix}.png", Path.Combine(scratch.FullName, "settings"), window => window.Open(sample));
            }
        }
        finally
        {
            scratch.Delete(recursive: true);
        }

        Console.WriteLine($"Screenshots written to {output}");
        return 0;
    }

    private static void Capture(string output, string name, string settings, Action<MainWindow> arrange)
    {
        var window = new MainWindow(new RecentProjects(settings)) { Width = 1280, Height = 800 };
        window.Show();
        arrange(window);
        Settle(TimeSpan.FromSeconds(1));
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(Path.Combine(output, name), new PngBitmapEncoderOptions());
        window.Close();
        Console.WriteLine($"  {name}");
    }

    private static string Sample(string location)
    {
        var project = Project.Create(location, "The Lantern Keeper");
        var folder = project.Folder;
        Directory.Delete(Path.Combine(folder, "Chapter 1"), recursive: true);
        Write(folder, "Prologue/Fog on the water.md", """
            The fog came in before the boats did. It rolled over the harbour wall in one long breath, and by the time Mara reached the lantern room the far shore had gone.

            She lit the lamp anyway. *Someone is always out there*, her mother used to say, *even when you can't see them.*

            ---

            The bell rang at half past nine. Twice, then nothing.
            """);
        Write(folder, "Act One/The Letter/A knock at dusk.md", "Mara heard the knock twice before she believed it.");
        Write(folder, "Act One/The Letter/What the seal said.md", "The wax was green, which meant the harbour office.");
        Write(folder, "Act One/Low Tide/The causeway.md", "At low tide the island was not an island at all.");
        Write(folder, "Notes/Mara.md", "Keeper of the lantern since she was nineteen.");
        Write(folder, "Research/Tides.md", "Spring tides follow the full and new moon.");
        File.WriteAllText(Path.Combine(folder, Project.IndexFile), """{ "format": 1, "order": { "": ["Prologue", "Act One"], "Act One": ["The Letter", "Low Tide"] } }""");
        return folder;
    }

    private static void Write(string folder, string relative, string text)
    {
        var path = Path.Combine([folder, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Settle(TimeSpan time)
    {
        var until = DateTime.UtcNow + time;
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(15);
        }
    }
}
