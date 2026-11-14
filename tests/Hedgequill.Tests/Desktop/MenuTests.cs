using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Styling;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class MenuTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public MenuTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Notes/Mara.md", "Keeper of the lantern.");
    }

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task The_menu_bar_has_the_prototypes_menus_and_items() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        var exit = OperatingSystem.IsMacOS() ? [] : new[] { "-", "Exit" };
        Assert.Equal(["File", "Edit", "View", "Chapter"], Menus(window).Select(menu => menu.Header));
        Assert.Equal(
            ["New project…", "Open folder…", "Open recent", "-", "New scene", "New chapter", "-", "Show project folder", "Close project", .. exit],
            Headers(window, "File"));
        Assert.Equal(
            ["Undo", "Redo", "-", "Cut", "Copy", "Paste", "-", "Bold", "Italic", "Insert scene break", "-", "Rename", "Find…"],
            Headers(window, "Edit"));
        Assert.Equal(
            ["Navigator", "Focus mode", "-", "Appearance", "Match system", "Light", "Dark", "-", "Collapse all"],
            Headers(window, "View"));
        Assert.Equal(["Open chapter overview", "Duplicate chapter", "-", "Delete chapter"], Headers(window, "Chapter"));
    });

    [Fact]
    public Task On_the_Welcome_screen_only_project_commands_are_on() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(Enabled(window, "File", "New project…"));
        Assert.True(Enabled(window, "File", "Open folder…"));
        Assert.False(Item(window, "File", "Open recent").IsEnabled);
        Assert.False(Enabled(window, "File", "New scene"));
        Assert.False(Enabled(window, "File", "Close project"));
        Assert.False(Enabled(window, "Edit", "Bold"));
        Assert.False(Enabled(window, "View", "Focus mode"));
        Assert.False(Enabled(window, "Chapter", "Delete chapter"));
    });

    [Fact]
    public Task Open_recent_lists_projects_and_opens_one()
    {
        new RecentProjects(_temp["settings"]).Add(Project.Open(_temp["stories/Tides"]), DateTimeOffset.Now);

        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            var recent = Item(window, "File", "Open recent");
            Assert.True(recent.IsEnabled);
            var tides = Assert.IsType<NativeMenuItem>(Assert.Single(recent.Menu!.Items));
            Assert.Equal("Tides", tides.Header);

            tides.Command!.Execute(null);

            Assert.Equal(_temp["stories/Tides"], window.Project?.Folder);
            Assert.True(Enabled(window, "File", "Close project"));
        });
    }

    [Fact]
    public Task Edit_commands_follow_what_is_open() => OnProject((window, workspace) =>
    {
        Assert.True(Enabled(window, "Edit", "Bold"));
        Assert.True(Enabled(window, "Edit", "Insert scene break"));
        Assert.True(Enabled(window, "Chapter", "Duplicate chapter"));

        workspace.Select(workspace.Rows.Single(row => row.Title == "Mara").Node);

        Assert.True(Enabled(window, "Edit", "Italic"));
        Assert.False(Enabled(window, "Edit", "Insert scene break"));
        Assert.False(Enabled(window, "Chapter", "Duplicate chapter"));
    });

    [Fact]
    public Task Bold_from_the_menu_wraps_the_selection_and_Undo_takes_it_back() => OnProject((window, workspace) =>
    {
        workspace.Editor.Select(9, 3);

        Run(window, "Edit", "Bold");
        Assert.Equal("Invented **fog** rolled in.", workspace.Editor.Text);

        Run(window, "Edit", "Undo");
        Assert.Equal("Invented fog rolled in.", workspace.Editor.Text);
    });

    [Fact]
    public Task Shortcuts_the_editor_leaves_alone_reach_the_menu() => OnProject((window, workspace) =>
    {
        Press(window, PhysicalKey.N, Command);

        Assert.True(File.Exists(_temp["stories/Tides/Chapter 1/Untitled scene.md"]));
        Assert.Equal("Untitled scene", workspace.Renaming?.Title);

        Press(window, PhysicalKey.Escape);
        Press(window, PhysicalKey.N, Command | RawInputModifiers.Shift);

        Assert.True(Find<NewProjectSheet>(window, "Sheet").IsVisible);
    });

    [Fact]
    public Task View_toggles_show_their_state() => OnProject((window, workspace) =>
    {
        Assert.True(Item(window, "View", "Navigator").IsChecked);
        Assert.False(Item(window, "View", "Focus mode").IsChecked);

        Run(window, "View", "Focus mode");

        Assert.True(workspace.FocusMode);
        Assert.True(Item(window, "View", "Focus mode").IsChecked);
        Assert.False(Enabled(window, "View", "Navigator"));

        Run(window, "View", "Focus mode");
        Run(window, "View", "Navigator");

        Assert.False(workspace.NavigatorShown);
        Assert.False(Item(window, "View", "Navigator").IsChecked);
    });

    [Fact]
    public Task Appearance_switches_the_theme_and_is_remembered() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(Item(window, "View", "Match system").IsChecked);

        Run(window, "View", "Dark");

        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        Assert.True(Item(window, "View", "Dark").IsChecked);
        Assert.False(Item(window, "View", "Match system").IsChecked);
        Assert.Equal(Appearance.Dark, new Preferences(_temp["settings"]).Appearance);

        Run(window, "View", "Match system");
        Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
    });

    [Fact]
    public Task Duplicate_chapter_and_Close_project_work_from_the_menu() => OnProject((window, workspace) =>
    {
        Run(window, "Chapter", "Duplicate chapter");

        Assert.Equal("Invented fog rolled in.", File.ReadAllText(_temp["stories/Tides/Chapter 1 copy/Fog.md"]));
        Assert.Equal("Chapter 1 copy", workspace.Selected?.Title);

        Run(window, "File", "Close project");

        Assert.IsType<WelcomeView>(Find<ContentControl>(window, "Screen").Content);
        Assert.False(Enabled(window, "File", "Close project"));
    });

    [Fact]
    public Task The_caption_row_carries_the_menu_and_title_except_on_macOS() => OnProject((window, workspace) =>
    {
        var caption = Find<Grid>(window, "CaptionRow");

        Assert.Equal(!OperatingSystem.IsMacOS(), caption.IsVisible);
        if (caption.IsVisible)
        {
            Assert.Equal(["File", "Edit", "View", "Chapter", "Tides — Hedgequill"], VisibleText(caption));
        }
    });

    [Fact]
    public Task The_menu_bar_takes_its_own_clicks_inside_the_draggable_caption_row() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(Find<Grid>(window, "CaptionRow")));
        Assert.Equal(WindowDecorationsElementRole.User, WindowDecorationProperties.GetElementRole(Find<NativeMenuBar>(window, "MenuBar")));
    });

    [Fact]
    public Task Navigator_rows_have_their_titles_as_accessible_names() => OnProject((window, workspace) =>
        Assert.Equal(
            ["Chapter 1", "Fog", "Notes", "Mara", "Research"],
            Visible<ListBoxItem>(workspace).Select(item => Avalonia.Automation.AutomationProperties.GetName(item))));

    public void Dispose() => _temp.Dispose();

    private static List<NativeMenuItem> Menus(Window window) => NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>().ToList();

    private static List<string?> Headers(Window window, string menu) =>
        Menus(window).Single(item => item.Header == menu).Menu!.Items.Select(item => item is NativeMenuItem named ? named.Header : "-").ToList();

    private static NativeMenuItem Item(Window window, string menu, string header) =>
        Menus(window).Single(item => item.Header == menu).Menu!.Items.OfType<NativeMenuItem>().Single(item => item.Header == header);

    private static bool Enabled(Window window, string menu, string header) => Item(window, menu, header).Command!.CanExecute(null);

    private static void Run(Window window, string menu, string header)
    {
        Item(window, menu, header).Command!.Execute(null);
        Settle();
    }

    private Task OnProject(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = window.Workspace!;
        workspace.Editor.TextArea.Focus();
        Settle();
        test(window, workspace);
    });
}
