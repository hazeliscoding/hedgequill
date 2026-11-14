using Hedgequill.Core.Projects;

namespace Hedgequill.Tests.Core;

public sealed class SavingTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void Saving_writes_the_text_with_LF_line_endings()
    {
        _temp.Write("Chapter 1/Fog.md", "Old line one.\r\nOld line two.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        Assert.Equal("Old line one.\nOld line two.", scene.Text);

        scene.Text = "New line one.\r\nNew line two.";
        project.Save(scene);

        Assert.Equal("New line one.\nNew line two.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
        Assert.Equal("New line one.\nNew line two.", scene.SavedText);
    }

    [Fact]
    public void A_change_made_in_another_app_isnt_overwritten_unless_asked()
    {
        _temp.Write("Chapter 1/Fog.md", "Original words.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        _temp.Write("Chapter 1/Fog.md", "Words from another editor.");
        scene.Text = "Words typed in Hedgequill.";

        Assert.Throws<ChangedOnDiskException>(() => project.Save(scene));
        Assert.Equal("Words from another editor.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));

        project.Save(scene, overwrite: true);
        Assert.Equal("Words typed in Hedgequill.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
    }

    [Fact]
    public void Reload_takes_the_text_on_disk()
    {
        _temp.Write("Chapter 1/Fog.md", "Original words.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        _temp.Write("Chapter 1/Fog.md", "Words from another editor.");
        scene.Text = "Words typed in Hedgequill.";

        project.Reload(scene);

        Assert.Equal("Words from another editor.", scene.Text);
        project.Save(scene);
        Assert.Equal("Words from another editor.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
    }

    [Fact]
    public void The_same_change_in_both_places_isnt_a_conflict()
    {
        _temp.Write("Chapter 1/Fog.md", "Original words.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        _temp.Write("Chapter 1/Fog.md", "Agreed words.");
        scene.Text = "Agreed words.";

        project.Save(scene);

        Assert.Equal("Agreed words.", scene.SavedText);
    }

    [Fact]
    public void A_failed_write_keeps_the_text_in_memory_and_Save_a_copy_writes_it_elsewhere()
    {
        _temp.Write("Chapter 1/Fog.md", "Original words.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        Directory.Delete(_temp["Chapter 1"], recursive: true);
        _temp.Write("Chapter 1", "A file where the chapter's folder was.");
        scene.Text = "Words that must survive.";

        Assert.ThrowsAny<IOException>(() => project.Save(scene));

        Assert.Equal("Words that must survive.", scene.Text);
        Assert.Equal("Original words.", scene.SavedText);
        Project.SaveCopy(scene, _temp["copy.md"]);
        Assert.Equal("Words that must survive.", File.ReadAllText(_temp["copy.md"]));
    }

    [Fact]
    public void A_folder_deleted_in_another_app_comes_back_on_save()
    {
        _temp.Write("Chapter 1/Fog.md", "Original words.");
        var project = Project.Open(_temp.Path);
        var scene = project.Scenes.Single();
        Directory.Delete(_temp["Chapter 1"], recursive: true);
        scene.Text = "Words that must survive.";

        project.Save(scene);

        Assert.Equal("Words that must survive.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
    }

    [Fact]
    public void Save_as_copies_the_project_with_the_text_in_memory()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Original words.");
        _temp.Write("stories/Tides/Notes/cover.png", "not markdown");
        var project = Project.Open(_temp["stories/Tides"]);
        project.Scenes.Single().Text = "Unsaved words.";

        var copy = project.SaveAs(_temp["elsewhere"]);

        Assert.Equal(_temp["elsewhere/Tides"], copy.Folder);
        Assert.Equal("Unsaved words.", File.ReadAllText(_temp["elsewhere/Tides/Chapter 1/Fog.md"]));
        Assert.True(File.Exists(_temp["elsewhere/Tides/Notes/cover.png"]));
        Assert.Equal("Original words.", File.ReadAllText(_temp["stories/Tides/Chapter 1/Fog.md"]));
        Assert.Equal("Unsaved words.", copy.Scenes.Single().Text);
    }

    public void Dispose() => _temp.Dispose();
}
