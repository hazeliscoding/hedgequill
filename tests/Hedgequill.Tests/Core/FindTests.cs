using Hedgequill.Core.Text;

namespace Hedgequill.Tests.Core;

public sealed class FindTests
{
    private const string Text = "Theo waved. theo's coat, Theodore's hat. A (note) and $5.";

    [Theory]
    [InlineData("Theo", false, false, 3)]
    [InlineData("Theo", true, false, 2)]
    [InlineData("Theo", false, true, 2)]
    [InlineData("Theo", true, true, 1)]
    [InlineData("(note)", false, true, 1)]
    [InlineData("$5", false, false, 1)]
    [InlineData("", false, false, 0)]
    public void Finds_the_words_as_typed(string query, bool matchCase, bool wholeWords, int count) =>
        Assert.Equal(count, Find.Pattern(query, matchCase, wholeWords)?.Count(Text) ?? 0);

    [Fact]
    public void Replace_all_puts_in_the_replacement_exactly_as_typed()
    {
        var pattern = Find.Pattern("theo", matchCase: false, wholeWords: true)!;
        Assert.Equal("$1 waved. $1's coat, Theodore's hat. A (note) and $5.", Find.ReplaceAll(pattern, Text, "$1"));
    }
}
