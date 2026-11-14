using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class NavigatorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public NavigatorTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog.");
        _temp.Write("stories/Tides/Chapter 1/Rain.md", "Invented rain.");
        _temp.Write("stories/Tides/Chapter 2/Ferry.md", "Invented ferry.");
    }

    [Fact]
    public Task New_scene_adds_one_to_the_chapter_and_names_it_in_place() => OnWorkspace((window, workspace) =>
    {
        Click(window, Find<Button>(workspace, "NewScene"));
        var box = Assert.IsType<TextBox>(window.FocusManager?.GetFocusedElement());
        Assert.Equal("Untitled scene", box.SelectedText);

        window.KeyTextInput("The causeway");
        Press(window, PhysicalKey.Enter);

        Assert.True(File.Exists(_temp["stories/Tides/Chapter 1/The causeway.md"]));
        Assert.Equal(["1 Chapter 1", "Fog", "Rain", "The causeway", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));
        Assert.Equal("The causeway", workspace.Selected?.Title);
        Assert.Equal("The causeway", (window.FocusManager?.GetFocusedElement() as ListBoxItem)?.DataContext is NavigatorRow row ? row.Title : null);
        Assert.Empty(Errors.Messages);
    });

    [Fact]
    public Task F2_renames_and_Escape_keeps_the_old_title() => OnWorkspace((window, workspace) =>
    {
        Press(window, PhysicalKey.F2);
        window.KeyTextInput("Mist");
        Press(window, PhysicalKey.Escape);

        Assert.Null(workspace.Renaming);
        Assert.Equal("Fog", workspace.Selected?.Title);
        Assert.True(File.Exists(_temp["stories/Tides/Chapter 1/Fog.md"]));

        Press(window, PhysicalKey.F2);
        window.KeyTextInput("Sea fret?");
        Press(window, PhysicalKey.Enter);

        Assert.Equal("Sea fret?", workspace.Selected?.Title);
        Assert.Equal("Invented fog.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Sea fret.md"]));
    });

    [Fact]
    public Task Delete_asks_then_moves_to_Trash_and_Undo_puts_it_back() => OnWorkspace((window, workspace) =>
    {
        Press(window, PhysicalKey.Delete);

        Assert.Equal(
            ["Delete “Fog”?", "The scene's file moves to the project's Trash folder. You can restore it from there.", "Cancel", "Delete"],
            VisibleText(Find<ConfirmSheet>(window, "Confirmation")));

        Press(window, PhysicalKey.Enter);

        Assert.False(Find<ConfirmSheet>(window, "Confirmation").IsVisible);
        Assert.Equal("Invented fog.", File.ReadAllText(_temp["stories/Tides/Trash/Chapter 1/Fog.md"]));
        Assert.Equal(["1 Chapter 1", "Rain", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));
        Assert.Equal("Chapter 1", workspace.Selected?.Title);
        var notice = Find<Toast>(window, "Notice");
        Assert.Equal(["Moved “Fog” to Trash", "Undo"], VisibleText(notice));

        Click(window, Find<Button>(notice, "ActionButton"));

        Assert.True(File.Exists(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.False(Directory.Exists(_temp["stories/Tides/Trash"]));
        Assert.Equal(["1 Chapter 1", "Fog", "Rain", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));
        Assert.Equal("Fog", workspace.Selected?.Title);
        Assert.False(notice.IsVisible);
    });

    [Fact]
    public Task Escape_cancels_a_delete() => OnWorkspace((window, workspace) =>
    {
        Press(window, PhysicalKey.Delete);
        Press(window, PhysicalKey.Escape);

        Assert.False(Find<ConfirmSheet>(window, "Confirmation").IsVisible);
        Assert.True(File.Exists(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.Equal("Fog", workspace.Selected?.Title);
    });

    [Fact]
    public Task Alt_arrows_reorder_and_a_scene_at_the_edge_moves_into_the_next_chapter() => OnWorkspace((window, workspace) =>
    {
        Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        Assert.Equal(["1 Chapter 1", "Rain", "Fog", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));

        Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        Assert.Equal(["1 Chapter 1", "Rain", "2 Chapter 2", "Fog", "Ferry", "Notes", "Research"], Rows(workspace));
        Assert.Equal("Fog", workspace.Selected?.Title);
        Assert.True(File.Exists(_temp["stories/Tides/Chapter 2/Fog.md"]));

        Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
        Assert.Equal(["1 Chapter 1", "Rain", "Fog", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(Reopened(window)));
    });

    [Fact]
    public Task Dragging_a_scene_onto_a_chapter_moves_it_in_and_between_scenes_reorders() => OnWorkspace((window, workspace) =>
    {
        Drag(window, workspace, "Fog", "Chapter 2", 0.5);
        Assert.Equal(["1 Chapter 1", "Rain", "2 Chapter 2", "Ferry", "Fog", "Notes", "Research"], Rows(workspace));
        Assert.True(File.Exists(_temp["stories/Tides/Chapter 2/Fog.md"]));

        Drag(window, workspace, "Fog", "Ferry", 0.1);
        Assert.Equal(["1 Chapter 1", "Rain", "2 Chapter 2", "Fog", "Ferry", "Notes", "Research"], Rows(workspace));

        Drag(window, workspace, "Rain", "Notes", 0.5);
        Assert.Equal(["1 Chapter 1", "Rain", "2 Chapter 2", "Fog", "Ferry", "Notes", "Research"], Rows(workspace));
        Assert.False(Find<Border>(workspace, "DropLine").IsVisible);
    });

    [Fact]
    public Task Left_collapses_and_goes_up_and_Right_expands() => OnWorkspace((window, workspace) =>
    {
        Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal("Chapter 1", workspace.Selected?.Title);

        Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal(["1 Chapter 1", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));

        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(["1 Chapter 1", "Fog", "Rain", "2 Chapter 2", "Ferry", "Notes", "Research"], Rows(workspace));
    });

    [Theory]
    [InlineData("Fog", 0.1, Placement.Before)]
    [InlineData("Fog", 0.5, Placement.After)]
    [InlineData("Chapter 2", 0.5, Placement.Into)]
    [InlineData("Chapter 2", 0.1, null)]
    [InlineData("Notes", 0.5, null)]
    public void A_scene_drops_only_where_the_prototype_allows(string target, double fraction, Placement? expected)
    {
        var project = Project.Open(_temp["stories/Tides"]);
        var all = project.Root.Descendants().ToList();

        Assert.Equal(expected, WorkspaceView.DropOn(all.Single(node => node.Title == "Ferry"), all.Single(node => node.Title == target), fraction));
    }

    public void Dispose() => _temp.Dispose();

    private static List<string> Rows(WorkspaceView workspace) =>
        workspace.Rows.Select(row => row.Number is null ? row.Title : $"{row.Number} {row.Title}").ToList();

    private static void Drag(MainWindow window, WorkspaceView workspace, string from, string to, double fraction)
    {
        Settle();
        var rows = Visible<ListBoxItem>(workspace).ToList();
        Point At(string title, double y)
        {
            var row = rows.Single(item => item.DataContext is NavigatorRow navigatorRow && navigatorRow.Title == title);
            return row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height * y), window)!.Value;
        }

        var start = At(from, 0.5);
        var end = At(to, fraction);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X, start.Y + 6), RawInputModifiers.LeftMouseButton);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Settle();
    }

    private WorkspaceView Reopened(MainWindow window)
    {
        window.Open(_temp["stories/Tides"]);
        return (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;
    }

    private Task OnWorkspace(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;
        workspace.FocusNavigator();
        Settle();
        Assert.Equal("Fog", workspace.Selected?.Title);
        test(window, workspace);
    });
}
