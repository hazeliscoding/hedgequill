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
    [InlineData("She **[ran]** home.", "**", "She |ran home.")]
    [InlineData("She *[ran]* home.", "*", "She |ran home.")]
    [InlineData("Line one.\n*[Two]*", "*", "Line one.\n|Two")]
    [InlineData("She **[ran]** home.", "*", "She ***ran|*** home.")]
    [InlineData("She *[ran]* home.", "**", "She ***ran|*** home.")]
    [InlineData("She **[ran home]**.", "**", "She |ran home.")]
    [InlineData("She **r[an]** home.", "**", "She **r**an|**** home.")]
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

    // "|" marks the caret, before and after.
    [Theory]
    [InlineData("- Fog|", "- Fog\n- |")]
    [InlineData("- Fo|g", "- Fo\n- |g")]
    [InlineData("* Fog|", "* Fog\n* |")]
    [InlineData("  - Fog|", "  - Fog\n  - |")]
    [InlineData("1. Fog|", "1. Fog\n2. |")]
    [InlineData("9)  Fog|", "9)  Fog\n10)  |")]
    [InlineData("- [x] Fog|", "- [x] Fog\n- [ ] |")]
    [InlineData("- Fog\n- |", "- Fog\n\n|")]
    [InlineData("- Fog\n- [ ] |\n- Rain", "- Fog\n\n|\n- Rain")]
    public void Enter_continues_a_list_and_ends_it_on_an_empty_item(string before, string after) =>
        Assert.Equal(after, Key(before, Markdown.Enter));

    [Theory]
    [InlineData("Fog|")]
    [InlineData("|- Fog")]
    [InlineData("- - -|")]
    [InlineData("```\n- Fog|\n```")]
    [InlineData("-Fog|")]
    public void Enter_anywhere_else_is_a_plain_line_break(string before) => Assert.Equal("none", Key(before, Markdown.Enter));

    [Theory]
    [InlineData("- Fog|", false, "  - Fog|")]
    [InlineData("1. [ ] Fo|g", false, "  1. [ ] Fo|g")]
    [InlineData("  - Fog|", true, "- Fog|")]
    [InlineData(" - Fog|", true, "- Fog|")]
    [InlineData("\t- Fog|", true, "- Fog|")]
    [InlineData("- Fog|", true, "- Fog|")]
    public void Tab_and_Shift_Tab_nest_a_list_item(string before, bool outward, string after) =>
        Assert.Equal(after, Key(before, (text, caret) => Markdown.Nest(text, caret, outward)));

    [Fact]
    public void Tab_outside_a_list_is_left_to_the_editor() => Assert.Equal("none", Key("Fog|", (text, caret) => Markdown.Nest(text, caret, false)));

    [Theory]
    [InlineData("- Fog", 0, 2, 0, null, null, 0)]
    [InlineData("    12. Fog", 4, 4, 0, null, 12, 2)]
    [InlineData("\t- [X] Fog", 1, 2, 4, true, null, 1)]
    [InlineData("+ [ ]", 0, 2, 3, false, null, 0)]
    public void A_list_items_parts_are_found(string line, int indent, int marker, int box, bool? isChecked, int? number, int level) =>
        Assert.Equal(new ListItem(indent, marker, box, isChecked, number, level), Markdown.ListItemAt(line));

    [Theory]
    [InlineData("-Fog")]
    [InlineData("* * *")]
    [InlineData("**Fog** rolled in")]
    [InlineData("1.5 knots")]
    public void Not_every_dash_or_number_starts_a_list(string line) => Assert.Null(Markdown.ListItemAt(line));

    [Fact]
    public void Fences_and_tables_are_told_by_their_neighbours()
    {
        string[] lines = ["Fog", "````js", "- not a list", "", "```", "````", "| A | B |", "|---|:-:|", "| 1 | 2 |", "| 3 |", "after", "| lone |", "~~~", "```", "~~~"];

        Assert.Equal(
            [LineKind.Text, LineKind.Fence, LineKind.Code, LineKind.Code, LineKind.Code, LineKind.Fence, LineKind.TableHeader, LineKind.TableRule, LineKind.TableRow, LineKind.TableRow, LineKind.Text, LineKind.Text, LineKind.Fence, LineKind.Code, LineKind.Fence],
            Markdown.Kinds(lines));
    }

    [Fact]
    public void A_comment_can_run_over_several_lines_but_not_out_of_code()
    {
        string[] lines = ["Fog <!-- check", "the tides", "and ferries --> Rain", "```", "<!-- code", "```", "after"];

        Assert.Equal(
            [LineKind.Text, LineKind.Comment, LineKind.CommentEnd, LineKind.Fence, LineKind.Code, LineKind.Fence, LineKind.Text],
            Markdown.Kinds(lines));
    }

    [Theory]
    [InlineData("Fog <!-- a --> and <!-- b --> rain", false, "Fog  and  rain")]
    [InlineData("Fog <!-- runs on", false, "Fog ")]
    [InlineData("still a note --> Rain", true, " Rain")]
    [InlineData("all note", true, "")]
    public void Comments_leave_a_line_with_only_its_text(string line, bool open, string kept) =>
        Assert.Equal(kept, Markdown.WithoutComments(line, open));

    [Theory]
    [InlineData("She [ran] home.", "She <!-- ran| --> home.")]
    [InlineData("She []ran.", "She <!-- | -->ran.")]
    public void Add_comment_wraps_the_selection(string before, string after) =>
        Assert.Equal(after, Apply(before, Markdown.Comment));

    [Fact]
    public void An_unclosed_fence_runs_to_the_end() => Assert.Equal([LineKind.Fence, LineKind.Code, LineKind.Code], Markdown.Kinds(["```", "| A |", "|---|"]));

    [Fact]
    public void A_tables_columns_are_as_wide_as_their_widest_cell() =>
        Assert.Equal([13, 12], Markdown.Columns(["| Name | Lives |", "|---|---|", "| Mrs. Okafor | downstairs |"]));

    [Theory]
    [InlineData("| Mira | the flat |", "[ Mira ][ the flat ]")]
    [InlineData("| a \\| b | c", "[ a \\| b ][ c]")]
    [InlineData("|  |", "[  ]")]
    public void A_rows_cells_are_between_its_pipes(string line, string cells) =>
        Assert.Equal(cells, string.Concat(Markdown.Cells(line).Select(cell => $"[{cell}]")));

    private static string Key(string marked, Func<string, int, Change?> edit)
    {
        var caret = marked.IndexOf('|', StringComparison.Ordinal);
        var text = marked.Remove(caret, 1);
        return edit(text, caret) is { } change ? change.ApplyTo(text).Insert(change.Caret, "|") : "none";
    }

    private static string Apply(string marked, Func<string, int, int, Change> edit)
    {
        var start = marked.IndexOf('[', StringComparison.Ordinal);
        var end = marked.IndexOf(']', StringComparison.Ordinal) - 1;
        var text = marked.Remove(end + 1, 1).Remove(start, 1);
        var change = edit(text, start, end);
        return change.ApplyTo(text).Insert(change.Caret, "|");
    }
}
