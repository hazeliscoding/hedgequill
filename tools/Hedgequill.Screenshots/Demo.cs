using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Screenshots;

/// <summary>
/// The README's demo: typing in an invented scene, Italic, Copy HTML, focus mode and the shortcut sheet, with each
/// shortcut's keys on screen. The app's motion runs on real time, so frames are taken as it plays and ffmpeg makes the GIF.
/// </summary>
/// <remarks>
/// Headless input renders until the window is still, which never happens while something moves, so the demo types
/// through the editor and runs each shortcut's menu command, showing the keys that menu item lists.
/// </remarks>
internal static class Demo
{
    public const int Width = 1200;
    public const int Height = 750;

    /// <summary>Plays the demo in an open project, and writes it to <paramref name="gif"/> unless that's null.</summary>
    public static void Play(MainWindow window, string? gif)
    {
        var workspace = window.Workspace!;
        workspace.Select(workspace.Rows.First(row => row.Node.Title == "The causeway").Node);
        Dispatcher.UIThread.RunJobs();
        var editor = workspace.GetVisualDescendants().OfType<TextEditor>().First(editor => editor.Name == "Editor");
        var keys = new Keys(window);
        var random = new Random(7);
        using var film = new Film(window, gif);

        void Type(string text)
        {
            foreach (var letter in text)
            {
                editor.TextArea.PerformTextInput(letter.ToString());
                film.Hold(0.045 + (random.NextDouble() * 0.05) + (letter == ' ' ? 0.03 : 0));
            }
        }

        void Shortcut(string header, double after)
        {
            var item = NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>()
                .SelectMany(menu => menu.Menu?.Items.OfType<NativeMenuItem>() ?? [])
                .First(item => item.Header == header);
            Press(item.Gesture!, header.TrimEnd('…'), () => item.Command!.Execute(item.CommandParameter), after);
        }

        void Press(KeyGesture gesture, string label, Action action, double after)
        {
            keys.Show(gesture, label);
            film.Hold(0.6);
            action();
            film.Hold(after);
            keys.Hide();
        }

        editor.CaretOffset = editor.Text.Length;
        editor.TextArea.Focus();
        film.Hold(1.2);
        Type("\n\nMara crossed before the light went. ");
        Shortcut("Italic", after: 0.25);
        Type("Don't look back");
        // Out past the closing marker, as a writer would with the arrow key.
        editor.CaretOffset++;
        Type(", her mother used to say.");
        film.Hold(1.6);

        Shortcut("Copy chapter HTML", after: 1.8);
        window.FindControl<Toast>("Notice")!.Hide();
        film.Hold(0.5);

        Shortcut("Focus mode", after: 0.7);
        Type(" So she didn't.");
        film.Hold(0.9);
        // Escape in the editor leaves focus mode; it isn't a menu item's shortcut.
        Press(new KeyGesture(Key.Escape), "Leave focus mode", workspace.ToggleFocus, after: 1.0);

        Shortcut("Keyboard shortcuts", after: 2.4);
        window.FindControl<HelpSheet>("Help")!.Hide();
        film.Hold(1.0);
    }

    /// <summary>Frames at a steady rate, piped to ffmpeg; a frame that took long to render fills the time it took.</summary>
    private sealed class Film(Window window, string? gif) : IDisposable
    {
        private const int Rate = 25;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private Process? _ffmpeg;
        private byte[] _pixels = [];
        private int _frames;

        public void Hold(double seconds)
        {
            var until = _clock.Elapsed.TotalSeconds + seconds;
            while (_clock.Elapsed.TotalSeconds < until)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var owed = (int)(_clock.Elapsed.TotalSeconds * Rate) + 1 - _frames;
                if (owed > 0)
                {
                    Capture(owed);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void Dispose()
        {
            if (_ffmpeg is null)
            {
                return;
            }

            _ffmpeg.StandardInput.Close();
            _ffmpeg.WaitForExit();
            if (_ffmpeg.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg stopped with status {_ffmpeg.ExitCode} making {gif}.");
            }
        }

        private void Capture(int copies)
        {
            using var frame = window.GetLastRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
            using var buffer = frame.Lock();
            var row = buffer.Size.Width * 4;
            if (_pixels.Length == 0)
            {
                _pixels = new byte[row * buffer.Size.Height];
                _ffmpeg = gif is null ? null : Start(buffer.Size, buffer.Format == PixelFormat.Rgba8888 ? "rgba" : "bgra");
            }

            for (var y = 0; y < buffer.Size.Height; y++)
            {
                Marshal.Copy(buffer.Address + (y * buffer.RowBytes), _pixels, y * row, row);
            }

            for (var copy = 0; copy < copies; copy++)
            {
                _ffmpeg?.StandardInput.BaseStream.Write(_pixels);
            }

            _frames += copies;
        }

        private Process Start(PixelSize size, string format)
        {
            // One palette for the whole film, and no dithering, which would speckle the text.
            var start = new ProcessStartInfo("ffmpeg")
            {
                ArgumentList =
                {
                    "-loglevel", "error", "-y",
                    "-f", "rawvideo", "-pix_fmt", format, "-s", $"{size.Width}x{size.Height}", "-r", $"{Rate}", "-i", "-",
                    "-vf", "split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none:diff_mode=rectangle",
                    "-loop", "0", gif!,
                },
                RedirectStandardInput = true,
                UseShellExecute = false,
            };
            try
            {
                return Process.Start(start)!;
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException("The demo needs ffmpeg on PATH to make the GIF.");
            }
        }
    }

    /// <summary>The keys of the shortcut being pressed, at the bottom of the window, in ink on light and paper on dark.</summary>
    private sealed class Keys
    {
        private readonly Border _badge;
        private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        private readonly bool _dark = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;

        public Keys(Window window)
        {
            _badge = new Border
            {
                Child = _row,
                Background = Brush(_dark ? "#ece6da" : "#231f1c"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                BoxShadow = BoxShadows.Parse("0 8 24 0 #40000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 24),
                Opacity = 0,
                Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) }],
            };
            var size = window.ClientSize;
            OverlayLayer.GetOverlayLayer(window)!.Children.Add(new Grid { Width = size.Width, Height = size.Height, IsHitTestVisible = false, Children = { _badge } });
        }

        public void Show(KeyGesture gesture, string label)
        {
            _row.Children.Clear();
            foreach (var key in gesture.ToString("p", null).Split('+'))
            {
                _row.Children.Add(new Border
                {
                    Background = Brush(_dark ? "#201f1d" : "#fbf8f1"),
                    BorderBrush = Brush(_dark ? "#847c73" : "#9a918a"),
                    BorderThickness = new Thickness(1, 1, 1, 3),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(9, 3),
                    MinWidth = 32,
                    Child = new TextBlock { Text = key, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Brush(_dark ? "#ece6da" : "#231f1c"), HorizontalAlignment = HorizontalAlignment.Center },
                });
            }

            _row.Children.Add(new TextBlock { Text = label, FontSize = 15, Margin = new Thickness(6, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(_dark ? "#201f1d" : "#fbf8f1") });
            _badge.Opacity = 1;
        }

        public void Hide() => _badge.Opacity = 0;

        private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
    }
}
