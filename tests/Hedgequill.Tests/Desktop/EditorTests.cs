using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class EditorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public EditorTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Chapter 1/Rain.md", "Rain on the invented platform.");
        _temp.Write("stories/Tides/Chapter 2/Ferry.md", "");
        _temp.Write("stories/Tides/Notes/Mara.md", "Keeper of the lantern.");
        _temp.Folder("stories/Tides/Chapter 3");
    }

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task A_scene_opens_in_Cardo_under_its_chapter_and_title() => OnEditor((window, workspace, editor) =>
    {
        Assert.Equal("Invented fog rolled in.", editor.Text);
        Assert.Equal("Cardo", new Typeface(editor.FontFamily).GlyphTypeface.FamilyName);
        Assert.Equal(19, editor.FontSize);
        Assert.Equal(19 * 1.7, editor.TextArea.TextView.DefaultLineHeight, 0.5);
        Assert.Contains("CHAPTER 1 · CHAPTER 1", VisibleText(workspace));
        Assert.Contains("Fog", VisibleText(Find<ManuscriptView>(workspace, "Manuscript")));
        Assert.Equal(["Saved"], VisibleText(Find<ContentControl>(workspace, "SaveSlot")));
        Assert.Empty(Errors.Messages);
    });

    [Fact]
    public Task Typing_saves_to_the_file_and_the_state_goes_from_Saving_to_Saved() => OnEditor((window, workspace, editor) =>
    {
        editor.CaretOffset = editor.Text.Length;
        window.KeyTextInput(" Then the bell.");

        Assert.Equal(SaveState.Saving, window.Saver!.State);
        Assert.Equal("Invented fog rolled in.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));

        window.Saver.Flush();

        Assert.Equal(SaveState.Saved, window.Saver.State);
        Assert.Equal("Invented fog rolled in. Then the bell.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
    });

    [Fact]
    public Task Autosave_writes_within_a_second_of_the_last_keystroke() => OnEditor((window, workspace, editor) =>
    {
        window.KeyTextInput("Quick ");

        // Timers only fire while the dispatcher runs, so run it for a second.
        var second = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => second.Continue = false, TimeSpan.FromSeconds(1));
        Dispatcher.UIThread.PushFrame(second);

        Assert.Equal(SaveState.Saved, window.Saver!.State);
        Assert.Equal("Quick Invented fog rolled in.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
    });

    [Fact]
    public Task Choosing_another_scene_saves_the_one_you_left_at_once() => OnEditor((window, workspace, editor) =>
    {
        window.KeyTextInput("Left behind: ");

        workspace.Select(workspace.Rows.Single(row => row.Title == "Rain").Node);

        Assert.Equal("Left behind: Invented fog rolled in.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.Equal("Rain on the invented platform.", editor.Text);
    });

    [Fact]
    public Task Bold_Italic_and_scene_break_add_plain_markers() => OnEditor((window, workspace, editor) =>
    {
        editor.Select(9, 3);
        Press(window, PhysicalKey.B, Command);
        Assert.Equal("Invented **fog** rolled in.", editor.Text);

        editor.Select(0, 8);
        Press(window, PhysicalKey.I, Command);
        Assert.Equal("*Invented* **fog** rolled in.", editor.Text);

        editor.CaretOffset = editor.Text.Length;
        Press(window, PhysicalKey.Enter, Command);
        Assert.Equal("*Invented* **fog** rolled in.\n\n---\n\n", editor.Text);
        Assert.Equal(editor.Text.Length, editor.CaretOffset);

        editor.Undo();
        Assert.Equal("*Invented* **fog** rolled in.", editor.Text);
    });

    [Fact]
    public Task Find_opens_AvaloniaEdits_search() => OnEditor((window, workspace, editor) =>
    {
        Press(window, PhysicalKey.F, Command);

        Assert.True(editor.GetVisualDescendants().OfType<SearchPanel>().Single().IsOpened);
    });

    [Fact]
    public Task A_change_from_another_app_waits_for_Reload_or_Keep_mine() => OnEditor((window, workspace, editor) =>
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Fog, as another editor wrote it.");
        window.KeyTextInput("Mine: ");
        window.Saver!.Flush();

        Assert.Equal(SaveState.ChangedOnDisk, window.Saver.State);
        Assert.Equal(["Changed on disk"], VisibleText(Find<ContentControl>(workspace, "SaveSlot")));
        Assert.Equal("Fog, as another editor wrote it.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));

        window.Saver.ReloadFile();

        Assert.Equal(SaveState.Saved, window.Saver.State);
        Assert.Equal("Fog, as another editor wrote it.", editor.Text);

        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Second outside change.");
        editor.CaretOffset = 0;
        window.KeyTextInput("Keep this. ");
        window.Saver.Flush();
        window.Saver.KeepMine();

        Assert.Equal(SaveState.Saved, window.Saver.State);
        Assert.Equal("Keep this. Fog, as another editor wrote it.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
    });

    [Fact]
    public Task A_failed_save_keeps_the_text_and_closing_asks_first() => OnEditor((window, workspace, editor) =>
    {
        Directory.Delete(_temp["stories/Tides/Chapter 1"], recursive: true);
        _temp.Write("stories/Tides/Chapter 1", "A file where the chapter's folder was.");
        window.KeyTextInput("Words that must survive. ");
        window.Saver!.Flush();

        Assert.Equal(SaveState.Failed, window.Saver.State);
        Assert.Equal(["Couldn't save"], VisibleText(Find<ContentControl>(workspace, "SaveSlot")));
        Assert.Equal("Words that must survive. Invented fog rolled in.", editor.Text);

        window.Close();

        Assert.True(window.IsVisible);
        Assert.Contains("Close without saving?", VisibleText(Find<ConfirmSheet>(window, "Confirmation")));
        Find<ConfirmSheet>(window, "Confirmation").Hide();
    });

    [Fact]
    public Task A_read_only_file_keeps_the_edits_in_memory()
    {
        var file = _temp["stories/Tides/Chapter 1/Fog.md"];
        var folder = _temp["stories/Tides/Chapter 1"];
        return OnEditor((window, workspace, editor) =>
        {
            // Windows refuses to replace a read-only file; macOS refuses to write in a folder without write permission.
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(file, FileAttributes.ReadOnly);
            }
            else
            {
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            try
            {
                window.KeyTextInput("Kept in memory. ");
                window.Saver!.Flush();

                Assert.Equal(SaveState.ReadOnly, window.Saver.State);
                Assert.Equal(["Read-only"], VisibleText(Find<ContentControl>(workspace, "SaveSlot")));
                Assert.Equal("Kept in memory. Invented fog rolled in.", editor.Text);
                Assert.Equal("Invented fog rolled in.", File.ReadAllText(file));
            }
            finally
            {
                if (OperatingSystem.IsWindows())
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                else
                {
                    File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }

                window.Saver!.Flush();
            }
        });
    }

    [Fact]
    public Task A_chapter_shows_its_scenes_and_an_empty_one_offers_to_add_one() => OnEditor((window, workspace, editor) =>
    {
        workspace.Select(workspace.Rows.Single(row => row.Title == "Chapter 1").Node);

        var manuscript = Find<ManuscriptView>(workspace, "Manuscript");
        Assert.Equal(["CHAPTER 1", "Chapter 1", "2 scenes · 9 words", "1", "Fog", "4 words", "2", "Rain", "5 words"], VisibleText(manuscript));

        Click(window, Visible<Button>(manuscript).Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Rain"));
        Assert.Equal("Rain", workspace.Selected?.Title);

        workspace.Select(workspace.Rows.Single(row => row.Title == "Chapter 3").Node);
        Assert.Contains("This chapter has no scenes yet.", VisibleText(manuscript));
        Click(window, Find<Button>(manuscript, "AddSceneButton"));
        Assert.True(File.Exists(_temp["stories/Tides/Chapter 3/Untitled scene.md"]));
    });

    [Fact]
    public Task Focus_mode_hides_the_navigator_and_Escape_brings_it_back() => OnEditor((window, workspace, editor) =>
    {
        Press(window, PhysicalKey.F, Command | RawInputModifiers.Shift);

        Assert.True(workspace.FocusMode);
        Assert.False(Find<DockPanel>(workspace, "NavigatorPane").IsVisible);
        Assert.Equal(["4 words", "Saved", "Esc to leave focus"], VisibleText(Find<StackPanel>(workspace, "FocusFooter")));

        Press(window, PhysicalKey.Escape);

        Assert.False(workspace.FocusMode);
        Assert.True(Find<DockPanel>(workspace, "NavigatorPane").IsVisible);
    });

    [Fact]
    public Task An_empty_scene_says_Begin_here() => OnEditor((window, workspace, editor) =>
    {
        workspace.Select(workspace.Rows.Single(row => row.Title == "Ferry").Node);

        Assert.Equal("", editor.Text);
        Assert.Contains("Begin here.", VisibleText(Find<ManuscriptView>(workspace, "Manuscript")));
    });

    public void Dispose() => _temp.Dispose();

    private Task OnEditor(Action<MainWindow, WorkspaceView, TextEditor> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;
        var editor = Find<TextEditor>(workspace, "Editor");
        editor.TextArea.Focus();
        Settle();
        test(window, workspace, editor);
    });
}
