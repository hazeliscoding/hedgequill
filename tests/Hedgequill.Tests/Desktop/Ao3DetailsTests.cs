using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Hedgequill.Core.Ao3;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>AO3's summary and notes for the work and each chapter, and the mark on a chapter changed since it was copied.</summary>
public sealed class Ao3DetailsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public Ao3DetailsTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "The fog came in.");
        _temp.Write("stories/Tides/Chapter 2/Rain.md", "Rain on the platform.");
        _temp.Write("stories/Tides/hedgequill.json", """{ "format": 2, "details": { "Chapter 1": { "summary": "The *fog* comes in." } } }""");
    }

    [Fact]
    public Task A_chapters_fields_show_its_summary_and_save_what_you_type() => OnWorkspace((window, workspace) =>
    {
        workspace.Select(Chapter(workspace, "Chapter 1"));
        var summary = Field(workspace, "Summary");
        Assert.Equal("The *fog* comes in.", summary.Text);
        Assert.Contains($"{Ao3Text.Html("The *fog* comes in.").Length} of 1,250", VisibleText(workspace));

        Field(workspace, "End notes").Text = "Thanks for reading.";
        Settle();
        workspace.Commit();

        var saved = Project.Open(_temp["stories/Tides"]).Root.Children.Single(node => node.Title == "Chapter 1");
        Assert.Equal(("The *fog* comes in.", "Thanks for reading."), (saved.Summary, saved.EndNotes));
    });

    [Fact]
    public Task A_summary_past_AO3s_limit_says_by_how_much() => OnWorkspace((window, workspace) =>
    {
        workspace.Select(Chapter(workspace, "Chapter 1"));
        Field(workspace, "Summary").Text = new string('a', 1293);
        Settle();

        Assert.Contains("1,300 of 1,250 · 50 over", VisibleText(workspace));
    });

    [Fact]
    public Task Copy_gives_the_field_as_AO3_HTML() => OnWorkspace((window, workspace) =>
    {
        workspace.Select(Chapter(workspace, "Chapter 1"));
        var copy = Visible<Button>(workspace).Single(button => AutomationProperties.GetName(button) == "Copy summary");

        copy.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var text = window.Clipboard!.TryGetTextAsync();
        while (!text.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("<p>The <em>fog</em> comes in.</p>", text.Result);
        Assert.Equal("Summary copied as HTML", Find<Toast>(window, "Notice").Title);
    });

    [Fact]
    public Task Summary_and_notes_in_the_project_menu_opens_the_works_fields() => OnWorkspace((window, workspace) =>
    {
        workspace.ShowWork();
        Assert.Contains("WORK", VisibleText(workspace));
        Assert.Contains("2 chapters · 8 words", VisibleText(workspace));

        Field(workspace, "Summary").Text = "A keeper and a lantern.";
        Settle();
        workspace.Commit();

        Assert.Equal("A keeper and a lantern.", Project.Open(_temp["stories/Tides"]).Root.Summary);
    });

    [Fact]
    public Task A_chapter_edited_since_it_was_copied_gets_a_dot_until_it_is_copied_again() => OnWorkspace((window, workspace) =>
    {
        var row = workspace.Rows.Single(row => row.Title == "Chapter 1");
        Assert.False(row.Changed);

        workspace.Select(Chapter(workspace, "Chapter 1"));
        Run(workspace.CopyChapterHtml());
        Assert.False(row.Changed);

        workspace.Select(row.Node.Children[0]);
        workspace.Editor.TextArea.Focus();
        workspace.Editor.CaretOffset = workspace.Editor.Text.Length;
        Settle();
        window.KeyTextInput(" Then the bell.");
        Pump(TimeSpan.FromMilliseconds(500));

        Assert.True(row.Changed);
        Assert.Equal("Chapter 1, changed since copied for AO3", row.Spoken);
        Assert.Contains(Visible<Avalonia.Controls.Shapes.Ellipse>(workspace), dot => dot.Classes.Contains("changed-dot"));

        Run(workspace.CopyChapterHtml());
        Assert.False(row.Changed);
    });

    private static Node Chapter(WorkspaceView workspace, string title) => workspace.Rows.Single(row => row.Title == title).Node;

    private static TextBox Field(WorkspaceView workspace, string label) =>
        Visible<TextBox>(workspace).Single(box => AutomationProperties.GetName(box) == label);

    private static void Run(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, time);
        Dispatcher.UIThread.PushFrame(frame);
        Settle();
    }

    private Task OnWorkspace(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        test(window, window.Workspace!);
    });

    public void Dispose() => _temp.Dispose();
}
