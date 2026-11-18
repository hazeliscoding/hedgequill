using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>Spell check as the prototype draws it: a wavy underline, and the word's suggestions at the top of the editor's menu.</summary>
public sealed class SpellCheckUiTests : IDisposable
{
    private const string Scene = "The keeper saw teh boat, and the lantern stayed lit.";

    private readonly TempFolder _temp = new();

    public SpellCheckUiTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", Scene);

    private static int Teh => Scene.IndexOf("teh", StringComparison.Ordinal);

    [Fact]
    public Task A_misspelling_gets_a_wavy_underline_in_the_danger_red() => OnSpelling((window, manuscript, editor) =>
    {
        var word = manuscript.Spelling!.At(Teh);
        Assert.NotNull(word);
        Assert.Equal((Teh, 3), (word.StartOffset, word.Length));
        Assert.Null(manuscript.Spelling.At(Scene.IndexOf("keeper", StringComparison.Ordinal)));

        var red = Token(window, "Misspelling", ThemeVariant.Light);
        var (left, baseline, right) = Under(window, editor, Teh, 3);
        using var frame = Render(window);
        var pixels = Enumerable.Range((int)left, (int)(right - left)).SelectMany(x => Enumerable.Range((int)baseline + 1, 6).Select(y => Pixel(frame, new Point(x, y))));
        Assert.Contains(pixels, pixel => pixel.R - pixel.G > 60 && Math.Abs(pixel.R - red.R) < 40);
    });

    [Fact]
    public Task Right_clicking_a_misspelling_puts_its_suggestions_above_the_editing_commands() => OnSpelling((window, manuscript, editor) =>
    {
        var headers = Headers(RightClick(window, editor, Teh + 1));

        var suggestions = headers.TakeWhile(header => header != "Add to project dictionary").ToList();
        Assert.Contains("the", suggestions);
        Assert.InRange(suggestions.Count, 1, 5);
        Assert.Equal(["Add to project dictionary", "-", "Cut", "Copy", "Paste", "-", "Bold", "Italic", "Insert scene break", "-", "Focus mode"], headers.Skip(suggestions.Count));
    });

    [Fact]
    public Task Right_clicking_a_correct_word_offers_editing_only() => OnSpelling((window, manuscript, editor) =>
        Assert.Equal(["Cut", "Copy", "Paste", "-", "Bold", "Italic", "Insert scene break", "-", "Focus mode"], Headers(RightClick(window, editor, 6))));

    [Fact]
    public Task Choosing_a_suggestion_replaces_the_word_as_one_undo() => OnSpelling((window, manuscript, editor) =>
    {
        Choose(RightClick(window, editor, Teh + 1), "the");

        Assert.Equal(Scene.Replace("teh", "the", StringComparison.Ordinal), editor.Text);
        Assert.True(editor.Undo());
        Assert.Equal(Scene, editor.Text);
    });

    [Fact]
    public Task Add_to_project_dictionary_keeps_the_word_in_the_index_and_drops_its_underline() => OnSpelling((window, manuscript, editor) =>
    {
        Choose(RightClick(window, editor, Teh + 1), "Add to project dictionary");

        Assert.Null(manuscript.Spelling!.At(Teh));
        Assert.True(Project.Open(_temp["stories/Tides"]).Knows("teh"));
        Assert.Equal(Scene, editor.Text);
    });

    private static MenuFlyout RightClick(Window window, TextEditor editor, int offset)
    {
        var (left, baseline, _) = Under(window, editor, offset, 1);
        var at = new Point(left + 1, baseline - 4);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        Settle();
        var menu = Assert.IsType<MenuFlyout>(editor.TextArea.ContextFlyout);
        Assert.True(menu.IsOpen);
        return menu;
    }

    // Where a run of text sits in the window: its left and right edges, and its baseline.
    private static (double Left, double Baseline, double Right) Under(Window window, TextEditor editor, int offset, int length)
    {
        var view = editor.TextArea.TextView;
        view.EnsureVisualLines();
        Point At(int at, VisualYPosition y) => view.TranslatePoint(view.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(at)), y) - view.ScrollOffset, window)!.Value;
        var start = At(offset, VisualYPosition.Baseline);
        return (start.X, start.Y, At(offset + length, VisualYPosition.Baseline).X);
    }

    private static List<string?> Headers(MenuFlyout menu) =>
        Shown(menu).Select(item => item is MenuItem named ? named.Header as string : "-").ToList();

    private static void Choose(MenuFlyout menu, string header)
    {
        var item = Shown(menu).OfType<MenuItem>().First(item => Equals(item.Header, header));
        menu.Hide();
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Settle();
    }

    private Task OnSpelling(Action<Window, ManuscriptView, TextEditor> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), "Only Windows and macOS have a system checker Hedgequill uses.");
        Assert.True(window.Open(_temp["stories/Tides"]));
        var manuscript = Find<ManuscriptView>(window.Workspace!, "Manuscript");
        var editor = Find<TextEditor>(manuscript, "Editor");
        Settle();
        Assert.NotNull(manuscript.Spelling);
        manuscript.Spelling.CheckVisible();
        Settle();
        test(window, manuscript, editor);
    });

    public void Dispose() => _temp.Dispose();
}
