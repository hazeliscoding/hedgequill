using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Hedgequill.Core.Ao3;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>The AO3 preview and Copy chapter HTML, as the prototype draws them.</summary>
public sealed class Ao3PreviewTests : IDisposable
{
    private const string Fog = "*Fog* rolled in over the **harbour**.";
    private const string Rain = "Rain on the [platform](https://example.org/platform).";

    private readonly TempFolder _temp = new();

    public Ao3PreviewTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", Fog);
        _temp.Write("stories/Tides/Chapter 1/Rain.md", Rain);
        _temp.Folder("stories/Tides/Chapter 2");
        _temp.Write("stories/Tides/Notes/Mara.md", "Keeper of the lantern.");
    }

    [Fact]
    public Task The_preview_shows_the_scenes_chapter_as_AO3_will() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        Assert.True(preview.IsVisible);
        var text = VisibleText(preview);
        Assert.Contains("Chapter 1", text);
        Assert.Contains($"Chapter 1 · {Ao3Text.Words(Fog) + Ao3Text.Words(Rain)} words", text);

        var fog = Runs(preview).First(run => run.Text == "Fog");
        Assert.Equal(FontStyle.Italic, fog.FontStyle);
        Assert.Equal(FontWeight.Bold, Runs(preview).First(run => run.Text == "harbour").FontWeight);
        Assert.Contains("link", Runs(preview).First(run => run.Text == "platform").Classes);
        Assert.Single(Visible<Border>(preview), border => border.Classes.Contains("preview-break"));
    });

    [Fact]
    public Task A_quote_is_italic_in_the_preview_and_its_bold_stays_bold()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "> Don't look back, **she** said.");
        return OnWorkspace(1280, (window, workspace, preview) =>
        {
            var quote = Runs(preview).Where(run => run.Text is "Don't look back, " or "she" or " said.").ToList();

            Assert.Equal(3, quote.Count);
            Assert.All(quote, run => Assert.Equal(FontStyle.Italic, run.FontStyle));
            Assert.Equal(FontWeight.Bold, quote[1].FontWeight);
        });
    }

    [Fact]
    public Task A_chapter_copies_for_a_beta_reader_as_rich_text_without_its_comments()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "*Fog* rolled in. <!-- check the tide tables -->");
        return OnWorkspace(1280, (window, workspace, preview) =>
        {
            Run(workspace.CopyForBeta());

            var html = Run(RichClipboard.ReadHtml(window.Clipboard!));
            var text = Run(window.Clipboard!.TryGetTextAsync());
            Assert.Equal(
                "<h1>Chapter 1: Chapter 1</h1>\n<p><em>Fog</em> rolled in.</p>\n<p style=\"text-align:center\">* * *</p>\n<p>Rain on the <a href=\"https://example.org/platform\">platform</a>.</p>",
                html);
            Assert.Equal("Chapter 1: Chapter 1\n\nFog rolled in.\n\n* * *\n\nRain on the platform.", text);
        });
    }

    [Fact]
    public Task The_preview_follows_typing_after_a_pause() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        workspace.Editor.TextArea.Focus();
        workspace.Editor.CaretOffset = 0;
        Settle();
        window.KeyTextInput("Grey ");
        Assert.DoesNotContain(Runs(preview), run => run.Text?.StartsWith("Grey", StringComparison.Ordinal) == true);

        Pump(TimeSpan.FromMilliseconds(500));

        Assert.Contains(Runs(preview), run => run.Text?.StartsWith("Grey", StringComparison.Ordinal) == true);
    });

    [Fact]
    public Task The_preview_starts_hidden_in_a_narrow_window_and_the_View_menu_shows_it() => OnWorkspace(1100, (window, workspace, preview) =>
    {
        Assert.False(preview.IsVisible);
        Assert.False(workspace.PreviewShown);

        workspace.TogglePreview();
        Settle();

        Assert.True(preview.IsVisible);
        Assert.Contains("Chapter 1", VisibleText(preview));
    });

    [Fact]
    public Task Focus_mode_hides_the_preview_and_brings_it_back() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        workspace.ToggleFocus();
        Settle();
        Assert.False(preview.IsVisible);

        workspace.ToggleFocus();
        Settle();
        Assert.True(preview.IsVisible);
    });

    [Fact]
    public Task A_chapter_without_scenes_and_a_page_explain_the_empty_preview() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        workspace.Select(workspace.Rows.Single(row => row.Title == "Chapter 2").Node);
        Assert.Contains("Nothing to preview yet.", VisibleText(preview));

        workspace.Select(workspace.Rows.Single(row => row.Title == "Mara").Node);
        Assert.Contains("Notes aren't part of a chapter.", VisibleText(preview));
    });

    [Fact]
    public Task Copy_chapter_HTML_puts_AO3_HTML_on_the_clipboard_and_offers_it() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        Run(workspace.CopyChapterHtml());

        var html = Ao3Text.ChapterHtml([Fog, Rain]);
        Assert.Equal(html, Run(window.Clipboard!.TryGetTextAsync()));
        Assert.Equal("Chapter 1 copied as HTML", Find<Toast>(window, "Notice").Title);

        workspace.ShowLastHtml();
        var sheet = Find<Ao3Sheet>(window, "Ao3Html");
        Assert.True(sheet.IsVisible);
        Assert.Equal("Chapter 1 copied for AO3", sheet.Title);
        var text = VisibleText(sheet);
        Assert.Contains("Chapter 1", text);
        Assert.Contains($"2 scenes joined with <hr /> · {Ao3Text.Words(Fog) + Ao3Text.Words(Rain)} words", text);
        Assert.Equal(html, Visible<SelectableTextBlock>(sheet).Single().Text);
        Assert.DoesNotContain("1 thing to fix before posting", text);
    });

    [Fact]
    public Task A_chapter_over_AO3s_limit_still_copies_with_a_warning() => OnWorkspace(1280, (window, workspace, preview) =>
    {
        var chapter = workspace.CurrentChapter!;
        chapter.Children[1].Text = string.Join("\n\n", Enumerable.Repeat("The tide came in again, and again, and again.", 12_000));

        Run(workspace.CopyChapterHtml());

        Assert.True(workspace.LastCopy!.Html.Length > ChapterCopy.Limit);
        Assert.Equal("Chapter 1 copied, but it's too long for AO3", Find<Toast>(window, "Notice").Title);
        workspace.ShowLastHtml();
        Assert.Contains("1 thing to fix before posting", VisibleText(Find<Ao3Sheet>(window, "Ao3Html")));
    });

    private static List<Avalonia.Controls.Documents.Run> Runs(PreviewView preview) =>
        Visible<TextBlock>(preview).SelectMany(text => text.Inlines?.OfType<Avalonia.Controls.Documents.Run>() ?? []).ToList();

    private static T Run<T>(Task<T> task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return task.GetAwaiter().GetResult();
    }

    private static void Run(Task task) => Run(task.ContinueWith(_ => true, TaskScheduler.Default));

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, time);
        Dispatcher.UIThread.PushFrame(frame);
        Settle();
    }

    private Task OnWorkspace(double width, Action<MainWindow, WorkspaceView, PreviewView> test) =>
        OnWindow(ThemeVariant.Light, window =>
        {
            Assert.True(window.Open(_temp["stories/Tides"]));
            Settle();
            var workspace = window.Workspace!;
            test(window, workspace, Find<PreviewView>(workspace, "Preview"));
        }, () => new MainWindow(_temp["settings"]) { Width = width, Height = 800 });

    public void Dispose() => _temp.Dispose();
}
