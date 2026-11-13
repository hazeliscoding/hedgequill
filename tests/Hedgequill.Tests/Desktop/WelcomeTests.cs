using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Tests.Desktop;

public class WelcomeTests
{
    private static readonly BindingErrors Errors = new();

    private static readonly HeadlessUnitTestSession Session = Start();

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    [Fact]
    public Task The_welcome_screen_shows_the_prototypes_copy() => OnWindow(ThemeVariant.Light, window =>
    {
        var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(
            ["Hedgequill", "A quiet place to write.", "New project…", "Open folder…",
             "A project is a folder of Markdown files on your computer. Nothing leaves it unless you copy it out.",
             "Recent", "Nothing here yet.", "Projects you open will be listed here."],
            text);
        Assert.Empty(Errors.Messages);
    });

    [Fact]
    public Task The_binding_error_check_catches_a_broken_binding() => OnWindow(
        ThemeVariant.Light,
        _ => Assert.NotEmpty(Errors.Messages),
        () => new Window { DataContext = new object(), Content = new TextBlock { [!TextBlock.TextProperty] = new ReflectionBinding("Missing") } });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task The_window_paints_its_theme_tokens(string variant)
    {
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        return OnWindow(theme, window =>
        {
            var frame = Render(window);
            Assert.Equal(Token(window, "BgWindow", theme), Pixel(frame, new Point(8, 8)));

            var card = window.GetVisualDescendants().OfType<Border>().Single(b => b.MaxWidth == 440);
            var inside = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height - 6), window)!.Value;
            Assert.Equal(Token(window, "BgPage", theme), Pixel(frame, inside));
        });
    }

    // Avalonia falls back to the system font without a word when an embedded one fails to load.
    [Fact]
    public Task The_title_and_tagline_use_the_bundled_fonts() => OnWindow(ThemeVariant.Light, window =>
    {
        var blocks = window.GetVisualDescendants().OfType<TextBlock>().ToDictionary(t => t.Text ?? "");
        Assert.Equal(("Gloock", FontStyle.Normal), Face(blocks["Hedgequill"]));
        Assert.Equal(("Cardo", FontStyle.Italic), Face(blocks["A quiet place to write."]));
        Assert.Equal(("Cardo", FontStyle.Italic), Face(blocks["Nothing here yet."]));
    });

    [Fact]
    public Task Tab_reaches_New_project_then_Open_folder() => OnWindow(ThemeVariant.Light, window =>
    {
        var order = new List<string?>();
        for (var i = 0; i < 2; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            order.Add(window.FocusManager?.GetFocusedElement() is Control focused ? AutomationProperties.GetName(focused) : null);
        }

        Assert.Equal(["New project…", "Open folder…"], order);
    });

    private static (string Family, FontStyle Style) Face(TextBlock block)
    {
        var glyphs = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight).GlyphTypeface;
        return (glyphs.FamilyName, glyphs.Style);
    }

    private static Color Token(Window window, string key, ThemeVariant theme) =>
        window.TryFindResource(key, theme, out var brush) && brush is ISolidColorBrush solid
            ? solid.Color
            : throw new KeyNotFoundException($"{key} isn't a brush in {theme}.");

    private static HeadlessUnitTestSession Start()
    {
        // Binding errors are only logged, so the sink is what turns them into a failure.
        Logger.Sink = Errors;
        return HeadlessUnitTestSession.StartNew(typeof(WelcomeTests));
    }

    private static Task OnWindow(ThemeVariant theme, Action<Window> test, Func<Window>? create = null) => Session.Dispatch(() =>
    {
        Application.Current!.RequestedThemeVariant = theme;
        Errors.Messages.Clear();
        var window = create?.Invoke() ?? new MainWindow { Width = 1280, Height = 800 };
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

    private static Bitmap Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        return window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
    }

    private static Color Pixel(Bitmap frame, Point at)
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

    private sealed class BindingErrors : ILogSink
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
