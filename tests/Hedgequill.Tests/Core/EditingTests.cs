using Hedgequill.Core.Projects;

namespace Hedgequill.Tests.Core;

public sealed class EditingTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void Adding_makes_untitled_files_and_folders_with_names_that_dont_clash()
    {
        _temp.Write("Chapter 1/Untitled scene.md", "Invented words already here.");
        var project = Project.Open(_temp.Path);
        var chapter = Child(project.Root, "Chapter 1");

        var scene = project.Add(chapter, NodeKind.Scene);
        var act = project.Add(project.Root, NodeKind.Act);
        var page = project.Add(Child(project.Root, "Notes"), NodeKind.Page);

        Assert.Equal("Untitled scene 2", scene.Title);
        Assert.Equal("New act", act.Title);
        Assert.Equal("Untitled page", page.Title);
        Assert.Equal(
            ["Chapter 1/", "Chapter 1/Untitled scene 2.md", "Chapter 1/Untitled scene.md", "New act/", "Notes/", "Notes/Untitled page.md", "hedgequill.json"],
            _temp.Tree());
        Assert.Equal(
            ["Chapter 1 [Chapter]", "  Untitled scene [Scene]", "  Untitled scene 2 [Scene]", "New act [Act]", "Notes [Notes]", "  Untitled page [Page]", "Research [Research]"],
            Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Only_the_right_kind_of_child_can_be_added()
    {
        var project = Project.Create(_temp.Path, "Small Hours");
        var before = _temp.Tree().ToList();

        Assert.Throws<InvalidOperationException>(() => project.Add(project.Root, NodeKind.Scene));
        Assert.Throws<InvalidOperationException>(() => project.Add(Child(project.Root, "Notes"), NodeKind.Scene));
        Assert.Equal(before, _temp.Tree());
    }

    [Fact]
    public void Renaming_renames_the_file_and_keeps_a_title_the_name_cant_hold()
    {
        var project = Project.Create(_temp.Path, "Small Hours");
        var scene = project.Scenes.Single();
        scene.Text = "Words that must come along.";
        File.WriteAllText(project.PathOf(scene), scene.Text);

        project.Rename(scene, "  What now?  ");

        Assert.Equal("What now?", scene.Title);
        Assert.Equal("Words that must come along.", File.ReadAllText(_temp["Small Hours/Chapter 1/What now.md"]));
        Assert.False(File.Exists(_temp["Small Hours/Chapter 1/Scene 1.md"]));
        Assert.Equal("What now?", Project.Open(project.Folder).Scenes.Single().Title);
    }

    [Fact]
    public void Renaming_a_folder_carries_its_scenes_and_a_case_change_is_allowed()
    {
        var project = Project.Create(_temp.Path, "Small Hours");
        var chapter = Child(project.Root, "Chapter 1");

        project.Rename(chapter, "chapter one");
        project.Rename(chapter, "Chapter One");

        Assert.Equal(["Chapter One/", "Chapter One/Scene 1.md"], _temp.Tree("Small Hours").Where(entry => entry.StartsWith("Small Hours/Chapter", StringComparison.Ordinal)).Select(entry => entry["Small Hours/".Length..]));
        Assert.Equal(["Chapter One [Chapter]", "  Scene 1 [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(project.Folder)));
    }

    [Fact]
    public void A_name_already_taken_or_reserved_gets_a_number_and_keeps_its_title()
    {
        _temp.Write("Chapter 1/Alpha.md");
        _temp.Write("Chapter 1/Beta.md");
        var project = Project.Open(_temp.Path);
        var chapter = Child(project.Root, "Chapter 1");

        project.Rename(Child(chapter, "Beta"), "Alpha");
        project.Rename(chapter, "Notes");

        Assert.True(File.Exists(_temp["Notes 2/Alpha 2.md"]));
        Assert.Equal(["Notes [Chapter]", "  Alpha [Scene]", "  Alpha [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Reordering_keeps_the_files_and_changes_the_index()
    {
        _temp.Write("Chapter 1/Alpha.md");
        _temp.Write("Chapter 1/Beta.md");
        _temp.Write("Chapter 1/Gamma.md");
        var project = Project.Open(_temp.Path);
        var chapter = Child(project.Root, "Chapter 1");
        var before = _temp.Tree().Where(entry => entry.EndsWith(".md", StringComparison.Ordinal)).ToList();

        project.Move(Child(chapter, "Gamma"), chapter, 0);

        Assert.Equal(before, _temp.Tree().Where(entry => entry.EndsWith(".md", StringComparison.Ordinal)));
        Assert.Equal(["Chapter 1 [Chapter]", "  Gamma [Scene]", "  Alpha [Scene]", "  Beta [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Moving_to_another_folder_moves_the_file_and_a_clash_gets_a_number()
    {
        _temp.Write("Chapter 1/Opening.md", "First opening.");
        _temp.Write("Chapter 2/Opening.md", "Second opening.");
        _temp.Write("Act One/Chapter 3/Later.md");
        var project = Project.Open(_temp.Path);
        var act = Child(project.Root, "Act One");

        project.Move(Child(Child(project.Root, "Chapter 2"), "Opening"), Child(project.Root, "Chapter 1"), 1);
        project.Move(Child(project.Root, "Chapter 1"), act, 0);

        Assert.Equal("Second opening.", File.ReadAllText(_temp["Act One/Chapter 1/Opening 2.md"]));
        Assert.Equal(
            ["Act One [Act]", "  Chapter 1 [Chapter]", "    Opening [Scene]", "    Opening [Scene]", "  Chapter 3 [Chapter]", "    Later [Scene]", "Chapter 2 [Chapter]", "Notes [Notes]", "Research [Research]"],
            Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void A_move_the_outline_doesnt_allow_changes_nothing()
    {
        var project = Project.Create(_temp.Path, "Small Hours");
        var before = _temp.Tree().ToList();

        Assert.Throws<InvalidOperationException>(() => project.Move(project.Scenes.Single(), project.Root, 0));
        Assert.Throws<InvalidOperationException>(() => project.Move(Child(project.Root, "Chapter 1"), Child(project.Root, "Notes"), 0));
        Assert.Equal(before, _temp.Tree());
    }

    [Fact]
    public void A_chapter_at_the_top_stays_above_Notes_and_Research()
    {
        _temp.Write("Act One/Chapter 1/Scene.md");
        var project = Project.Open(_temp.Path);

        project.Move(Child(Child(project.Root, "Act One"), "Chapter 1"), project.Root, 99);

        Assert.Equal(["Act One [Act]", "Chapter 1 [Chapter]", "  Scene [Scene]", "Notes [Notes]", "Research [Research]"], Outline(project));
        Assert.Equal(["Act One [Act]", "Chapter 1 [Chapter]", "  Scene [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Delete_moves_into_Trash_under_the_same_folders_and_Undo_puts_it_back()
    {
        _temp.Write("Act One/Chapter 1/Alpha.md", "Alpha's invented words.");
        _temp.Write("Act One/Chapter 1/Beta.md", "Beta's invented words.");
        var project = Project.Open(_temp.Path);
        var chapter = Child(Child(project.Root, "Act One"), "Chapter 1");

        var deleted = project.Delete(Child(chapter, "Alpha"));

        Assert.Equal("Alpha's invented words.", File.ReadAllText(_temp["Trash/Act One/Chapter 1/Alpha.md"]));
        Assert.False(File.Exists(_temp["Act One/Chapter 1/Alpha.md"]));
        Assert.Equal(["Act One [Act]", "  Chapter 1 [Chapter]", "    Beta [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));

        project.Restore(deleted);

        Assert.Equal("Alpha's invented words.", File.ReadAllText(_temp["Act One/Chapter 1/Alpha.md"]));
        Assert.False(Directory.Exists(_temp["Trash"]));
        Assert.Equal(["Act One [Act]", "  Chapter 1 [Chapter]", "    Alpha [Scene]", "    Beta [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Deleting_a_chapter_moves_its_folder_and_a_second_delete_of_the_same_name_doesnt_overwrite()
    {
        _temp.Write("Chapter 1/Scene.md", "First.");
        var project = Project.Open(_temp.Path);
        project.Delete(Child(project.Root, "Chapter 1"));
        _temp.Write("Chapter 1/Scene.md", "Second.");
        project = Project.Open(_temp.Path);

        project.Delete(Child(project.Root, "Chapter 1"));

        Assert.Equal("First.", File.ReadAllText(_temp["Trash/Chapter 1/Scene.md"]));
        Assert.Equal("Second.", File.ReadAllText(_temp["Trash/Chapter 1 2/Scene.md"]));
    }

    [Fact]
    public void Undo_finds_a_free_name_when_the_old_one_was_taken()
    {
        _temp.Write("Chapter 1/Scene.md", "Deleted words.");
        var project = Project.Open(_temp.Path);
        var chapter = Child(project.Root, "Chapter 1");
        var deleted = project.Delete(Child(chapter, "Scene"));
        _temp.Write("Chapter 1/Scene.md", "Newer words.");

        project.Restore(deleted);

        Assert.Equal("Newer words.", File.ReadAllText(_temp["Chapter 1/Scene.md"]));
        Assert.Equal("Deleted words.", File.ReadAllText(_temp["Chapter 1/Scene 2.md"]));
    }

    [Fact]
    public void Duplicate_copies_a_scene_with_its_unsaved_text_right_after_it()
    {
        _temp.Write("Chapter 1/Fog.md", "Saved fog.");
        _temp.Write("Chapter 1/Rain.md", "Rain.");
        var project = Project.Open(_temp.Path);
        var fog = project.Scenes.First();
        fog.Text = "Fog not saved yet.";

        var copy = project.Duplicate(fog);

        Assert.Equal("Fog copy", copy.Title);
        Assert.Equal("Fog not saved yet.", File.ReadAllText(_temp["Chapter 1/Fog copy.md"]));
        Assert.Equal("Saved fog.", File.ReadAllText(_temp["Chapter 1/Fog.md"]));
        Assert.Equal(["Chapter 1 [Chapter]", "  Fog [Scene]", "  Fog copy [Scene]", "  Rain [Scene]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(_temp.Path)));
    }

    [Fact]
    public void Duplicating_a_chapter_copies_its_folder_and_scenes()
    {
        _temp.Write("Chapter 1/Fog.md", "Fog.");
        _temp.Write("Chapter 1/Rain.md", "Rain.");
        var project = Project.Open(_temp.Path);

        project.Duplicate(Child(project.Root, "Chapter 1"));
        project.Duplicate(Child(project.Root, "Chapter 1"));

        Assert.Equal("Rain.", File.ReadAllText(_temp["Chapter 1 copy/Rain.md"]));
        Assert.Equal(
            ["Chapter 1 [Chapter]", "  Fog [Scene]", "  Rain [Scene]", "Chapter 1 copy [Chapter]", "  Fog [Scene]", "  Rain [Scene]",
             "Chapter 1 copy [Chapter]", "  Fog [Scene]", "  Rain [Scene]", "Notes [Notes]", "Research [Research]"],
            Outline(Project.Open(_temp.Path)));
        Assert.True(Directory.Exists(_temp["Chapter 1 copy 2"]));
    }

    [Fact]
    public void Acts_and_groups_dont_duplicate()
    {
        var project = Project.Create(_temp.Path, "Small Hours");

        Assert.Throws<InvalidOperationException>(() => project.Duplicate(project.Add(project.Root, NodeKind.Act)));
        Assert.Throws<InvalidOperationException>(() => project.Duplicate(Child(project.Root, "Notes")));
    }

    public void Dispose() => _temp.Dispose();

    private static Node Child(Node parent, string title) => parent.Children.Single(node => node.Title == title && node.Name.StartsWith(title, StringComparison.Ordinal));

    private static List<string> Outline(Project project)
    {
        var lines = new List<string>();
        Walk(project.Root, 0);
        return lines;

        void Walk(Node parent, int depth)
        {
            foreach (var node in parent.Children)
            {
                lines.Add($"{new string(' ', depth * 2)}{node.Title} [{node.Kind}]");
                Walk(node, depth + 1);
            }
        }
    }
}
