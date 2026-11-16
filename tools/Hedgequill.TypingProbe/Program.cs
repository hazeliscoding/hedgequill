using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hedgequill.Core.Text;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Themes;
using Hedgequill.Desktop.Views;

namespace Hedgequill.TypingProbe;

internal static class Program
{
    private const double Frame = 1000.0 / 60;
    private const int Keys = 400;
    private const int WarmUp = 20;
    private const string Typed = "She lit the lamp anyway, and waited for the boats to find the harbour. ";

    [STAThread]
    public static int Main(string[] args)
    {
        // An invented chapter and an empty Recent list, never the writer's.
        var scratch = Directory.CreateTempSubdirectory("hedgequill-typing-");
        Environment.SetEnvironmentVariable("HEDGEQUILL_SETTINGS", Path.Combine(scratch.FullName, "settings"));
        var exit = 1;
        try
        {
            var project = Chapter.Write(Path.Combine(scratch.FullName, "stories"));
            var slide = args.Contains("--slide");
            Motion.On = slide;
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .AfterSetup(_ => DispatcherTimer.RunOnce(() => Run(project, args.Contains("--bare"), slide, code => exit = code), TimeSpan.FromSeconds(1)))
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }

        return exit;
    }

    private static void Run(string project, bool bare, bool slide, Action<int> done)
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        try
        {
            if (slide)
            {
                Slide((MainWindow)desktop.MainWindow!, project, done);
            }
            else
            {
                Measure((MainWindow)desktop.MainWindow!, project, bare, done);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine(error);
            desktop.Shutdown(1);
        }
    }

    private static void Measure(MainWindow window, string project, bool bare, Action<int> done)
    {
        window.Open(project);
        window.UpdateLayout();
        var manuscript = window.GetVisualDescendants().OfType<ManuscriptView>().Single();
        Console.WriteLine("Opened the chapter; typing.");
        var editor = manuscript.TextEditor;
        // --bare types into a plain AvaloniaEdit editor holding the same chapter, without Hedgequill's styling or spell check.
        if (bare)
        {
            editor = new AvaloniaEdit.TextEditor { Document = new AvaloniaEdit.Document.TextDocument(editor.Text), FontSize = 19, WordWrap = true };
            window.Content = editor;
            window.UpdateLayout();
        }

        var words = Words.Count(editor.Text);

        // Type at the end of a paragraph in the middle of the chapter, with a misspelling on screen.
        var middle = editor.Document.GetLineByNumber(editor.Document.LineCount / 2);
        while (middle.Length == 0)
        {
            middle = middle.NextLine!;
        }

        editor.TextArea.Focus();
        editor.CaretOffset = middle.EndOffset;
        editor.ScrollToLine(middle.LineNumber);

        var keystrokes = new List<double>();
        var checks = new List<double>();
        var clock = Stopwatch.StartNew();
        DispatcherTimer.RunOnce(() => Next(0), TimeSpan.FromMilliseconds(500));

        void Next(int key)
        {
            if (key == Keys)
            {
                Report(words, keystrokes.Skip(WarmUp).ToList(), checks, clock.Elapsed);
                done(keystrokes.Skip(WarmUp).All(ms => ms <= Frame) && checks.All(ms => ms <= Frame) ? 0 : 1);
                window.Close();
                return;
            }

            var start = Stopwatch.GetTimestamp();
            var paused = GC.GetTotalPauseDuration();
            editor.TextArea.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = Typed[key % Typed.Length].ToString() });
            var input = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            // The frame that shows the keystroke: from its start to the end of its layout and rendering.
            TopLevel.GetTopLevel(editor)!.RequestAnimationFrame(_ =>
            {
                var frame = Stopwatch.GetTimestamp();
                Dispatcher.UIThread.Post(() =>
                {
                    var drawn = Stopwatch.GetElapsedTime(frame).TotalMilliseconds;
                    keystrokes.Add(input + drawn);
                    if (input + drawn > Frame && key >= WarmUp)
                    {
                        Console.WriteLine($"  key {key}: input {input:F1} ms, frame {drawn:F1} ms, GC pauses {(GC.GetTotalPauseDuration() - paused).TotalMilliseconds:F1} ms");
                    }

                    // Every 40 keys the writer pauses, and spell check looks at the line again.
                    if (key % 40 == 39 && !bare && manuscript.Spelling is { } spelling)
                    {
                        var check = Stopwatch.GetTimestamp();
                        spelling.CheckVisible();
                        checks.Add(Stopwatch.GetElapsedTime(check).TotalMilliseconds);
                        DispatcherTimer.RunOnce(() => Next(key + 1), TimeSpan.FromMilliseconds(600));
                    }
                    else
                    {
                        DispatcherTimer.RunOnce(() => Next(key + 1), TimeSpan.FromMilliseconds(30));
                    }
                }, DispatcherPriority.Background);
            });
        }
    }

    // --slide slides the navigator and the preview shut and open 20 times, in a window narrow enough that the chapter rewraps
    // as they move, and times each frame of the slides from its start to the end of its layout and rendering.
    private static void Slide(MainWindow window, string project, Action<int> done)
    {
        window.Width = 1100;
        window.Open(project);
        window.UpdateLayout();
        var workspace = window.Workspace!;
        var top = TopLevel.GetTopLevel(window)!;
        var frames = new List<double>();
        Console.WriteLine($"Opened the chapter, {Words.Count(workspace.Editor.Text):N0} words; sliding.");
        DispatcherTimer.RunOnce(() => Next(0), TimeSpan.FromMilliseconds(500));

        void Next(int slide)
        {
            if (slide == 20)
            {
                double Percentile(double p) => frames.Order().ElementAt((int)Math.Min(frames.Count - 1, Math.Ceiling(p * frames.Count) - 1));

                Console.WriteLine($"{RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} logical processors");
                Console.WriteLine($"Slide frames, ms: {frames.Count} frames, median {Percentile(0.5):F2}, 95th {Percentile(0.95):F2}, max {frames.Max():F2}; {frames.Count(ms => ms > Frame)} over {Frame:F1}");
                done(frames.All(ms => ms <= Frame) ? 0 : 1);
                window.Close();
                return;
            }

            // Navigator shut, preview shut, navigator open, preview open, and again.
            var pane = slide % 2 == 0 ? "navigator" : "preview";
            var opening = slide % 4 >= 2;
            if (pane == "navigator")
            {
                workspace.ToggleNavigator();
            }
            else
            {
                workspace.TogglePreview();
            }

            var until = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * 3 / 10);
            top.RequestAnimationFrame(Time);

            // Asked for inside a frame, the next callback waits for the next frame; asked for outside one, Avalonia renders at once.
            void Time(TimeSpan _)
            {
                var start = Stopwatch.GetTimestamp();
                Dispatcher.UIThread.Post(() =>
                {
                    var took = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    frames.Add(took);
                    if (took > Frame)
                    {
                        Console.WriteLine($"  {(opening ? "opening" : "shutting")} the {pane}: a frame took {took:F1} ms");
                    }
                }, DispatcherPriority.Background);
                if (Stopwatch.GetTimestamp() < until)
                {
                    top.RequestAnimationFrame(Time);
                }
                else
                {
                    DispatcherTimer.RunOnce(() => Next(slide + 1), TimeSpan.FromMilliseconds(300));
                }
            }
        }
    }

    private static void Report(int words, List<double> keystrokes, List<double> checks, TimeSpan took)
    {
        double Percentile(List<double> values, double p) => values.Order().ElementAt((int)Math.Min(values.Count - 1, Math.Ceiling(p * values.Count) - 1));

        Console.WriteLine($"{RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} logical processors");
        Console.WriteLine($"Chapter: {words:N0} words, typed {keystrokes.Count + WarmUp} keys in {took.TotalSeconds:F0} s; the first {WarmUp} are left out as warm-up.");
        Console.WriteLine($"Keystroke to frame, ms: median {Percentile(keystrokes, 0.5):F2}, 95th {Percentile(keystrokes, 0.95):F2}, max {keystrokes.Max():F2}; {keystrokes.Count(ms => ms > Frame)} over {Frame:F1}");
        Console.WriteLine(checks.Count > 0
            ? $"Spell check after a pause, ms: median {Percentile(checks, 0.5):F2}, max {checks.Max():F2}"
            : "Spell check: no system checker.");
        Console.WriteLine(keystrokes.All(ms => ms <= Frame) && checks.All(ms => ms <= Frame) ? "Every keystroke fit in one 60 Hz frame." : "Some keystrokes missed a 60 Hz frame.");
    }
}
