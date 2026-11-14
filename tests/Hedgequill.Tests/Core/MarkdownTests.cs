using Hedgequill.Core.Text;

namespace Hedgequill.Tests.Core;

public class MarkdownTests
{
    // "[" and "]" mark the selection in, "|" the caret out.
    [Theory]
    [InlineData("She [ran] home.", "**", "She **ran|** home.")]
    [InlineData("She [**ran**] home.", "**", "She |ran home.")]
    [InlineData("She [ran] home.", "*", "She *ran|* home.")]
    [InlineData("She [*ran*] home.", "*", "She |ran home.")]
    [InlineData("She ran[] home.", "*", "She ran*|* home.")]
    [InlineData("[]", "**", "**|**")]
    [InlineData("A [**] b", "**", "A ****|** b")]
    public void Bold_and_Italic_wrap_or_unwrap_the_selection(string before, string marker, string after) =>
        Assert.Equal(after, Apply(before, (text, start, end) => Markdown.Wrap(text, start, end, marker)));

    [Theory]
    [InlineData("The ferry left.[]", "The ferry left.\n\n---\n\n|")]
    [InlineData("The ferry left.\n\n[]\n\nMorning came.", "The ferry left.\n\n---\n\n|Morning came.")]
    [InlineData("The ferry left.[ Then nothing.]\nMorning came.", "The ferry left.\n\n---\n\n|Morning came.")]
    [InlineData("[]Morning came.", "---\n\n|Morning came.")]
    [InlineData("Windows lines.\r\n\r\n[]", "Windows lines.\n\n---\n\n|")]
    public void A_scene_break_sits_on_its_own_line_between_blank_lines(string before, string after) =>
        Assert.Equal(after, Apply(before, Markdown.InsertSceneBreak));

    private static string Apply(string marked, Func<string, int, int, Change> edit)
    {
        var start = marked.IndexOf('[', StringComparison.Ordinal);
        var end = marked.IndexOf(']', StringComparison.Ordinal) - 1;
        var text = marked.Remove(end + 1, 1).Remove(start, 1);
        var change = edit(text, start, end);
        return change.ApplyTo(text).Insert(change.Caret, "|");
    }
}
