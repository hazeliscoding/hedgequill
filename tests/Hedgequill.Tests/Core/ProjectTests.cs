using Hedgequill.Core.Ao3;
using Hedgequill.Core.Projects;

namespace Hedgequill.Tests.Core;

public class ProjectTests
{
    [Fact]
    public void A_new_project_is_a_folder_named_after_its_title()
    {
        using var temp = new TempFolder();

        var project = Project.Create(temp.Path, "  The Lantern Keeper ");

        Assert.Equal(
            ["The Lantern Keeper/", "The Lantern Keeper/Chapter 1/", "The Lantern Keeper/Chapter 1/Scene 1.md",
             "The Lantern Keeper/Notes/", "The Lantern Keeper/Research/", "The Lantern Keeper/hedgequill.json"],
            temp.Tree());
        Assert.Equal(temp["The Lantern Keeper"], project.Folder);
        Assert.Equal("The Lantern Keeper", project.Title);
        Assert.Equal(["Chapter 1 [Chapter]", "  Scene 1 [Scene]", "Notes [Notes]", "Research [Research]"], Outline(project));
    }

    [Fact]
    public void A_title_a_file_name_cant_hold_is_kept_in_the_index()
    {
        using var temp = new TempFolder();

        Project.Create(temp.Path, "What now?");
        var reopened = Project.Open(temp["What now"]);

        Assert.Equal("What now?", reopened.Title);
        Assert.Contains("\"\": \"What now?\"", File.ReadAllText(temp["What now/hedgequill.json"]));
    }

    [Fact]
    public void A_new_project_never_reuses_a_folder_that_exists()
    {
        using var temp = new TempFolder();
        temp.Write("Small Hours/draft.md", "Invented words that must stay.");

        var project = Project.Create(temp.Path, "Small Hours");

        Assert.Equal(temp["Small Hours 2"], project.Folder);
        Assert.Equal("Small Hours", project.Title);
        Assert.Equal(["Small Hours/draft.md"], temp.Tree("Small Hours"));
    }

    [Fact]
    public void An_empty_title_becomes_Untitled_story()
    {
        using var temp = new TempFolder();

        Assert.Equal("Untitled story", Project.Create(temp.Path, "   ").Title);
    }

    [Fact]
    public void Any_folder_opens_with_acts_chapters_scenes_and_pages_in_name_order()
    {
        using var temp = new TempFolder();
        temp.Write("Act One/Chapter 10/Return.md", "The ferry came back at dawn.");
        temp.Write("Act One/Chapter 2/Departure.md", "She left the lantern lit.");
        temp.Write("Act One/Chapter 2/Arrival.md", "Rain on the platform.");
        temp.Write("Epilogue/Later.md", "Years on, the tower stood.");
        temp.Write("Notes/Characters.md", "Mara: keeper of the lantern.");
        temp.Write("Research/Tides.md", "Spring tides follow the full moon.");
        temp.Write("Trash/Old scene.md", "Cut.");
        temp.Write(".git/HEAD", "ref: refs/heads/main");
        temp.Write("README.md", "Not part of the outline.");
        temp.Write("Act One/Chapter 2/cover.png", "");
        var before = temp.Tree().ToList();

        var project = Project.Open(temp.Path);

        Assert.Equal(
            ["Act One [Act]", "  Chapter 2 [Chapter]", "    Arrival [Scene]", "    Departure [Scene]", "  Chapter 10 [Chapter]", "    Return [Scene]",
             "Epilogue [Chapter]", "  Later [Scene]",
             "Notes [Notes]", "  Characters [Page]", "Research [Research]", "  Tides [Page]"],
            Outline(project));
        Assert.Equal("Rain on the platform.", project.Scenes.First().Text);
        Assert.Equal(before, temp.Tree());
    }

    [Fact]
    public void The_index_sets_the_order_and_unlisted_files_follow_in_name_order()
    {
        using var temp = new TempFolder();
        temp.Write("Chapter 1/Alpha.md");
        temp.Write("Chapter 1/Beta.md");
        temp.Write("Chapter 1/Gamma.md");
        temp.Write("Chapter 1/Delta.md");
        temp.Write("Chapter 2/One.md");
        temp.Write("hedgequill.json", """
            {
              // Edited by hand, so comments and a trailing comma are fine.
              "format": 2,
              "order": {
                "": ["Chapter 2", "Chapter 1", "Notes", "Research"],
                "Chapter 1": ["gamma.md", "Gone.md", "Alpha.md",],
              },
            }
            """);

        var project = Project.Open(temp.Path);

        Assert.Equal(
            ["Chapter 2 [Chapter]", "  One [Scene]", "Chapter 1 [Chapter]", "  Gamma [Scene]", "  Alpha [Scene]", "  Beta [Scene]", "  Delta [Scene]",
             "Notes [Notes]", "Research [Research]"],
            Outline(project));
    }

    [Fact]
    public void An_empty_act_stays_an_act_and_an_empty_folder_is_a_chapter()
    {
        using var temp = new TempFolder();
        temp.Folder("Act Two");
        temp.Folder("Interlude");
        temp.Write("hedgequill.json", """{ "format": 1, "acts": ["Act Two"] }""");

        Assert.Equal(["Act Two [Act]", "Interlude [Chapter]", "Notes [Notes]", "Research [Research]"], Outline(Project.Open(temp.Path)));
    }

    [Fact]
    public void A_damaged_index_costs_only_the_order()
    {
        using var temp = new TempFolder();
        temp.Write("Chapter 1/Scene 1.md", "Still here.");
        temp.Write("hedgequill.json", "{ \"order\": ");

        var project = Project.Open(temp.Path);

        Assert.Equal("Still here.", project.Scenes.Single().Text);
    }

    [Fact]
    public void A_project_from_a_newer_Hedgequill_doesnt_open()
    {
        using var temp = new TempFolder();
        temp.Write("hedgequill.json", """{ "format": 3 }""");

        var error = Assert.Throws<InvalidDataException>(() => Project.Open(temp.Path));
        Assert.Contains("newer version of Hedgequill", error.Message);
    }

    [Fact]
    public void A_missing_folder_doesnt_open()
    {
        using var temp = new TempFolder();

        Assert.Throws<DirectoryNotFoundException>(() => Project.Open(temp["Gone"]));
    }

    [Fact]
    public void Summaries_and_notes_live_in_the_index_and_follow_their_chapter()
    {
        using var temp = new TempFolder();
        var project = Project.Create(temp.Path, "Tides");
        var chapter = project.Root.Children.First(node => node.Kind == NodeKind.Chapter);
        project.Root.Summary = "A keeper and a lantern.";
        chapter.Summary = "The fog comes in.";
        chapter.EndNotes = "Thanks for *reading*.";
        project.Rename(chapter, "The Fog");

        var reopened = Project.Open(project.Folder);
        var fog = reopened.Root.Children.Single(node => node.Title == "The Fog");
        Assert.Equal(("A keeper and a lantern.", "The fog comes in.", "", "Thanks for *reading*."), (reopened.Root.Summary, fog.Summary, fog.BeginningNotes, fog.EndNotes));

        var copy = reopened.Duplicate(fog);
        Assert.Equal("The fog comes in.", copy.Summary);

        var deleted = reopened.Delete(fog);
        reopened.Restore(deleted);
        Assert.Equal("The fog comes in.", Project.Open(project.Folder).Root.Children.Single(node => node.Title == "The Fog").Summary);
    }

    [Fact]
    public void A_chapter_edited_since_it_was_copied_is_marked_until_it_matches_again()
    {
        using var temp = new TempFolder();
        temp.Write("Chapter 1/Fog.md", "The fog came in.");
        var project = Project.Open(temp.Path);
        var chapter = project.Root.Children.Single(node => node.Kind == NodeKind.Chapter);
        Assert.False(Project.ChangedSinceCopied(chapter));

        project.MarkCopied(chapter, Ao3Text.ChapterHtml(chapter.Children.Select(scene => scene.Text)));
        Assert.False(Project.ChangedSinceCopied(chapter));

        chapter.Children[0].Text = "The fog came in fast.";
        Assert.True(Project.ChangedSinceCopied(chapter));

        var reopened = Project.Open(temp.Path);
        Assert.False(Project.ChangedSinceCopied(reopened.Root.Children.Single(node => node.Kind == NodeKind.Chapter)));

        chapter.Children[0].Text = "The fog came in.";
        Assert.False(Project.ChangedSinceCopied(chapter));
    }

    [Fact]
    public void The_project_dictionary_lives_in_the_index_and_outlasts_other_edits()
    {
        using var temp = new TempFolder();
        var project = Project.Create(temp.Path, "Tides");

        project.AddToDictionary("Okafor");
        project.AddToDictionary("Ines");
        project.AddToDictionary("okafor");
        project.Rename(project.Scenes.Single(), "Harbour");

        var reopened = Project.Open(project.Folder);
        Assert.True(reopened.Knows("Okafor"));
        Assert.True(reopened.Knows("OKAFOR"));
        Assert.False(reopened.Knows("Mara"));
        Assert.Contains("\"dictionary\": [\n    \"Ines\",\n    \"Okafor\"\n  ]", File.ReadAllText(Path.Combine(project.Folder, Project.IndexFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_index_records_order_acts_and_only_the_titles_that_differ()
    {
        using var temp = new TempFolder();
        temp.Write("Act I/Chapter 1/Scene 1.md");
        temp.Write("Act I/Chapter 1/Scène 2.md");
        temp.Write("Prologue/Opening.md");
        temp.Write("Notes/Map.md");
        temp.Write("hedgequill.json", """
            { "format": 1, "order": { "": ["Prologue", "Act I"], "Act I/Chapter 1": ["Scène 2.md", "Scene 1.md"] },
              "titles": { "Prologue/Opening.md": "Opening: the letter" } }
            """);

        Project.Open(temp.Path).SaveIndex();

        Assert.Equal(
            """
            {
              "format": 2,
              "order": {
                "": [
                  "Prologue",
                  "Act I",
                  "Notes",
                  "Research"
                ],
                "Prologue": [
                  "Opening.md"
                ],
                "Act I": [
                  "Chapter 1"
                ],
                "Act I/Chapter 1": [
                  "Scène 2.md",
                  "Scene 1.md"
                ],
                "Notes": [
                  "Map.md"
                ]
              },
              "acts": [
                "Act I"
              ],
              "titles": {
                "Prologue/Opening.md": "Opening: the letter"
              }
            }

            """.ReplaceLineEndings("\n"),
            File.ReadAllText(temp["hedgequill.json"]));
    }

    [Fact]
    public void Words_counts_every_scene()
    {
        using var temp = new TempFolder();
        temp.Write("Chapter 1/Scene 1.md", "She *ran*.\n\n---\n\nThe end — finally.");
        temp.Write("Chapter 2/Scene 1.md", "Two words");
        temp.Write("Notes/Ideas.md", "Pages don't count toward the story.");

        Assert.Equal(7, Project.Open(temp.Path).Words);
    }

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
