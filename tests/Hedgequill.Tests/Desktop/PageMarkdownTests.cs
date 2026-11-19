using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>M6 in the editor: lists, checklists, code and tables on a Notes page, styled in place.</summary>
public sealed class PageMarkdownTests : IDisposable
{
    private readonly TempFolder _temp = new();

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task Enter_and_Tab_work_a_list() => OnPage("- Tides", (window, editor) =>
    {
        editor.CaretOffset = editor.Text.Length;
        Press(window, PhysicalKey.Enter);
        window.KeyTextInput("Ferry");
        Press(window, PhysicalKey.Tab);

        Assert.Equal("- Tides\n  - Ferry", editor.Text);

        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        Press(window, PhysicalKey.Enter);
        Press(window, PhysicalKey.Enter);
        window.KeyTextInput("Lamp");

        Assert.Equal("- Tides\n- Ferry\n\nLamp", editor.Text);
    });

    [Fact]
    public Task Clicking_a_box_ticks_it_in_the_file_and_Undo_clears_it() => OnPage("To do\n\n- [ ] Check the tides", (window, editor) =>
    {
        var view = editor.TextArea.TextView;
        view.EnsureVisualLines();
        var box = view.GetVisualPosition(new TextViewPosition(3, 3), VisualYPosition.TextMiddle) - view.ScrollOffset;
        var at = view.TranslatePoint(box + new Vector(4, 0), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Settle();

        Assert.Equal("To do\n\n- [x] Check the tides", editor.Text);

        Press(window, PhysicalKey.Z, Command);

        Assert.Equal("To do\n\n- [ ] Check the tides", editor.Text);
    });

    [Fact]
    public Task A_fenced_block_is_in_the_code_face_from_its_first_line_to_its_last() => OnPage("Before\n\n```\nlow tide\n---\n```\n\nAfter", (window, editor) =>
    {
        var view = editor.TextArea.TextView;
        var mono = (FontFamily)Avalonia.Application.Current!.FindResource("FontMono")!;
        VisualLine Line(int number) => view.GetOrConstructVisualLine(editor.Document.GetLineByNumber(number));
        FontFamily Face(int number) => Line(number).Elements.First(element => element.DocumentLength > 0).TextRunProperties.Typeface.FontFamily;

        Assert.All([3, 4, 5, 6], number => Assert.Equal(mono, Face(number)));
        Assert.NotEqual(mono, Face(1));
        Assert.NotEqual(mono, Face(8));
        // Inside the block, three dashes are code, not a scene break's ornament.
        Assert.Contains(Line(5).Elements, element => element is VisualLineText);
    });

    [Fact]
    public Task A_tables_columns_line_up_on_screen_while_the_file_keeps_them_as_typed() => OnPage("| Name | Lives |\n|---|---|\n| Mrs. Okafor | downstairs |", (window, editor) =>
    {
        var view = editor.TextArea.TextView;
        view.EnsureVisualLines();
        // Where each row's pipe ends, by its number in the row.
        double After(int line, int pipe) =>
            view.GetVisualPosition(new TextViewPosition(line, Markdown.Pipes(editor.Document.GetText(editor.Document.GetLineByNumber(line)))[pipe] + 2), VisualYPosition.Baseline).X;

        foreach (var pipe in new[] { 1, 2 })
        {
            Assert.All([1, 2], line => Assert.Equal(After(3, pipe), After(line, pipe), 0.5));
        }

        Assert.Equal("| Name | Lives |\n|---|---|\n| Mrs. Okafor | downstairs |", editor.Text);
    });

    [Fact]
    public Task A_page_switches_to_reading_view_and_back_with_a_box_ticked_there() => OnPage("# Checks\n\n- [ ] Check the *tides*\n- Lamp", (window, editor) =>
    {
        var manuscript = Find<ManuscriptView>(window, "Manuscript");
        Press(window, PhysicalKey.E, Command);

        Assert.True(manuscript.IsReading);
        Assert.False(editor.IsEffectivelyVisible);
        var reading = Find<ReadingView>(window, "Reading");
        Assert.Equal(["Checks", "Check the tides", "Lamp"], Words(reading));
        Assert.Contains("•", VisibleText(reading));

        Click(window, reading.GetVisualDescendants().OfType<CheckBox>().Single());

        Assert.Equal("# Checks\n\n- [x] Check the *tides*\n- Lamp", editor.Text);
        Assert.True(reading.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked);

        Press(window, PhysicalKey.E, Command);

        Assert.False(manuscript.IsReading);
        Assert.True(editor.IsEffectivelyVisible);
        Press(window, PhysicalKey.Z, Command);
        Assert.Equal("# Checks\n\n- [ ] Check the *tides*\n- Lamp", editor.Text);
    });

    [Fact]
    public async Task A_page_opens_in_reading_view_again_after_a_restart()
    {
        await OnPage("Keeper of the lantern.", (window, _) => Press(window, PhysicalKey.E, Command));

        await OnPage("Keeper of the lantern.", (window, editor) =>
        {
            Assert.True(Find<ManuscriptView>(window, "Manuscript").IsReading);
            Assert.False(editor.IsEffectivelyVisible);
        });
    }

    [Fact]
    public Task A_scene_has_the_AO3_preview_instead_of_a_reading_view() => OnPage("Keeper of the lantern.", (window, editor) =>
    {
        var workspace = window.Workspace!;
        workspace.Select(workspace.Rows.Single(row => row.Node.Kind == NodeKind.Scene).Node);
        Settle();

        Press(window, PhysicalKey.E, Command);

        Assert.False(workspace.Reading);
        Assert.True(editor.IsEffectivelyVisible);
        Assert.False(Find<Button>(window, "ReadButton").IsVisible);
    });

    [Fact]
    public Task Paste_keeps_italics_and_bold_from_HTML_and_Shift_pastes_plain_text() => OnPage("", (window, editor) =>
    {
        const string Docs = "<b style=\"font-weight:normal\" id=\"docs-internal-guid-1\"><p><span>The fog </span><span style=\"font-style:italic\">never</span><span style=\"font-weight:700\"> lifted</span>.</p></b>";
        Wait(RichClipboard.Write(window.Clipboard!, "The fog never lifted.", Docs));

        Press(window, PhysicalKey.V, Command);
        Wait(Task.Delay(50));

        Assert.Equal("The fog *never* **lifted**.", editor.Text);

        editor.SelectAll();
        Press(window, PhysicalKey.V, Command | RawInputModifiers.Shift);
        Wait(Task.Delay(50));

        Assert.Equal("The fog never lifted.", editor.Text);
        Press(window, PhysicalKey.Z, Command);
        Assert.Equal("The fog *never* **lifted**.", editor.Text);
    });

    public void Dispose() => _temp.Dispose();

    // Runs the UI thread's jobs until an asynchronous step, such as a clipboard read, is done.
    private static void Wait(Task task)
    {
        while (!task.IsCompleted)
        {
            Settle();
        }

        task.GetAwaiter().GetResult();
        Settle();
    }

    // The reading view's text, a block or list item at a time.
    private static List<string> Words(ReadingView reading) =>
        reading.GetVisualDescendants().OfType<SelectableTextBlock>().Select(text => text.Inlines!.Text ?? "").Where(text => text.Length > 0).ToList();

    private Task OnPage(string text, Action<MainWindow, TextEditor> test)
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Notes/Mara.md", text);
        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            Assert.True(window.Open(_temp["stories/Tides"]));
            var workspace = window.Workspace!;
            workspace.Select(workspace.Rows.Single(row => row.Node.Kind == NodeKind.Page).Node);
            var editor = Find<TextEditor>(workspace, "Editor");
            editor.TextArea.Focus();
            Settle();
            test(window, editor);
        });
    }
}
