using Hedgequill.Core.Text;

namespace Hedgequill.Tests.Core;

public class MarkdownStyleTests
{
    [Theory]
    [InlineData("*Nobody* had told **Wren**.", "Marker[*] Emphasis[Nobody] Marker[*] Marker[**] Strong[Wren] Marker[**]")]
    [InlineData("_quiet_ and __loud__", "Marker[_] Emphasis[quiet] Marker[_] Marker[__] Strong[loud] Marker[__]")]
    [InlineData("***both***", "Marker[*] Emphasis[**both**] Marker[**] Strong[both] Marker[**] Marker[*]")]
    [InlineData("**bold *and* bold**", "Marker[**] Strong[bold *and* bold] Marker[*] Emphasis[and] Marker[*] Marker[**]")]
    [InlineData("The *rain*, then *more* rain.", "Marker[*] Emphasis[rain] Marker[*] Marker[*] Emphasis[more] Marker[*]")]
    public void Emphasis_and_strong_are_styled_and_their_markers_faded(string line, string spans) =>
        Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("2 * 3 * 4")]
    [InlineData("snake_case_name")]
    [InlineData("*unclosed")]
    [InlineData("not closed*")]
    [InlineData("* not emphasis *")]
    public void Stray_markers_stay_plain(string line) => Assert.Equal("", Describe(line));

    [Fact]
    public void A_backslash_keeps_a_marker_literal_and_fades() =>
        Assert.Equal("Marker[\\] Marker[\\]", Describe("\\*not italic\\*"));

    [Theory]
    [InlineData("# The Tower", "Heading[# The Tower] Marker[# ]")]
    [InlineData("### A *quiet* hour", "Heading[### A *quiet* hour] Marker[### ] Marker[*] Emphasis[quiet] Marker[*]")]
    [InlineData("#", "Heading[#] Marker[#]")]
    [InlineData("#hashtag", "")]
    [InlineData("####### seven", "")]
    [InlineData("    # indented too far", "")]
    public void Headings_take_the_whole_line(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("> She said *no*.", "Quote[> She said *no*.] Marker[> ] Marker[*] Emphasis[no] Marker[*]")]
    [InlineData("> > Nested.", "Quote[> > Nested.] Marker[> > ]")]
    [InlineData(">Tight.", "Quote[>Tight.] Marker[>]")]
    public void Quotes_take_the_whole_line(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("---", true)]
    [InlineData("***", true)]
    [InlineData("___", true)]
    [InlineData("* * *", true)]
    [InlineData("****", true)]
    [InlineData("   -----  ", true)]
    [InlineData("--", false)]
    [InlineData("--- then", false)]
    [InlineData("-*-", false)]
    [InlineData("    ---", false)]
    [InlineData("———", false)]
    [InlineData("", false)]
    public void A_line_of_three_or_more_dashes_stars_or_underscores_is_a_scene_break(string line, bool isBreak)
    {
        Assert.Equal(isBreak, Markdown.IsSceneBreak(line));
        Assert.Equal(isBreak ? $"Marker[{line}]" : "", Describe(line));
    }

    [Fact]
    public void Spans_come_in_the_order_to_apply_them() =>
        Assert.Equal(
            [Mark.Quote, Mark.Emphasis, Mark.Marker, Mark.Marker, Mark.Marker],
            Markdown.Spans("> A *word*.").Select(span => span.Mark));

    // The spans in reading order, each with the text it covers.
    private static string Describe(string line) =>
        string.Join(' ', Markdown.Spans(line).OrderBy(span => span.Start).ThenBy(span => span.Mark).Select(span => $"{span.Mark}[{line.Substring(span.Start, span.Length)}]"));
}
