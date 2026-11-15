using Hedgequill.Desktop.Spelling;

namespace Hedgequill.Tests.Desktop;

public class SpellCheckerTests
{
    private const string Line = "The keeper saw teh boat, and *Wren* waved.";

    [Fact]
    public void The_system_checker_flags_a_misspelling_and_suggests_the_word()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), "Only Windows and macOS have a system checker Hedgequill uses.");
        var checker = SpellChecker.Create();

        Assert.NotNull(checker);
        Assert.Equal([new Misspelling(Line.IndexOf("teh", StringComparison.Ordinal), 3)], checker.Check(Line));
        Assert.Contains("the", checker.Suggest("teh"));
        Assert.Empty(checker.Check("The keeper saw the boat."));
        Assert.Empty(checker.Check(""));
    }
}
