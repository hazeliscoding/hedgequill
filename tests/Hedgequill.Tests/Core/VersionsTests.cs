using Hedgequill.Core.Projects;

namespace Hedgequill.Tests.Core;

public sealed class VersionsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void The_first_save_in_a_session_keeps_the_text_from_before_as_plain_Markdown()
    {
        _temp.Write("Chapter 1/Fog.md", "The fog came in slowly.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();

        scene.Text = "The fog came in fast.";
        project.Save(scene);
        scene.Text = "The fog came in all at once.";
        project.Save(scene);

        var version = Assert.Single(project.Versions(scene));
        Assert.Equal("The fog came in slowly.", version.Text);
        Assert.Equal(_temp["Versions/Chapter 1/Fog"], Path.GetDirectoryName(version.Path));
        Assert.Equal("The fog came in all at once.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));

        // The next session keeps the text this one left.
        var next = Project.Open(_temp.Path);
        var again = next.Scenes.Single();
        again.Text = "Fog, everywhere.";
        next.Save(again);
        Assert.Equal(["The fog came in all at once.", "The fog came in slowly."], next.Versions(again).Select(kept => kept.Text));
    }

    [Fact]
    public void Versions_stay_out_of_the_outline_and_follow_a_rename_and_a_move()
    {
        _temp.Write("Chapter 1/Fog.md", "Grey morning.");
        _temp.Folder("Chapter 2");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        scene.Text = "Grey afternoon.";
        project.Save(scene);

        var reopened = Project.Open(_temp.Path);
        Assert.Equal(["Chapter 1", "Chapter 2", "Notes", "Research"], reopened.Root.Children.Select(node => node.Name));

        project.Rename(scene, "Mist");
        Assert.Equal("Grey morning.", Assert.Single(project.Versions(scene)).Text);
        project.Move(scene, project.Root.Children[1], 0);
        Assert.Equal("Grey morning.", Assert.Single(project.Versions(scene)).Text);
        project.Rename(project.Root.Children[1], "Two");
        Assert.Equal(_temp["Versions/Two/Mist"], Path.GetDirectoryName(Assert.Single(project.Versions(scene)).Path));
    }

    [Fact]
    public void Bringing_back_a_version_keeps_the_text_it_replaces()
    {
        _temp.Write("Chapter 1/Fog.md", "First draft of the harbour.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        scene.Text = "Second draft of the harbour.";
        project.Save(scene);

        project.BringBack(scene, project.Versions(scene)[0]);
        project.Save(scene);

        Assert.Equal("First draft of the harbour.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
        Assert.Equal(["Second draft of the harbour.", "First draft of the harbour."], project.Versions(scene).Select(kept => kept.Text));
    }

    [Fact]
    public void Bringing_back_a_version_over_unsaved_edits_keeps_both_the_edits_and_the_text_on_disk()
    {
        _temp.Write("Chapter 1/Fog.md", "Rain on the pier.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        scene.Text = "Snow on the pier.";
        project.Save(scene);
        var reopened = Project.Open(_temp.Path);
        var again = reopened.Scenes.Single();
        again.Text = "Hail on the pier, unsaved.";

        reopened.BringBack(again, reopened.Versions(again).Single());

        Assert.Equal("Rain on the pier.", again.Text);
        Assert.Equal(["Hail on the pier, unsaved.", "Snow on the pier.", "Rain on the pier."], reopened.Versions(again).Select(kept => kept.Text));
    }

    [Fact]
    public void An_empty_scene_and_a_page_keep_no_versions()
    {
        _temp.Write("Chapter 1/Fog.md");
        _temp.Write("Notes/Ideas.md", "An invented idea.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        var page = project.Root.Descendants().Single(node => node.Kind == NodeKind.Page);
        scene.Text = "The first words.";
        page.Text = "A better idea.";
        project.Save(scene);
        project.Save(page);

        Assert.Empty(project.Versions(scene));
        Assert.False(Directory.Exists(_temp["Versions"]));
    }

    public void Dispose() => _temp.Dispose();
}
