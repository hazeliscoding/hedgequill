using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;
using Hedgequill.Desktop.Spelling;

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
        window.Opened += (_, _) => DispatcherTimer.RunOnce(async () => desktop.Shutdown(await Run(window, editor, markers)), TimeSpan.FromSeconds(1));
        desktop.MainWindow = window;
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<int> Run(Window window, TextEditor editor, FadedMarkers markers)
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

        // M2: the system's spell checker, through COM on Windows and the Objective-C runtime on macOS.
        SpellChecker? speller = null;
        try
        {
            speller = OperatingSystem.IsWindows() ? WindowsSpellChecker.Open() : OperatingSystem.IsMacOS() ? MacSpellChecker.Open() : null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        const string Misspelled = "The keeper saw teh boat, and the lantern stayed lit.";
        Check("the system spell checker opened", speller is not null);
        var flagged = speller?.Check(Misspelled) ?? [];
        Console.WriteLine($"      flagged: {string.Join(", ", flagged.Select(word => $"\"{Misspelled.Substring(word.Start, word.Length)}\" at {word.Start}"))}");
        Check("it flagged the misspelling, and only that", flagged is [{ Start: 15, Length: 3 }]);
        Check("it suggested the word", speller?.Suggest("teh").Contains("the") == true);
        Check("it passed a correct line", speller?.Check("The keeper saw the boat.") is []);

        // M7: HTML on the clipboard beside plain text, as Copy for a beta reader will put it there and Google Docs and Word
        // leave it for a paste. Windows calls it HTML Format, behind a header of offsets; macOS calls it public.html.
        const string Html = "<p><em>Nobody</em> had told Wren.</p>";
        const string Plain = "Nobody had told Wren.";
        var html = DataFormat.CreateBytesPlatformFormat(OperatingSystem.IsWindows() ? "HTML Format" : "public.html");
        try
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, Plain);
            item.Set(html, OperatingSystem.IsWindows() ? ClipboardHtml.Wrap(Html) : System.Text.Encoding.UTF8.GetBytes(Html));
            var transfer = new DataTransfer();
            transfer.Add(item);
            var clipboard = window.Clipboard!;
            await clipboard.SetDataAsync(transfer);
            var formats = await clipboard.GetDataFormatsAsync();
            Console.WriteLine($"      formats: {string.Join(", ", formats)}");
            Check("HTML went on the clipboard beside plain text", formats.Contains(html) && formats.Contains(DataFormat.Text));
            var back = await clipboard.TryGetValueAsync(html);
            Console.WriteLine($"      HTML read back: {(back is null ? "nothing" : ClipboardHtml.Unwrap(back))}");
            Check("the HTML reads back as it was written", back is not null && ClipboardHtml.Unwrap(back) == Html);
            Check("the plain text reads back", await clipboard.TryGetTextAsync() == Plain);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Check("the clipboard took HTML", false);
        }

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
