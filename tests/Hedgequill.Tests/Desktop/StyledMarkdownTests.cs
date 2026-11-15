using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class StyledMarkdownTests : IDisposable
{
    private const string Scene = "# The Harbour\n\n*Nobody* told **Wren**.\n\n> A *quiet* line.\n\n---\n\nMorning came.";

    private readonly TempFolder _temp = new();

    public StyledMarkdownTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", Scene);

    [Fact]
    public Task Emphasis_is_italic_strong_is_Cardo_Bold_and_their_markers_fade() => OnEditor(ThemeVariant.Light, (window, editor) =>
    {
        var nobody = Run(editor, "Nobody");
        Assert.Equal(FontStyle.Italic, nobody.Typeface.Style);
        Assert.Equal(Token(window, "TextPrimary", ThemeVariant.Light), Color(nobody));

        var wren = Run(editor, "Wren");
        Assert.Equal(FontWeight.Bold, wren.Typeface.Weight);
        Assert.Equal(("Cardo", FontWeight.Bold, FontSimulations.None), (wren.Typeface.GlyphTypeface.FamilyName, wren.Typeface.GlyphTypeface.Weight, wren.Typeface.GlyphTypeface.FontSimulations));

        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Light), Color(Run(editor, "*Nobody")));
        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Light), Color(Run(editor, "**Wren")));
        Assert.Equal(FontStyle.Normal, Run(editor, "told").Typeface.Style);
    });

    [Fact]
    public Task A_heading_is_in_Gloock_at_1_3_times_the_size_with_its_marker_faded() => OnEditor(ThemeVariant.Light, (window, editor) =>
    {
        var title = Run(editor, "The Harbour");
        Assert.Equal("Gloock", title.Typeface.GlyphTypeface.FamilyName);
        Assert.Equal(19 * 1.3, title.FontRenderingEmSize, 0.01);
        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Light), Color(Run(editor, "# The")));
    });

    [Fact]
    public Task A_quote_is_italic_in_the_secondary_color() => OnEditor(ThemeVariant.Light, (window, editor) =>
    {
        var quote = Run(editor, "A *quiet");
        Assert.Equal(FontStyle.Italic, quote.Typeface.Style);
        Assert.Equal(Token(window, "TextSecondary", ThemeVariant.Light), Color(quote));
        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Light), Color(Run(editor, "> A")));
    });

    [Fact]
    public Task Markers_fade_to_the_dark_token_in_dark() => OnEditor(ThemeVariant.Dark, (window, editor) =>
        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Dark), Color(Run(editor, "*Nobody"))));

    [Fact]
    public Task A_scene_break_is_one_centred_ornament_until_the_caret_is_on_it() => OnEditor(ThemeVariant.Light, (window, editor) =>
    {
        var view = editor.TextArea.TextView;
        var line = BreakLine(editor);
        var ornament = Assert.Single(line.Elements);
        Assert.Equal((1, 3), (ornament.VisualLength, ornament.DocumentLength));
        Assert.Equal(Token(window, "MarkerFaded", ThemeVariant.Light), Color(ornament.TextRunProperties));

        // The run ends where the marks do, just right of the column's middle.
        var end = line.GetVisualPosition(1, VisualYPosition.LineTop).X;
        Assert.InRange(end, view.Bounds.Width / 2, (view.Bounds.Width / 2) + 60);
        Assert.Equal(Scene, editor.Text);

        editor.CaretOffset = Scene.IndexOf("---", StringComparison.Ordinal) + 3;
        Settle();
        Assert.Equal(3, BreakLine(editor).VisualLengthWithEndOfLineMarker - (editor.Options.ShowEndOfLine ? 1 : 0));

        editor.CaretOffset = 0;
        Settle();
        Assert.Single(BreakLine(editor).Elements);
    });

    [Fact]
    public Task With_the_caret_on_it_a_scene_break_edits_as_plain_text() => OnEditor(ThemeVariant.Light, (window, editor) =>
    {
        var end = Scene.IndexOf("---", StringComparison.Ordinal) + 3;
        editor.CaretOffset = end;
        Settle();

        Press(window, PhysicalKey.Backspace);

        Assert.Equal(Scene.Remove(end - 1, 1), editor.Text);
    });

    private static VisualLine BreakLine(TextEditor editor)
    {
        editor.TextArea.TextView.EnsureVisualLines();
        return editor.TextArea.TextView.GetVisualLine(editor.Document.GetLineByOffset(Scene.IndexOf("---", StringComparison.Ordinal)).LineNumber)!;
    }

    // The run properties of the element that starts the first match of the text.
    private static TextRunProperties Run(TextEditor editor, string text)
    {
        var offset = editor.Text.IndexOf(text, StringComparison.Ordinal);
        var view = editor.TextArea.TextView;
        view.EnsureVisualLines();
        var line = view.GetVisualLine(editor.Document.GetLineByOffset(offset).LineNumber)!;
        var relative = offset - line.FirstDocumentLine.Offset;
        return line.Elements.Single(element => element.RelativeTextOffset <= relative && relative < element.RelativeTextOffset + element.DocumentLength).TextRunProperties;
    }

    private static Color Color(TextRunProperties run) => ((ISolidColorBrush)run.ForegroundBrush!).Color;

    private Task OnEditor(ThemeVariant theme, Action<MainWindow, TextEditor> test) => OnWindow(theme, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;
        var editor = Find<TextEditor>(workspace, "Editor");
        editor.TextArea.Focus();
        editor.CaretOffset = 0;
        Settle();
        test(window, editor);
    });

    public void Dispose() => _temp.Dispose();
}
