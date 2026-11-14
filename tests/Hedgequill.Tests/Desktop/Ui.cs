using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Tests.Desktop;

/// <summary>One headless Avalonia session for every UI test, rendering with Skia so fonts and layout are real.</summary>
internal static class Ui
{
    public static readonly BindingErrors Errors = new();

    private static readonly HeadlessUnitTestSession Session = Start();

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs a test on a 1280 × 800 main window whose Recent list lives in its own folder, never the real one.</summary>
    public static Task OnWindow(ThemeVariant theme, string settings, Action<MainWindow> test) =>
        OnWindow(theme, test, () => new MainWindow(new RecentProjects(settings)) { Width = 1280, Height = 800 });

    public static Task OnWindow<T>(ThemeVariant theme, Action<T> test, Func<T> create)
        where T : Window => Session.Dispatch(() =>
    {
        Application.Current!.RequestedThemeVariant = theme;
        Errors.Messages.Clear();
        var window = create();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            test(window);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    /// <summary>The text on screen, after a layout pass: a control shown or added since the last one has no visual children yet.</summary>
    public static List<string?> VisibleText(Visual root)
    {
        Settle();
        return root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToList();
    }

    public static T Find<T>(Visual root, string name)
        where T : Control
    {
        Settle();
        return root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    }

    public static IEnumerable<T> Visible<T>(Visual root)
        where T : Control
    {
        Settle();
        return root.GetVisualDescendants().OfType<T>().Where(control => control.IsEffectivelyVisible);
    }

    public static void Settle() => Dispatcher.UIThread.RunJobs();

    public static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    public static void Click(Window window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Settle();
    }

    public static Bitmap Render(Window window)
    {
        Settle();
        return window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
    }

    public static Color Pixel(Bitmap frame, Point at)
    {
        var bytes = new byte[4];
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(new PixelRect((int)at.X, (int)at.Y, 1, 1), pinned.AddrOfPinnedObject(), 4, 4);
        }
        finally
        {
            pinned.Free();
        }

        return frame.Format == PixelFormats.Rgba8888 ? Color.FromRgb(bytes[0], bytes[1], bytes[2]) : Color.FromRgb(bytes[2], bytes[1], bytes[0]);
    }

    public static Color Token(Window window, string key, ThemeVariant theme) =>
        window.TryFindResource(key, theme, out var brush) && brush is ISolidColorBrush solid
            ? solid.Color
            : throw new KeyNotFoundException($"{key} isn't a brush in {theme}.");

    private static HeadlessUnitTestSession Start()
    {
        // Binding errors are only logged, so the sink is what turns them into a failure.
        Logger.Sink = Errors;
        return HeadlessUnitTestSession.StartNew(typeof(Ui));
    }

    internal sealed class BindingErrors : ILogSink
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == LogArea.Binding;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Log(level, area, source, messageTemplate, []);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
            {
                Messages.Enqueue($"{source}: {messageTemplate} {string.Join(", ", propertyValues)}");
            }
        }
    }
}
