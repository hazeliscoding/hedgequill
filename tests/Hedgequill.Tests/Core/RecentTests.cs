using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Tests.Core;

public class RecentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Opening_a_project_puts_it_first_once_with_its_word_count()
    {
        using var temp = new TempFolder();
        var recent = new RecentProjects(temp["settings"]);
        var first = Project.Create(temp["stories"], "First Light");
        var second = Project.Create(temp["stories"], "Second Wind");
        second.Scenes.Single().Text = "Three invented words";

        recent.Add(first, Now.AddHours(-2));
        recent.Add(second, Now.AddHours(-1));
        recent.Add(first, Now);

        Assert.Equal(
            [new RecentProject(first.Folder, "First Light", Now, 0), new RecentProject(second.Folder, "Second Wind", Now.AddHours(-1), 3)],
            recent.Load());
    }

    [Fact]
    public void The_same_folder_in_other_letter_case_is_one_entry()
    {
        using var temp = new TempFolder();
        var recent = new RecentProjects(temp["settings"]);
        var project = Project.Create(temp["stories"], "First Light");

        recent.Add(project, Now.AddHours(-1));
        recent.Add(Project.Open(project.Folder.ToUpperInvariant()), Now);

        Assert.Equal(project.Folder, recent.Load().Single().Folder, ignoreCase: true);
    }

    [Fact]
    public void Recent_keeps_the_last_eight()
    {
        using var temp = new TempFolder();
        var recent = new RecentProjects(temp["settings"]);
        for (var i = 1; i <= 10; i++)
        {
            recent.Add(Project.Create(temp["stories"], $"Story {i}"), Now.AddMinutes(i));
        }

        Assert.Equal(["Story 10", "Story 9", "Story 8", "Story 7", "Story 6", "Story 5", "Story 4", "Story 3"], recent.Load().Select(entry => entry.Title));
    }

    [Fact]
    public void A_missing_or_damaged_list_is_empty()
    {
        using var temp = new TempFolder();
        Assert.Empty(new RecentProjects(temp["settings"]).Load());

        temp.Write("settings/recent.json", "[{\"folder\":");
        Assert.Empty(new RecentProjects(temp["settings"]).Load());
    }

    [Fact]
    public void Appearance_is_remembered_and_starts_as_Match_system()
    {
        using var temp = new TempFolder();
        Assert.Equal(Appearance.System, new Preferences(temp["settings"]).Appearance);

        new Preferences(temp["settings"]).Appearance = Appearance.Dark;

        Assert.Equal(Appearance.Dark, new Preferences(temp["settings"]).Appearance);
        Assert.Contains("\"appearance\": \"Dark\"", File.ReadAllText(temp["settings/settings.json"]));

        temp.Write("settings/settings.json", "{ \"appearance\": ");
        Assert.Equal(Appearance.System, new Preferences(temp["settings"]).Appearance);
    }

    [Fact]
    public void Settings_live_in_a_Hedgequill_folder_under_local_app_data() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hedgequill"),
            RecentProjects.DefaultFolder);

    [Theory]
    [InlineData("2026-11-13T00:12", "Today, 00:12")]
    [InlineData("2026-11-12T23:59", "Yesterday")]
    [InlineData("2026-11-10T09:00", "Last Tuesday")]
    [InlineData("2026-11-07T09:00", "Last Saturday")]
    [InlineData("2026-11-06T09:00", "6 Nov")]
    [InlineData("2025-12-24T09:00", "24 Dec 2025")]
    public void Recent_says_when_like_the_prototype(string opened, string label) =>
        Assert.Equal(label, Labels.When(new DateTimeOffset(DateTime.Parse(opened, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero), Now));

    [Theory]
    [InlineData(0, "0 words")]
    [InlineData(1, "1 word")]
    [InlineData(1340, "1,340 words")]
    public void Recent_counts_words_like_the_prototype(int count, string label) =>
        Assert.Equal(label, Labels.Words(count));

    [Theory]
    [InlineData("", 0)]
    [InlineData("One", 1)]
    [InlineData("  *Two*   **words**  ", 2)]
    [InlineData("# Heading\n\nDon't stop — keep going.", 5)]
    [InlineData("---\n\n* * *\n\n> “Quoted,” she said.", 3)]
    public void Words_skip_markdown_markers(string text, int count) =>
        Assert.Equal(count, Words.Count(text));
}
