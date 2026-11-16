using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using Hedgequill.Desktop;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>A project in OneDrive, iCloud Drive or Dropbox: a sync client holds files while it uploads and changes them after a phone does.</summary>
public sealed class SyncedFolderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public SyncedFolderTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");

    private string Fog => _temp["stories/Tides/Chapter 1/Fog.md"];

    [Fact]
    public Task Typing_while_another_app_holds_the_file_keeps_saving_until_it_lets_go() => OnEditor((window, editor) =>
    {
        window.KeyTextInput("Grey ");
        using (new FileStream(Fog, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            window.Saver!.Flush();
            Assert.True(window.Saver.State is SaveState.Saving or SaveState.Saved, window.Saver.State.ToString());
        }

        Pump(TimeSpan.FromSeconds(1.5));

        Assert.Equal(SaveState.Saved, window.Saver.State);
        Assert.Equal("Grey Invented fog rolled in.", File.ReadAllText(Fog));
    });

    [Fact]
    public Task A_file_changed_in_another_app_reloads_when_nothing_here_is_unsaved() => OnEditor((window, editor) =>
    {
        File.WriteAllText(Fog, "Fog, edited on a phone.");

        Pump(TimeSpan.FromSeconds(1.5));

        Assert.Equal("Fog, edited on a phone.", editor.Text);
        Assert.Equal(SaveState.Saved, window.Saver!.State);
    });

    [Fact]
    public Task A_file_changed_in_another_app_with_unsaved_edits_here_asks_and_keeps_both() => OnEditor((window, editor) =>
    {
        window.KeyTextInput("Grey ");
        File.WriteAllText(Fog, "Fog, edited on a phone.");

        Pump(TimeSpan.FromSeconds(1.5));

        Assert.Equal(SaveState.ChangedOnDisk, window.Saver!.State);
        Assert.Equal("Grey Invented fog rolled in.", editor.Text);
        Assert.Equal("Fog, edited on a phone.", File.ReadAllText(Fog));
    });

    [Fact]
    public Task Hedgequills_own_saves_dont_reload_the_scene() => OnEditor((window, editor) =>
    {
        window.KeyTextInput("Grey ");
        window.Saver!.Flush();

        Pump(TimeSpan.FromSeconds(1.5));

        Assert.True(editor.CanUndo);
        Assert.Equal("Grey Invented fog rolled in.", editor.Text);
    });

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, time);
        Dispatcher.UIThread.PushFrame(frame);
        Settle();
    }

    private Task OnEditor(Action<MainWindow, TextEditor> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var editor = Find<TextEditor>(window.Workspace!, "Editor");
        editor.TextArea.Focus();
        editor.CaretOffset = 0;
        Settle();
        test(window, editor);
    });

    public void Dispose() => _temp.Dispose();
}
