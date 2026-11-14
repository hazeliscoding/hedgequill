using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>Menus that open in a popup: right-clicking a row, the navigator's More button, and the caption row's menu bar.</summary>
public sealed class PopupMenuTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public PopupMenuTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Notes/Mara.md", "Keeper of the lantern.");
    }

    [Fact]
    public Task Right_clicking_a_row_opens_its_menu_like_the_prototype() => OnProject((window, workspace) =>
    {
        var menu = RightClick(window, workspace, "Fog");
        Assert.Equal(["Rename", "Duplicate", "-", "Delete scene"], Headers(menu));
        Assert.Equal("Fog", workspace.Selected?.Title);
        menu.Hide();

        Assert.Equal(["New scene", "Rename", "Duplicate", "-", "Delete chapter"], Headers(RightClick(window, workspace, "Chapter 1")));
        Find<ListBox>(workspace, "Navigator").ContextFlyout!.Hide();
        Assert.Equal(["New page"], Headers(RightClick(window, workspace, "Notes")));
    });

    [Fact]
    public Task Choosing_from_a_rows_menu_runs_it() => OnProject((window, workspace) =>
    {
        var menu = RightClick(window, workspace, "Fog");

        Choose(menu, "Duplicate");

        Assert.Equal("Invented fog rolled in.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog copy.md"]));
        Assert.Equal("Fog copy", workspace.Selected?.Title);
    });

    [Fact]
    public Task Right_clicking_empty_space_offers_the_add_menu() => OnProject((window, workspace) =>
    {
        var navigator = Find<ListBox>(workspace, "Navigator");
        var below = navigator.TranslatePoint(new Point(navigator.Bounds.Width / 2, navigator.Bounds.Height - 10), window)!.Value;
        window.MouseDown(below, MouseButton.Right);
        window.MouseUp(below, MouseButton.Right);
        Settle();

        var menu = Assert.IsType<MenuFlyout>(navigator.ContextFlyout);
        Assert.True(menu.IsOpen);
        Assert.Equal(["New scene", "New chapter", "New act", "-", "New note", "Collapse all"], Headers(menu));
    });

    [Fact]
    public Task More_opens_the_add_menu() => OnProject((window, workspace) =>
    {
        var more = Find<Button>(workspace, "More");

        Click(window, more);

        var menu = Assert.IsType<MenuFlyout>(more.Flyout);
        Assert.True(menu.IsOpen);
        Choose(menu, "New act");
        Assert.True(Directory.Exists(_temp["stories/Tides/New act"]));
        Assert.Equal("New act", workspace.Renaming?.Title);
    });

    [Fact]
    public Task The_caption_rows_menus_drop_down_and_run_their_items()
    {
        Assert.SkipWhen(OperatingSystem.IsMacOS(), "macOS shows the menu at the top of the screen, not in the window.");
        return OnProject((window, workspace) =>
        {
            var file = Visible<MenuItem>(Find<NativeMenuBar>(window, "MenuBar")).Single(item => Equals(item.Header, "File"));

            Click(window, file);

            Assert.True(file.IsSubMenuOpen);
            var items = file.GetRealizedContainers().OfType<MenuItem>().ToList();
            Assert.Equal(
                ["New project…", "Open folder…", "Open recent", "New scene", "New chapter", "Show project folder", "Close project", "Exit"],
                items.Select(item => item.Header as string));

            items.Single(item => Equals(item.Header, "Close project")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Settle();

            Assert.IsType<WelcomeView>(Find<ContentControl>(window, "Screen").Content);
        });
    }

    public void Dispose() => _temp.Dispose();

    private static MenuFlyout RightClick(MainWindow window, WorkspaceView workspace, string title)
    {
        var row = Visible<ListBoxItem>(workspace).Single(item => item.DataContext is NavigatorRow navigatorRow && navigatorRow.Title == title);
        var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        Settle();
        var menu = Assert.IsType<MenuFlyout>(Find<ListBox>(workspace, "Navigator").ContextFlyout);
        Assert.True(menu.IsOpen);
        return menu;
    }

    private static List<string?> Headers(MenuFlyout menu) =>
        menu.ItemsSource!.Cast<Control>().Select(item => item is MenuItem named ? named.Header as string : "-").ToList();

    private static void Choose(MenuFlyout menu, string header)
    {
        var item = menu.ItemsSource!.OfType<MenuItem>().Single(item => Equals(item.Header, header));
        menu.Hide();
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Settle();
    }

    private Task OnProject(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        test(window, window.Workspace!);
    });
}
