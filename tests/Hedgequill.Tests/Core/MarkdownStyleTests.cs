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
    [InlineData("then * not emphasis *")]
    public void Stray_markers_stay_plain(string line) => Assert.Equal("", Describe(line));

    [Theory]
    [InlineData("It was ~~never~~ always yours.", "Marker[~~] Strike[never] Marker[~~]")]
    [InlineData("~one~ and ~~~three~~~", "")]
    [InlineData("~~ not struck ~~", "")]
    public void Two_tildes_strike_through(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("See [the map](https://example.com/map).", "Marker[[] Link[the map] Marker[](https://example.com/map)]")]
    [InlineData("[a *b*](https://example.com/a_b_c)", "Marker[[] Link[a *b*] Marker[*] Emphasis[b] Marker[*] Marker[](https://example.com/a_b_c)]")]
    [InlineData("![the harbour](harbour.png)", "")]
    [InlineData("[no target]", "")]
    [InlineData("[spaced](not a link)", "")]
    public void A_link_fades_its_brackets_and_target(string line, string spans) => Assert.Equal(spans, Describe(line));

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

    [Theory]
    [InlineData("- Fog", "Marker[- ]")]
    [InlineData("  12. Fog", "Marker[12. ]")]
    [InlineData("- [ ] Fog", "Marker[- [ ] ]")]
    [InlineData("- [x] Fog", "Marker[- [x] ] Done[Fog]")]
    [InlineData("- *Fog*", "Marker[- ] Marker[*] Emphasis[Fog] Marker[*]")]
    public void A_list_items_marker_fades_and_a_ticked_items_text_is_done(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("Use `Platform Nine` here", "Marker[`] Code[Platform Nine] Marker[`]")]
    [InlineData("``a ` b``", "Marker[``] Code[a ` b] Marker[``]")]
    [InlineData("`*not emphasis*`", "Marker[`] Code[*not emphasis*] Marker[`]")]
    [InlineData("`unclosed", "")]
    public void A_code_span_wins_over_emphasis_inside_it(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Theory]
    [InlineData("| A | *b* |", LineKind.TableHeader, "Code[| A | *b* |] Marker[|] Strong[ A ] Marker[|] Strong[ *b* ] Marker[*] Emphasis[b] Marker[*] Marker[|]")]
    [InlineData("| 1 | 2 |", LineKind.TableRow, "Code[| 1 | 2 |] Marker[|] Marker[|] Marker[|]")]
    [InlineData("|---|:-:|", LineKind.TableRule, "Code[|---|:-:|] Marker[|---|:-:|]")]
    [InlineData("```js", LineKind.Fence, "Code[```js] Marker[```js]")]
    [InlineData("- *not* a list", LineKind.Code, "Code[- *not* a list]")]
    public void Code_and_tables_are_in_the_code_face(string line, LineKind kind, string spans) => Assert.Equal(spans, Describe(line, kind));

    [Theory]
    [InlineData("Fog. <!-- check the tide --> Rain.", "Marker[<!--] Comment[ check the tide ] Marker[-->]")]
    [InlineData("Fog. <!-- runs on", "Marker[<!--] Comment[ runs on]")]
    [InlineData("`<!-- code -->`", "Marker[`] Code[<!-- code -->] Marker[`]")]
    public void A_comment_to_yourself_is_quieter_and_its_markers_fade(string line, string spans) => Assert.Equal(spans, Describe(line));

    [Fact]
    public void A_comment_from_above_ends_at_its_marker() =>
        Assert.Equal("Comment[still the note ] Marker[-->] Marker[*] Emphasis[then] Marker[*]", Describe("still the note --> *then* the text", LineKind.CommentEnd));

    // The spans in reading order, each with the text it covers.
    private static string Describe(string line, LineKind kind = LineKind.Text) =>
        string.Join(' ', Markdown.Spans(line, kind).OrderBy(span => span.Start).ThenBy(span => span.Mark).Select(span => $"{span.Mark}[{line.Substring(span.Start, span.Length)}]"));
}
