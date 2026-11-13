using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Hedgequill.AotProbe;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => AppBuilder.Configure<ProbeApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}

// Opens AvaloniaEdit in a real window, then types, undoes, redoes, selects and styles Markdown markers the way M1 and
// M2 will, printing each check. The exit code is 0 only if every check passed.
public sealed partial class ProbeApp : Application
{
    private const string Sample = """
        The lantern swung once, twice, and went still.

        *Nobody* had told Wren the tower would be this **quiet**.

        * * *

        By morning the snow had covered every track but hers.
        """;

    private const string Typed = " She went down to look.";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var markers = new FadedMarkers();
        var editor = new TextEditor { Document = new TextDocument(Sample), FontSize = 18, WordWrap = true };
        editor.TextArea.TextView.LineTransformers.Add(markers);
        var window = new Window { Title = "Hedgequill NativeAOT probe", Width = 800, Height = 600, Content = editor };
        window.Opened += (_, _) => DispatcherTimer.RunOnce(() => desktop.Shutdown(Run(editor, markers)), TimeSpan.FromSeconds(1));
        desktop.MainWindow = window;
        base.OnFrameworkInitializationCompleted();
    }

    private static int Run(TextEditor editor, FadedMarkers markers)
    {
        var view = editor.TextArea.TextView;
        var failures = 0;
        void Check(string name, bool passed)
        {
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}");
            failures += passed ? 0 : 1;
        }

        Check("the editor laid out and drew its lines", view.VisualLinesValid && view.VisualLines.Count > 0);
        Check("a line transformer styled the Markdown markers", markers.Styled > 0);

        editor.TextArea.Caret.Offset = editor.Document.TextLength;
        editor.TextArea.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = Typed });
        Check("typing at the caret inserts text", editor.Text == Sample + Typed);

        Check("undo removes it", editor.Undo() && editor.Text == Sample);
        Check("redo puts it back", editor.Redo() && editor.Text == Sample + Typed);

        editor.Select(0, "The lantern".Length);
        Check("a selection reads back", editor.SelectedText == "The lantern");

        editor.SelectAll();
        editor.TextArea.Selection.ReplaceSelectionWithText("Fresh page.");
        Check("replacing a selection edits the document", editor.Text == "Fresh page." && editor.Undo() && editor.Text == Sample + Typed);

        Console.WriteLine($"Working set after the checks: {Environment.WorkingSet / 1_048_576} MB");
        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}

// Fades * markers and italicizes what a single pair wraps, as M2's editor will.
internal sealed class FadedMarkers : DocumentColorizingTransformer
{
    private static readonly IBrush Faded = new SolidColorBrush(Color.Parse("#9A918A"));

    public int Styled { get; private set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        for (var start = text.IndexOf('*'); start >= 0; start = text.IndexOf('*', start + 1))
        {
            ChangeLinePart(line.Offset + start, line.Offset + start + 1, element => element.TextRunProperties.SetForegroundBrush(Faded));
            Styled++;
        }

        var open = text.IndexOf('*');
        var close = open >= 0 ? text.IndexOf('*', open + 1) : -1;
        if (close > open + 1 && text[open + 1] != '*')
        {
            ChangeLinePart(line.Offset + open + 1, line.Offset + close, element =>
            {
                var face = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, FontStyle.Italic, face.Weight));
            });
        }
    }
}
