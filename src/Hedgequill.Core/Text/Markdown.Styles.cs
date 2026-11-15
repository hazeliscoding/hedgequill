namespace Hedgequill.Core.Text;

/// <summary>A style the editor gives part of a line, in the order it applies them, so a marker's fade wins over a quote's color.</summary>
public enum Mark
{
    Heading,
    Quote,
    Emphasis,
    Strong,
    Marker,
}

public readonly record struct Span(int Start, int Length, Mark Mark);

public static partial class Markdown
{
    /// <summary>Markdown's thematic break, which Hedgequill shows as a scene break: three or more of one of -, * or _, alone on a line.</summary>
    public static bool IsSceneBreak(ReadOnlySpan<char> line)
    {
        var indent = Indent(line);
        if (indent > 3)
        {
            return false;
        }

        var mark = '\0';
        var count = 0;
        foreach (var c in line[indent..])
        {
            if (c is ' ' or '\t')
            {
                continue;
            }

            if (c is not ('-' or '*' or '_') || (count > 0 && c != mark))
            {
                return false;
            }

            mark = c;
            count++;
        }

        return count >= 3;
    }

    /// <summary>
    /// How to style one line: a heading or quote takes the whole line, emphasis and strong go inside it, and the markers
    /// that make them fade. CommonMark's rules, line by line as the editor draws, so emphasis never spans lines.
    /// </summary>
    public static List<Span> Spans(string line)
    {
        var spans = new List<Span>();
        if (IsSceneBreak(line))
        {
            spans.Add(new(0, line.Length, Mark.Marker));
            return spans;
        }

        var body = HeadingPrefix(line);
        if (body > 0)
        {
            spans.Add(new(0, line.Length, Mark.Heading));
            spans.Add(new(0, body, Mark.Marker));
        }
        else if ((body = QuotePrefix(line)) > 0)
        {
            spans.Add(new(0, line.Length, Mark.Quote));
            spans.Add(new(0, body, Mark.Marker));
        }

        Emphasis(line, body, spans);
        spans.Sort((a, b) => a.Mark.CompareTo(b.Mark));
        return spans;
    }

    private static int Indent(ReadOnlySpan<char> line) => line.Length - line.TrimStart(' ').Length;

    // One to six #, then a space or the end of the line.
    private static int HeadingPrefix(string line)
    {
        var start = Indent(line);
        if (start > 3)
        {
            return 0;
        }

        var end = start;
        while (end < line.Length && line[end] == '#')
        {
            end++;
        }

        if (end - start is 0 or > 6 || (end < line.Length && line[end] is not (' ' or '\t')))
        {
            return 0;
        }

        while (end < line.Length && line[end] is ' ' or '\t')
        {
            end++;
        }

        return end;
    }

    // One or more >, each with an optional space after it.
    private static int QuotePrefix(string line)
    {
        var end = 0;
        while (true)
        {
            var at = end + Indent(line.AsSpan(end));
            if (at - end > 3 || at >= line.Length || line[at] != '>')
            {
                return end;
            }

            end = at + 1 < line.Length && line[at + 1] == ' ' ? at + 2 : at + 1;
        }
    }

    private sealed class Run(char marker, int start, int length, bool canOpen, bool canClose)
    {
        public char Marker { get; } = marker;
        public int Start { get; set; } = start;
        public int Length { get; set; } = length;
        public int Original { get; } = length;
        public bool CanOpen { get; } = canOpen;
        public bool CanClose { get; } = canClose;
    }

    // CommonMark's delimiter runs: a run opens when it leans on the word after it and closes when it leans on the word before.
    private static void Emphasis(string line, int from, List<Span> spans)
    {
        var runs = new List<Run>();
        for (var i = from; i < line.Length;)
        {
            var c = line[i];
            if (c == '\\' && i + 1 < line.Length && IsPunctuation(line[i + 1]) && line[i + 1] < 128)
            {
                spans.Add(new(i, 1, Mark.Marker));
                i += 2;
                continue;
            }

            if (c is not ('*' or '_'))
            {
                i++;
                continue;
            }

            var end = i;
            while (end < line.Length && line[end] == c)
            {
                end++;
            }

            var before = i > from ? line[i - 1] : ' ';
            var after = end < line.Length ? line[end] : ' ';
            var left = !char.IsWhiteSpace(after) && (!IsPunctuation(after) || char.IsWhiteSpace(before) || IsPunctuation(before));
            var right = !char.IsWhiteSpace(before) && (!IsPunctuation(before) || char.IsWhiteSpace(after) || IsPunctuation(after));
            runs.Add(c == '*'
                ? new Run(c, i, end - i, left, right)
                : new Run(c, i, end - i, left && (!right || IsPunctuation(before)), right && (!left || IsPunctuation(after))));
            i = end;
        }

        for (var closing = 0; closing < runs.Count; closing++)
        {
            var closer = runs[closing];
            while (closer.CanClose && closer.Length > 0)
            {
                var opening = closing - 1;
                while (opening >= 0 && !(runs[opening] is { Length: > 0, CanOpen: true } run && run.Marker == closer.Marker && !RuleOfThree(run, closer)))
                {
                    opening--;
                }

                if (opening < 0)
                {
                    break;
                }

                var opener = runs[opening];
                var use = opener.Length >= 2 && closer.Length >= 2 ? 2 : 1;
                var content = opener.Start + opener.Length;
                spans.Add(new(content - use, use, Mark.Marker));
                spans.Add(new(content, closer.Start - content, use == 2 ? Mark.Strong : Mark.Emphasis));
                spans.Add(new(closer.Start, use, Mark.Marker));
                opener.Length -= use;
                closer.Start += use;
                closer.Length -= use;
                for (var between = opening + 1; between < closing; between++)
                {
                    runs[between].Length = 0;
                }
            }
        }
    }

    // A run that can both open and close can't pair with one whose lengths sum to a multiple of 3, unless both are: "*a**b*" stays one emphasis.
    private static bool RuleOfThree(Run opener, Run closer) =>
        (opener.CanClose || closer.CanOpen) && (opener.Original + closer.Original) % 3 == 0 && !(opener.Original % 3 == 0 && closer.Original % 3 == 0);

    private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);
}
