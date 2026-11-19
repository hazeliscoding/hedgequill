using Hedgequill.Core.Text;
using Hedgequill.Tests.Guards;

namespace Hedgequill.Tests.Core;

public class HtmlPasteTests
{
    // Invented text exactly as each app put it on the clipboard on Windows, saved by scripts/save_clipboard_html.ps1. Word's
    // file links held the owner's user name, swapped for one of the same length so the header's offsets still hold.
    [Theory]
    [InlineData("word")]
    public void Text_copied_from_an_app_pastes_with_its_italics_and_bold(string app)
    {
        var html = ClipboardHtml.Unwrap(File.ReadAllBytes(System.IO.Path.Combine(Repo.Root, $"tests/Hedgequill.Tests/Fixtures/Paste/{app}.cfhtml")));

        Assert.Equal(Repo.Read($"tests/Hedgequill.Tests/Fixtures/Paste/{app}.md"), HtmlPaste.ToMarkdown(html) + "\n");
    }

    [Theory]
    // Google Docs wraps a copy in a bold that isn't, and marks real bold and italics with styles.
    [InlineData("<b style=\"font-weight:normal;\" id=\"docs-internal-guid-1\"><p><span style=\"font-weight:400\">The fog </span><span style=\"font-style:italic\">never</span><span style=\"font-weight:700\"> lifted</span>.</p></b>", "The fog *never* **lifted**.")]
    [InlineData("<p>One<br>two</p><p></p><p>three</p>", "One\ntwo\n\nthree")]
    [InlineData("<p>2 * 3 is `six`</p>", "2 \\* 3 is \\`six\\`")]
    [InlineData("<p>* * *</p>", "* * *")]
    [InlineData("<p>Fish &amp; chips&nbsp;&lt;here&gt;</p>", "Fish & chips <here>")]
    [InlineData("<p>caf&#233; &#x2014; Theo&rsquo;s &bogus; &#xD800;</p>", "café — Theo’s &bogus; &#xD800;")]
    [InlineData("<style>p { color: red }</style><!-- note --><p>Kept</p><script>var a = '<p>';</script>", "Kept")]
    [InlineData("<h2>Ferry</h2><ul><li>Rope</li><li><em>Lamp</em></li></ul>", "Ferry\n\nRope\n\n*Lamp*")]
    [InlineData("<i>a</i><i>b</i> and <strong><em>both</em></strong>", "*ab* and ***both***")]
    public void HTML_keeps_its_italics_bold_and_paragraphs_and_nothing_else(string html, string markdown) =>
        Assert.Equal(markdown, HtmlPaste.ToMarkdown(html));
}
