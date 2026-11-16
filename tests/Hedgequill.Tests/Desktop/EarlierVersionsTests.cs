using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>Earlier versions…: a scene's kept versions in a sheet, and Bring back this version.</summary>
public sealed class EarlierVersionsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public EarlierVersionsTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");

    private string Fog => _temp["stories/Tides/Chapter 1/Fog.md"];

    [Fact]
    public Task Bringing_back_a_version_puts_it_in_the_editor_and_on_disk_and_keeps_what_it_replaced() => OnProject((window, workspace) =>
    {
        workspace.Editor.TextArea.Focus();
        workspace.Editor.CaretOffset = 0;
        window.KeyTextInput("Grey ");
        var scene = workspace.OpenDocument!;

        workspace.ShowVersions(scene);
        Settle();
        var sheet = Find<VersionsSheet>(window, "Versions");
        Assert.True(sheet.IsVisible);
        Assert.Equal("Earlier versions of “Fog”", sheet.Title);
        Assert.StartsWith("Today, ", Find<ListBox>(sheet, "Kept").SelectedItem is VersionRow row ? row.When : "");
        Assert.Equal("Invented fog rolled in.", Find<SelectableTextBlock>(sheet, "KeptText").Text);

        Click(window, Find<Button>(sheet, "Restore"));
        window.Saver!.Flush();

        Assert.False(sheet.IsVisible);
        Assert.Equal("Invented fog rolled in.", workspace.Editor.Text);
        Assert.Equal("Invented fog rolled in.", File.ReadAllText(Fog));
        Assert.Equal(["Grey Invented fog rolled in.", "Invented fog rolled in."], workspace.Project.Versions(scene).Select(version => version.Text));
        Assert.StartsWith("Brought back the version from Today, ", Find<Toast>(window, "Notice").Title);
    });

    [Fact]
    public Task A_scene_with_no_versions_says_so_and_brings_nothing_back() => OnProject((window, workspace) =>
    {
        workspace.ShowVersions(workspace.OpenDocument!);
        Settle();
        var sheet = Find<VersionsSheet>(window, "Versions");

        Assert.StartsWith("No earlier versions of this scene yet.", Find<SelectableTextBlock>(sheet, "KeptText").Text);
        Assert.False(Find<Button>(sheet, "Restore").IsEnabled);
        Click(window, Find<Button>(sheet, "Cancel"));
        Assert.False(sheet.IsVisible);
    });

    public void Dispose() => _temp.Dispose();

    private Task OnProject(Action<MainWindow, WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        test(window, window.Workspace!);
    });
}
