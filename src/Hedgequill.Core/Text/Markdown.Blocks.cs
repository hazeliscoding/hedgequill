using System.Globalization;
using System.Text.RegularExpressions;

namespace Hedgequill.Core.Text;

/// <summary>What a line is where its neighbours decide: plain Markdown, a code fence or the code between fences, or part of a table.</summary>
public enum LineKind
{
    Text,
    Fence,
    Code,
    TableHeader,
    TableRule,
    TableRow,
}

/// <summary>
/// A list item's parts, as lengths from the line's start: its indent, its marker with the spaces after it, and a
/// checklist's box with the spaces after that. Level counts two spaces of indent, or a tab, as one step in.
/// </summary>
public sealed record ListItem(int Indent, int Marker, int Box, bool? Checked, int? Number, int Level)
{
    /// <summary>Where the item's text starts.</summary>
    public int Text => Indent + Marker + Box;
}

public static partial class Markdown
{
    /// <summary>A bullet (- * or +) or a number (with . or ) after it), then a space, then an optional box: [ ] or [x].</summary>
    public static ListItem? ListItemAt(string line)
    {
        if (IsSceneBreak(line) || ListPattern().Match(line) is not { Success: true } match)
        {
            return null;
        }

        var indent = match.Groups["indent"];
        var space = match.Groups["space"];
        var box = match.Groups["box"];
        var number = match.Groups["number"];
        return new ListItem(
            indent.Length,
            space.Index + space.Length - indent.Length,
            box.Success ? match.Length - box.Index : 0,
            box.Success ? box.Value[1] != ' ' : null,
            number.Success ? int.Parse(number.Value, CultureInfo.InvariantCulture) : null,
            indent.Value.Sum(c => c == '\t' ? 2 : 1) / 2);
    }

    /// <summary>
    /// Enter in a list item, after its marker: the next item, numbered one on and with an empty box if this one has a box,
    /// takes the rest of the line. Enter on an empty item ends the list, leaving a blank line. Null anywhere else.
    /// </summary>
    public static Change? Enter(string text, int caret)
    {
        if (ItemAround(text, caret) is not var (start, end, line, item) || caret < start + item.Text)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(line[item.Text..]))
        {
            return new Change(start, end - start, "\n", start + 1);
        }

        var marker = line.Substring(item.Indent, item.Marker);
        if (item.Number is { } number)
        {
            var digits = marker.TakeWhile(char.IsAsciiDigit).Count();
            marker = (number + 1).ToString(CultureInfo.InvariantCulture) + marker[digits..];
        }

        var next = "\n" + line[..item.Indent] + marker + (item.Checked is null ? "" : "[ ] ");
        return new Change(caret, 0, next, caret + next.Length);
    }

    /// <summary>Tab nests a list item under the one above with two more spaces before it; Shift+Tab takes up to two away. Null outside a list.</summary>
    public static Change? Nest(string text, int caret, bool outward)
    {
        if (ItemAround(text, caret) is not var (start, _, line, _))
        {
            return null;
        }

        if (!outward)
        {
            return new Change(start, 0, "  ", caret + 2);
        }

        var cut = line.StartsWith('\t') ? 1 : line.TakeWhile(c => c == ' ').Take(2).Count();
        return new Change(start, cut, "", Math.Max(start, caret - cut));
    }

    /// <summary>
    /// What each line is where its neighbours decide. A fence is three or more backticks or tildes, and the lines up to the
    /// next fence like it are code. A table, as GitHub's Markdown has it, is a row of cells between pipes, a rule row of dashes
    /// under it, and the rows below that start with a pipe.
    /// </summary>
    public static LineKind[] Kinds(IReadOnlyList<string> lines)
    {
        var kinds = new LineKind[lines.Count];
        string? fence = null;
        for (var i = 0; i < lines.Count; i++)
        {
            if (fence is not null)
            {
                var closes = Fence(lines[i]) is { } close && close[0] == fence[0] && close.Length >= fence.Length && lines[i].Trim().Length == close.Length;
                kinds[i] = closes ? LineKind.Fence : LineKind.Code;
                fence = closes ? null : fence;
            }
            else if (Fence(lines[i]) is { } open)
            {
                kinds[i] = LineKind.Fence;
                fence = open;
            }
            else if (IsTableRow(lines[i]) && i + 1 < lines.Count && IsTableRule(lines[i + 1]))
            {
                kinds[i] = LineKind.TableHeader;
                kinds[++i] = LineKind.TableRule;
                while (i + 1 < lines.Count && IsTableRow(lines[i + 1]))
                {
                    kinds[++i] = LineKind.TableRow;
                }
            }
        }

        return kinds;
    }

    /// <summary>Where a table row's pipes are, leaving out a pipe escaped with a backslash.</summary>
    public static List<int> Pipes(string line)
    {
        var pipes = new List<int>();
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\')
            {
                i++;
            }
            else if (line[i] == '|')
            {
                pipes.Add(i);
            }
        }

        return pipes;
    }

    /// <summary>A table row's cells, the text between its pipes, without a pipe at either end.</summary>
    public static List<string> Cells(string line)
    {
        var pipes = Pipes(line);
        var cells = new List<string>();
        for (var i = 0; i < pipes.Count; i++)
        {
            var end = i + 1 < pipes.Count ? pipes[i + 1] : line.Length;
            if (i + 1 < pipes.Count || line[(pipes[i] + 1)..].Trim().Length > 0)
            {
                cells.Add(line[(pipes[i] + 1)..end]);
            }
        }

        return cells;
    }

    /// <summary>A table's column widths in characters: for each gap between two pipes, the widest it is in any row.</summary>
    public static int[] Columns(IEnumerable<string> rows)
    {
        var widths = new List<int>();
        foreach (var pipes in rows.Select(Pipes))
        {
            for (var i = 0; i + 1 < pipes.Count; i++)
            {
                var width = pipes[i + 1] - pipes[i] - 1;
                if (i == widths.Count)
                {
                    widths.Add(width);
                }
                else
                {
                    widths[i] = Math.Max(widths[i], width);
                }
            }
        }

        return [.. widths];
    }

    // Up to three spaces, then three or more backticks or tildes; a backtick fence's info string has no backticks.
    private static string? Fence(string line)
    {
        var start = Indent(line);
        if (start > 3 || start >= line.Length || line[start] is not ('`' or '~'))
        {
            return null;
        }

        var end = start;
        while (end < line.Length && line[end] == line[start])
        {
            end++;
        }

        return end - start >= 3 && (line[start] == '~' || line.IndexOf('`', end) < 0) ? line[start..end] : null;
    }

    private static bool IsTableRow(string line) => line.TrimStart().StartsWith('|');

    private static bool IsTableRule(string line) =>
        IsTableRow(line) && line.Contains('-', StringComparison.Ordinal) && line.All(c => c is '|' or '-' or ':' or ' ' or '\t');

    // The list item holding the caret, if its line is plain Markdown rather than code or a table.
    private static (int Start, int End, string Line, ListItem Item)? ItemAround(string text, int caret)
    {
        var start = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var end = text.IndexOf('\n', caret) is var newline and >= 0 ? newline : text.Length;
        var line = text[start..end].TrimEnd('\r');
        if (ListItemAt(line) is not { } item)
        {
            return null;
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var number = text.AsSpan(0, start).Count('\n');
        return Kinds(lines)[number] == LineKind.Text ? (start, start + line.Length, line, item) : null;
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:[-*+]|(?<number>\d{1,9})[.)])(?<space>[ \t]+)(?:(?<box>\[[ xX]\])(?:[ \t]+|$))?")]
    private static partial Regex ListPattern();
}
