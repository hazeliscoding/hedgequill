using System.Text;
using System.Text.RegularExpressions;
using Hedgequill.Core.Text;

namespace Hedgequill.Core.Ao3;

[Flags]
public enum Style
{
    None = 0,
    Emphasis = 1,
    Strong = 2,
    Strike = 4,
}

/// <summary>A run of text in one style, which becomes one text node in AO3 HTML. Link is its target inside a link.</summary>
public sealed record Run(string Text, Style Style, string? Link);

/// <summary>One line of a block; a block's lines are joined by line breaks.</summary>
public sealed record Line(IReadOnlyList<Run> Runs);

public abstract record Block;

public sealed record Paragraph(IReadOnlyList<Line> Lines) : Block;

/// <summary>A heading, at its AO3 level: # is h2, since AO3 sets the chapter title above it.</summary>
public sealed record Heading(int Level, Line Line) : Block;

public sealed record Quote(IReadOnlyList<Paragraph> Paragraphs) : Block;

public sealed record ListBlock(bool Ordered, int Start, IReadOnlyList<Paragraph> Items) : Block;

public sealed record SceneBreak : Block;

/// <summary>
/// Markdown read as AO3 keeps it: paragraphs, emphasis, strikethrough, headings, quotes, lists, links and scene breaks.
/// Anything else stays plain text, so the preview shows what gets posted. The HTML and the preview both draw from Read,
/// and Words counts it the way AO3 does.
/// </summary>
public static partial class Ao3Text
{
    /// <summary>The blocks of a scene, page, summary or note, read line by line as the editor styles them.</summary>
    public static IReadOnlyList<Block> Read(string markdown)
    {
        var blocks = new List<Block>();
        List<Line>? paragraph = null;
        List<Paragraph>? quote = null;
        List<Line>? quoted = null;
        (bool Ordered, int Start, List<Paragraph> Items)? list = null;

        void Close()
        {
            if (paragraph is { Count: > 0 })
            {
                blocks.Add(new Paragraph(paragraph));
            }

            if (quoted is { Count: > 0 })
            {
                quote!.Add(new Paragraph(quoted));
            }

            if (quote is { Count: > 0 })
            {
                blocks.Add(new Quote(quote));
            }

            if (list is { } items)
            {
                blocks.Add(new ListBlock(items.Ordered, items.Start, items.Items));
            }

            (paragraph, quote, quoted, list) = (null, null, null, null);
        }

        foreach (var line in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                Close();
            }
            else if (Markdown.IsSceneBreak(line))
            {
                Close();
                blocks.Add(new SceneBreak());
            }
            else if (Markdown.HeadingPrefix(line) is > 0 and var body)
            {
                Close();
                if (line[body..].Trim() is { Length: > 0 } title)
                {
                    blocks.Add(new Heading(Math.Min(line.TrimStart().TakeWhile(c => c == '#').Count() + 1, 6), Inline(title)));
                }
            }
            else if (Markdown.QuotePrefix(line) is > 0 and var start)
            {
                if (quote is null)
                {
                    Close();
                    (quote, quoted) = ([], []);
                }

                // A bare > inside a quote starts its next paragraph.
                if (line[start..].Trim() is { Length: > 0 } text)
                {
                    quoted!.Add(Inline(text));
                }
                else if (quoted!.Count > 0)
                {
                    quote.Add(new Paragraph(quoted));
                    quoted = [];
                }
            }
            else if (ListItem().Match(line) is { Success: true } item)
            {
                var ordered = item.Groups["number"].Success;
                if (list is not { } open || open.Ordered != ordered)
                {
                    Close();
                    list = (ordered, ordered ? int.Parse(item.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture) : 1, []);
                }

                list.Value.Items.Add(new Paragraph([Inline(item.Groups["text"].Value.Trim())]));
            }
            else
            {
                if (paragraph is null)
                {
                    Close();
                    paragraph = [];
                }

                paragraph.Add(Inline(line.Trim()));
            }
        }

        Close();
        return blocks;
    }

    /// <summary>AO3 HTML for a scene, page, summary or note: one block per line, since AO3 turns a line break inside a paragraph into another break.</summary>
    public static string Html(string markdown) => Html(Read(markdown));

    public static string Html(IReadOnlyList<Block> blocks) => string.Join("\n", blocks.Select(Html));

    /// <summary>AO3 HTML for a chapter: its scenes in order, with a scene break between them.</summary>
    public static string ChapterHtml(IEnumerable<string> scenes) =>
        string.Join("\n<hr />\n", scenes.Select(Html).Where(html => html.Length > 0));

    /// <summary>The word count AO3 shows for this text.</summary>
    public static int Words(string markdown) => Words(Read(markdown));

    public static int Words(IReadOnlyList<Block> blocks) => Lines(blocks).Sum(line => line.Runs.Sum(run => CountWords(run.Text)));

    /// <summary>
    /// AO3's count for one text node (otwarchive's lib/word_counter.rb): -- becomes an em dash, hyphens and apostrophes join a
    /// word, and Chinese, Japanese and Thai count a word per character.
    /// </summary>
    public static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var rune in text.Replace("--", "—", StringComparison.Ordinal).EnumerateRunes())
        {
            if (rune.Value is '\'' or '’' or '‘' or '-')
            {
                continue;
            }

            if (IsCountedByCharacter(rune.Value))
            {
                count++;
                inWord = false;
            }
            else if (IsWordCharacter(rune))
            {
                count += inWord ? 0 : 1;
                inWord = true;
            }
            else
            {
                inWord = false;
            }
        }

        return count;
    }

    private static IEnumerable<Line> Lines(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            IEnumerable<Line> lines = block switch
            {
                Paragraph paragraph => paragraph.Lines,
                Heading heading => [heading.Line],
                Quote quote => quote.Paragraphs.SelectMany(paragraph => paragraph.Lines),
                ListBlock list => list.Items.SelectMany(item => item.Lines),
                _ => [],
            };
            foreach (var line in lines)
            {
                yield return line;
            }
        }
    }

    // Ruby's [[:word:]]: letters, marks, decimal digits and connector punctuation.
    private static bool IsWordCharacter(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        System.Globalization.UnicodeCategory.UppercaseLetter or System.Globalization.UnicodeCategory.LowercaseLetter
            or System.Globalization.UnicodeCategory.TitlecaseLetter or System.Globalization.UnicodeCategory.ModifierLetter
            or System.Globalization.UnicodeCategory.OtherLetter or System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark
            or System.Globalization.UnicodeCategory.DecimalDigitNumber or System.Globalization.UnicodeCategory.ConnectorPunctuation => true,
        _ => false,
    };

    // AO3's CHARACTER_COUNT_SCRIPTS: Han, Hiragana, Katakana and Thai, by their Unicode blocks. .NET's regex has no script property.
    private static bool IsCountedByCharacter(int c) =>
        c is (>= 0x0E01 and <= 0x0E3A) or (>= 0x0E40 and <= 0x0E5B) // Thai
            or (>= 0x2E80 and <= 0x2E99) or (>= 0x2E9B and <= 0x2EF3) or (>= 0x2F00 and <= 0x2FD5) // Han radicals
            or 0x3005 or 0x3007 or (>= 0x3021 and <= 0x3029) or (>= 0x3038 and <= 0x303B)
            or (>= 0x3041 and <= 0x3096) or (>= 0x309D and <= 0x309F) // Hiragana
            or (>= 0x30A1 and <= 0x30FA) or (>= 0x30FD and <= 0x30FF) or (>= 0x31F0 and <= 0x31FF) // Katakana
            or (>= 0x32D0 and <= 0x32FE) or (>= 0x3300 and <= 0x3357) or (>= 0xFF66 and <= 0xFF6F) or (>= 0xFF71 and <= 0xFF9D)
            or (>= 0x3400 and <= 0x4DBF) or (>= 0x4E00 and <= 0x9FFF) or (>= 0xF900 and <= 0xFAD9) // Han
            or (>= 0x1B000 and <= 0x1B16F) // Kana supplement and extensions
            or (>= 0x20000 and <= 0x323AF); // Han extensions

    private static string Html(Block block) => block switch
    {
        Paragraph paragraph => $"<p>{Html(paragraph.Lines)}</p>",
        Heading heading => $"<h{heading.Level}>{Html(heading.Line)}</h{heading.Level}>",
        Quote quote => $"<blockquote>{string.Concat(quote.Paragraphs.Select(paragraph => $"<p>{Html(paragraph.Lines)}</p>"))}</blockquote>",
        ListBlock list => (list.Ordered ? list.Start == 1 ? "<ol>" : $"<ol start=\"{list.Start}\">" : "<ul>")
            + string.Concat(list.Items.Select(item => $"<li>{Html(item.Lines)}</li>"))
            + (list.Ordered ? "</ol>" : "</ul>"),
        _ => "<hr />",
    };

    private static string Html(IEnumerable<Line> lines) => string.Join("<br />", lines.Select(Html));

    // Tags open in the order their styles start and close as they end, so "*a **b** c*" nests as written.
    private static string Html(Line line)
    {
        var html = new StringBuilder();
        var open = new List<(string Tag, string Close)>();
        foreach (var run in line.Runs)
        {
            var wanted = Tags(run).ToList();
            var keep = open.FindIndex(tag => !wanted.Contains(tag));
            if (keep >= 0)
            {
                for (var i = open.Count - 1; i >= keep; i--)
                {
                    html.Append(open[i].Close);
                }

                open.RemoveRange(keep, open.Count - keep);
            }

            foreach (var tag in wanted.Where(tag => !open.Contains(tag)))
            {
                html.Append(tag.Tag);
                open.Add(tag);
            }

            html.Append(Escape(run.Text));
        }

        for (var i = open.Count - 1; i >= 0; i--)
        {
            html.Append(open[i].Close);
        }

        return html.ToString();
    }

    private static IEnumerable<(string Tag, string Close)> Tags(Run run)
    {
        if (run.Link is { } target)
        {
            yield return ($"<a href=\"{Escape(target).Replace("\"", "&quot;", StringComparison.Ordinal)}\">", "</a>");
        }

        if (run.Style.HasFlag(Style.Strong))
        {
            yield return ("<strong>", "</strong>");
        }

        if (run.Style.HasFlag(Style.Emphasis))
        {
            yield return ("<em>", "</em>");
        }

        if (run.Style.HasFlag(Style.Strike))
        {
            yield return ("<s>", "</s>");
        }
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    // One line's runs: the markers drop out, and each stretch of one style and link becomes a run.
    private static Line Inline(string text)
    {
        var styles = new Style[text.Length];
        var hidden = new bool[text.Length];
        var links = new string?[text.Length];
        foreach (var span in Markdown.Inline(text))
        {
            var style = span.Mark switch
            {
                Mark.Emphasis => Style.Emphasis,
                Mark.Strong => Style.Strong,
                Mark.Strike => Style.Strike,
                _ => Style.None,
            };
            var target = span.Mark == Mark.Link ? Target(text, span) : null;
            for (var i = span.Start; i < span.Start + span.Length; i++)
            {
                hidden[i] |= span.Mark == Mark.Marker;
                styles[i] |= style;
                links[i] ??= target;
            }
        }

        var runs = new List<Run>();
        var run = new StringBuilder();
        (Style Style, string? Link) current = (Style.None, null);
        for (var i = 0; i < text.Length; i++)
        {
            if (hidden[i])
            {
                continue;
            }

            if (run.Length > 0 && (styles[i], links[i]) != current)
            {
                runs.Add(new Run(run.ToString(), current.Style, current.Link));
                run.Clear();
            }

            current = (styles[i], links[i]);
            run.Append(text[i]);
        }

        if (run.Length > 0)
        {
            runs.Add(new Run(run.ToString(), current.Style, current.Link));
        }

        return new Line(runs);
    }

    // The target of a link, if AO3 keeps it: http, https, ftp, mailto or a relative address. Any other stays plain text.
    private static string? Target(string text, Span link)
    {
        var close = link.Start + link.Length;
        var target = text[(close + 2)..text.IndexOf(')', close + 2)];
        return Scheme().Match(target) is { Success: true } scheme && scheme.Groups[1].Value.ToLowerInvariant() is not ("http" or "https" or "ftp" or "mailto")
            ? null
            : target;
    }

    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9+.-]*):")]
    private static partial Regex Scheme();

    // - * or + then a space for a bullet; a number, . or ) and a space for a numbered item.
    [GeneratedRegex(@"^ {0,3}(?:[-*+]|(?<number>\d{1,9})[.)])[ \t]+(?<text>\S.*)$")]
    private static partial Regex ListItem();
}
