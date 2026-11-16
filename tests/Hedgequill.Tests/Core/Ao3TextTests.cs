using Hedgequill.Core.Ao3;
using Hedgequill.Tests.Guards;

namespace Hedgequill.Tests.Core;

public class Ao3TextTests
{
    // The word counts come from an independent copy of AO3's counter (otwarchive's lib/word_counter.rb) run on the
    // expected HTML, so this checks Hedgequill's count against AO3's rules, not against itself.
    [Theory]
    [InlineData("harbour", 92)]
    [InlineData("letters", 62)]
    [InlineData("kana", 30)]
    public void Invented_chapters_become_the_expected_AO3_HTML_and_word_count(string name, int words)
    {
        var markdown = Repo.Read($"tests/Hedgequill.Tests/Fixtures/Ao3/{name}.md");

        Assert.Equal(Repo.Read($"tests/Hedgequill.Tests/Fixtures/Ao3/{name}.html"), Ao3Text.Html(markdown) + "\n");
        Assert.Equal(words, Ao3Text.Words(markdown));
    }

    [Fact]
    public void A_chapter_puts_a_scene_break_between_its_scenes_and_skips_empty_ones() =>
        Assert.Equal("<p>Fog.</p>\n<hr />\n<p>Rain.</p>", Ao3Text.ChapterHtml(["Fog.", "", "  \n", "Rain."]));

    [Theory]
    [InlineData("# One", "<h2>One</h2>")]
    [InlineData("#### Four", "<h5>Four</h5>")]
    [InlineData("###### Six", "<h6>Six</h6>")]
    [InlineData("#", "")]
    public void Headings_sit_a_level_below_AO3s_chapter_title(string markdown, string html) =>
        Assert.Equal(html, Ao3Text.Html(markdown));

    [Theory]
    [InlineData("Line one\r\nLine two", "<p>Line one<br />Line two</p>")]
    [InlineData("<b>bold?</b>", "<p>&lt;b&gt;bold?&lt;/b&gt;</p>")]
    [InlineData("*a **b** c*", "<p><em>a <strong>b</strong> c</em></p>")]
    [InlineData("    indented", "<p>indented</p>")]
    [InlineData("1) First\n2) Second", "<ol><li>First</li><li>Second</li></ol>")]
    [InlineData("- One\n1. Two", "<ul><li>One</li></ul>\n<ol><li>Two</li></ol>")]
    [InlineData("> Said.\nNot quoted.", "<blockquote><p>Said.</p></blockquote>\n<p>Not quoted.</p>")]
    [InlineData("[link](ftp://example.org/file)", "<p><a href=\"ftp://example.org/file\">link</a></p>")]
    [InlineData("[quote](https://example.org/a\"b)", "<p><a href=\"https://example.org/a&quot;b\">quote</a></p>")]
    public void Converts_what_AO3_keeps_and_leaves_the_rest_as_text(string markdown, string html) =>
        Assert.Equal(html, Ao3Text.Html(markdown));

    [Theory]
    [InlineData("wait—no", 2)]
    [InlineData("one--two", 2)]
    [InlineData("don't", 1)]
    [InlineData("rowboat’s", 1)]
    [InlineData("well-worn", 1)]
    [InlineData("3.5 knots", 3)]
    [InlineData("ラーメン", 4)]
    [InlineData("", 0)]
    public void Counts_words_the_way_AO3_does(string text, int words) =>
        Assert.Equal(words, Ao3Text.CountWords(text));

    [Fact]
    public void A_word_split_by_emphasis_counts_twice_as_on_AO3() =>
        Assert.Equal(2, Ao3Text.Words("*wo*rd"));
}
