using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>Edit › Find in project…: every scene and page searched, each match under its scene, and Replace all with Undo.</summary>
public sealed class FindInProjectTests : IDisposable
{
    private const string Fog = "Theo waited in the fog.\n\nThe fog waited too.";
    private const string Harbour = "Mira found Theo's coat at the harbour.";
    private const string Cast = "Theo: a ferryman, invented.";

    private readonly TempFolder _temp = new();

    public FindInProjectTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", Fog);
        _temp.Write("stories/Tides/Chapter 2/Harbour.md", Harbour);
        _temp.Write("stories/Tides/Notes/Cast.md", Cast);
    }

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task The_shortcut_opens_the_panel_in_the_navigators_place_and_a_match_opens_its_scene() => OnProject((window, workspace) =>
    {
        workspace.CollapseAll();
        Press(window, PhysicalKey.H, Command | RawInputModifiers.Shift);
        var find = Find<FindPanel>(workspace, "FindPane");
        Assert.True(find.IsVisible);
        Assert.False(Find<DockPanel>(workspace, "Tree").IsVisible);

        window.KeyTextInput("theo");
        Pump(TimeSpan.FromMilliseconds(300));

        Assert.Equal("3 matches in 3 scenes", Find<TextBlock>(find, "Summary").Text);
        Assert.Equal(["Fog", "Chapter 1", "Harbour", "Chapter 2", "Cast", "Notes"], Visible<TextBlock>(find).Where(text => text.Classes.Contains("group-title") || (text.Classes.Contains("caption") && text.Name is null)).Select(text => text.Text));
        var matches = Visible<Button>(find).Where(button => button.Classes.Contains("match")).ToList();
        Assert.Equal(3, matches.Count);
        Assert.Equal("Fog: Theo waited in the fog. The fog waited too.", Avalonia.Automation.AutomationProperties.GetName(matches[0]));

        // Harbour's chapter is collapsed; the match opens it.
        Click(window, matches[1]);
        Assert.Equal("Harbour", workspace.OpenDocument?.Title);
        Assert.Equal("Theo", workspace.Editor.SelectedText);
        Assert.Equal(11, workspace.Editor.SelectionStart);

        Click(window, Find<Button>(find, "Close"));
        Assert.False(find.IsVisible);
        Assert.True(Find<DockPanel>(workspace, "Tree").IsVisible);
    });

    [Fact]
    public Task Nothing_to_find_says_so() => OnProject((window, workspace) =>
    {
        workspace.OpenFindInProject();
        var find = Find<FindPanel>(workspace, "FindPane");
        Assert.Equal("Type to search every scene and page.", VisibleText(find).Last());

        Find<TextBox>(find, "Query").Text = "lighthouse";
        find.Refresh();

        Assert.Equal("No matches", Find<TextBlock>(find, "Summary").Text);
        Assert.Equal("Nothing in this project matches “lighthouse”.", VisibleText(find).Last());
        Assert.False(Find<Button>(find, "ReplaceAll").IsEnabled);
    });

    [Fact]
    public Task Replace_all_writes_every_scene_and_page_and_Undo_spares_words_typed_since() => OnProject((window, workspace) =>
    {
        workspace.OpenFindInProject();
        var find = Find<FindPanel>(workspace, "FindPane");
        Find<TextBox>(find, "Query").Text = "Theo";
        Find<TextBox>(find, "Replacement").Text = "Teddy";
        Find<CheckBox>(find, "WholeWords").IsChecked = true;
        Settle();

        Click(window, Find<Button>(find, "ReplaceAll"));
        window.Saver!.Flush();

        Assert.Equal("Teddy waited in the fog.\n\nThe fog waited too.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.Equal("Mira found Teddy's coat at the harbour.", File.ReadAllText(_temp["stories/Tides/Chapter 2/Harbour.md"]));
        Assert.Equal("Teddy: a ferryman, invented.", File.ReadAllText(_temp["stories/Tides/Notes/Cast.md"]));
        Assert.Equal("Teddy waited in the fog.\n\nThe fog waited too.", workspace.Editor.Text);
        var toast = Find<Toast>(window, "Notice");
        Assert.Equal("Replaced 3 matches in 3 scenes", toast.Title);

        workspace.Editor.TextArea.Focus();
        workspace.Editor.CaretOffset = 0;
        window.KeyTextInput("Grey. ");
        Click(window, Find<Button>(toast, "ActionButton"));
        window.Saver.Flush();

        Assert.Equal("Grey. Teddy waited in the fog.\n\nThe fog waited too.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.Equal(Harbour, File.ReadAllText(_temp["stories/Tides/Chapter 2/Harbour.md"]));
        Assert.Equal(Cast, File.ReadAllText(_temp["stories/Tides/Notes/Cast.md"]));
    });

    public void Dispose() => _temp.Dispose();

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, time);
        Dispatcher.UIThread.PushFrame(frame);
        Settle();
    }

    private Task OnProject(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        test(window, window.Workspace!);
    });
}
