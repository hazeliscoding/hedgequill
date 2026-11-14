using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class WorkspaceTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public Task New_project_creates_the_folder_and_opens_its_first_scene() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Click(window, Find<Button>(window, "NewProject"));
        var sheet = Find<NewProjectSheet>(window, "Sheet");
        Assert.True(sheet.IsVisible);
        Assert.Equal(NewProjectSheet.DefaultLocation, sheet.Location);
        Assert.Same(Find<TextBox>(sheet, "TitleBox"), window.FocusManager?.GetFocusedElement());

        sheet.Location = _temp["stories"];
        window.KeyTextInput("The Lantern Keeper");
        Press(window, PhysicalKey.Enter);

        Assert.False(sheet.IsVisible);
        Assert.True(File.Exists(_temp["stories/The Lantern Keeper/Chapter 1/Scene 1.md"]));
        Assert.Equal("The Lantern Keeper — Hedgequill", window.Title);
        var workspace = Assert.IsType<WorkspaceView>(Find<ContentControl>(window, "Screen").Content);
        Assert.Equal(["1 Chapter 1", "Scene 1", "Notes", "Research"], Rows(workspace));
        Assert.Equal("Scene 1", workspace.Selected?.Title);
        Assert.Equal(["The Lantern Keeper", "Scene 1", "Chapter 1 — Chapter 1"], VisibleText(workspace).Where(text => text is "The Lantern Keeper" or "Scene 1" or "Chapter 1 — Chapter 1").Distinct());
        Assert.Equal("The Lantern Keeper", new RecentProjects(_temp["settings"]).Load().Single().Title);
        Assert.Empty(Errors.Messages);
    });

    [Fact]
    public Task Escape_closes_the_sheet_without_creating_anything() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Click(window, Find<Button>(window, "NewProject"));
        var sheet = Find<NewProjectSheet>(window, "Sheet");
        sheet.Location = _temp["stories"];

        Press(window, PhysicalKey.Escape);

        Assert.False(sheet.IsVisible);
        Assert.False(Directory.Exists(_temp["stories"]));
        Assert.Null(window.Project);
    });

    [Fact]
    public Task A_project_that_cant_be_created_keeps_the_sheet_open_with_a_notice() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        _temp.Write("blocked", "A file where the location's folder should be.");
        Click(window, Find<Button>(window, "NewProject"));
        var sheet = Find<NewProjectSheet>(window, "Sheet");
        sheet.Location = _temp["blocked"];
        sheet.ProjectTitle = "Small Hours";

        sheet.Submit();

        Assert.True(sheet.IsVisible);
        Assert.Null(window.Project);
        Assert.Contains("Couldn't create the project", VisibleText(window));
    });

    [Fact]
    public Task The_navigator_shows_acts_numbered_chapters_scenes_and_pages() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        _temp.Write("stories/Tides/Prologue/Fog.md", "Invented fog.");
        _temp.Write("stories/Tides/Act One/Arrival/Platform.md", "Invented rain.");
        _temp.Write("stories/Tides/Act One/Departure/Ferry.md", "Invented ferry.");
        _temp.Write("stories/Tides/Notes/Mara.md", "Invented keeper.");
        _temp.Write("stories/Tides/hedgequill.json", """{ "format": 1, "order": { "": ["Prologue", "Act One"] } }""");

        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;

        Assert.Equal(["1 Prologue", "Fog", "Act One", "2 Arrival", "Platform", "3 Departure", "Ferry", "Notes", "Mara", "Research"], Rows(workspace));
        Assert.Equal("Fog", workspace.Selected?.Title);

        workspace.Select(workspace.Rows.Single(row => row.Title == "Act One").Node);
        Assert.Contains("2 chapters. Acts only group chapters; select a scene to write.", VisibleText(workspace));
    });

    [Fact]
    public Task Collapsing_a_chapter_hides_its_scenes_and_selects_the_chapter() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md");
        _temp.Write("stories/Tides/Chapter 2/Rain.md");
        window.Open(_temp["stories/Tides"]);
        var workspace = (WorkspaceView)Find<ContentControl>(window, "Screen").Content!;

        Click(window, Visible<Button>(workspace).First(button => button.Classes.Contains("chevron")));

        Assert.Equal(["1 Chapter 1", "2 Chapter 2", "Rain", "Notes", "Research"], Rows(workspace));
        Assert.Equal("Chapter 1", workspace.Selected?.Title);
    });

    public void Dispose() => _temp.Dispose();

    private static List<string> Rows(WorkspaceView workspace) =>
        workspace.Rows.Select(row => row.Number is null ? row.Title : $"{row.Number} {row.Title}").ToList();
}
