using Hedgequill.Core.Files;

namespace Hedgequill.Tests.Core;

public class FileTests
{
    [Theory]
    [InlineData("What now?", "What now")]
    [InlineData("Act I: The Letter", "Act I The Letter")]
    [InlineData("  .hidden thoughts  ", "hidden thoughts")]
    [InlineData("Wait...", "Wait")]
    [InlineData("a/b\\c|d*e\"f<g>h", "abcdefgh")]
    [InlineData("tab\tand\nnewline", "tabandnewline")]
    [InlineData("CON", "CON_")]
    [InlineData("nul.notes", "nul.notes_")]
    [InlineData("Console", "Console")]
    [InlineData("???", "Untitled")]
    [InlineData("", "Untitled")]
    public void A_title_becomes_a_name_Windows_and_macOS_accept(string title, string name) =>
        Assert.Equal(name, FileNames.From(title, "Untitled"));

    [Fact]
    public void A_long_title_is_cut_to_80_characters_without_a_trailing_space()
    {
        var name = FileNames.From(new string('a', 79) + " and then some", "Untitled");

        Assert.Equal(new string('a', 79), name);
    }

    [Fact]
    public void Unique_counts_up_from_2_and_ignores_case()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Scene.md", "scene 2.md" };

        Assert.Equal("Scene 3.md", FileNames.Unique("Scene", ".md", taken.Contains));
        Assert.Equal("Other.md", FileNames.Unique("Other", ".md", taken.Contains));
    }

    [Fact]
    public void An_atomic_write_replaces_the_file_and_leaves_no_temporary_file()
    {
        using var temp = new TempFolder();
        temp.Write("Scene.md", "Old words.");

        AtomicFile.Write(temp["Scene.md"], "New words, with an é and a ’.");

        Assert.Equal("New words, with an é and a ’.", File.ReadAllText(temp["Scene.md"]));
        Assert.Equal(["Scene.md"], temp.Tree());
        Assert.NotEqual(0xEF, File.ReadAllBytes(temp["Scene.md"])[0]);
    }

    [Fact]
    public void A_failed_atomic_write_keeps_what_was_there_and_cleans_up()
    {
        using var temp = new TempFolder();
        temp.Write("Scene.md/inside.md", "Words in the way.");

        // Windows refuses with access denied, macOS with "is a directory".
        var error = Assert.ThrowsAny<Exception>(() => AtomicFile.Write(temp["Scene.md"], "New words."));
        Assert.True(error is IOException or UnauthorizedAccessException, error.ToString());

        Assert.Equal(["Scene.md/", "Scene.md/inside.md"], temp.Tree());
    }

    [Fact]
    public void An_atomic_write_into_a_missing_folder_fails_without_creating_it()
    {
        using var temp = new TempFolder();

        Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.Write(temp["Gone/Scene.md"], "Words."));
        Assert.Empty(temp.Tree());
    }
}
