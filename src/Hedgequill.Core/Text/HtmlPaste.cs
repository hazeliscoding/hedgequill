using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Hedgequill.Core.Text;

/// <summary>
/// HTML from the clipboard, as Google Docs, Word or a browser put it there, read as Markdown. Italics and bold become
/// markers, paragraphs and line breaks stay, and everything else comes in as its text: headings, lists and tables lose
/// their shape but keep their words. A * or ` in the text is escaped, so it can't turn into a marker.
/// </summary>
public static partial class HtmlPaste
{
    private static readonly HashSet<string> Blocks =
        ["p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol", "dl", "dt", "dd", "blockquote", "pre", "table", "tr", "section", "article", "header", "footer", "hr"];

    private static readonly HashSet<string> Hidden = ["head", "style", "script", "title", "template"];

    private static readonly HashSet<string> Void = ["br", "hr", "img", "meta", "link", "input", "col", "wbr", "area", "base", "source"];

    public static string ToMarkdown(string html)
    {
        var paragraphs = new List<string>();
        var runs = new List<Run>();
        var open = new List<(string Name, bool Bold, bool Italic)>();
        var hidden = 0;
        for (var i = 0; i < html.Length;)
        {
            if (html[i] != '<')
            {
                var next = html.IndexOf('<', i) is var lt and >= 0 ? lt : html.Length;
                if (hidden == 0)
                {
                    var (bold, italic) = open.Count > 0 ? (open[^1].Bold, open[^1].Italic) : (false, false);
                    runs.Add(new Run(Collapse(WebUtility.HtmlDecode(html[i..next])), bold, italic));
                }

                i = next;
                continue;
            }

            // A comment, Word's conditional ones among them, and Word's list bullets, which it hides from all but itself.
            if (Skip(html, i, "<!--", "-->") is { } afterComment)
            {
                i = afterComment;
                continue;
            }

            if (Skip(html, i, "<![if", "<![endif]>") is { } afterBullet)
            {
                i = afterBullet;
                continue;
            }

            var close = TagEnd(html, i);
            if (close < 0)
            {
                runs.Add(new Run("<", false, false));
                i++;
                continue;
            }

            var (name, closing, style) = Tag(html[(i + 1)..close]);
            i = close + 1;
            if (name.Length == 0)
            {
                continue;
            }

            if (Hidden.Contains(name))
            {
                hidden = Math.Max(0, hidden + (closing ? -1 : 1));
                continue;
            }

            if (hidden > 0)
            {
                continue;
            }

            if (closing)
            {
                var at = open.FindLastIndex(tag => tag.Name == name);
                if (at >= 0)
                {
                    open.RemoveRange(at, open.Count - at);
                }

                if (Blocks.Contains(name))
                {
                    Paragraph(runs, paragraphs);
                }

                continue;
            }

            if (name == "br")
            {
                runs.Add(new Run("\n", false, false));
                continue;
            }

            if (Blocks.Contains(name))
            {
                Paragraph(runs, paragraphs);
            }

            if (!Void.Contains(name))
            {
                var (bold, italic) = open.Count > 0 ? (open[^1].Bold, open[^1].Italic) : (false, false);
                open.Add((name, Bold(name, style, bold), Italic(name, style, italic)));
            }
        }

        Paragraph(runs, paragraphs);
        return string.Join("\n\n", paragraphs);
    }

    private sealed record Run(string Text, bool Bold, bool Italic);

    // The runs so far as one paragraph of Markdown: runs of one style merge, and the markers hug the words, never spaces.
    private static void Paragraph(List<Run> runs, List<string> paragraphs)
    {
        var plain = string.Concat(runs.Select(run => run.Text)).Trim();
        if (plain.Length == 0)
        {
            runs.Clear();
            return;
        }

        if (Markdown.IsSceneBreak(plain))
        {
            paragraphs.Add(plain);
            runs.Clear();
            return;
        }

        var merged = new List<Run>();
        foreach (var run in runs.Where(run => run.Text.Length > 0))
        {
            if (merged.Count > 0 && run.Text != "\n" && merged[^1].Text != "\n" && (merged[^1].Bold, merged[^1].Italic) == (run.Bold, run.Italic))
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + run.Text };
            }
            else
            {
                merged.Add(run);
            }
        }

        var text = new StringBuilder();
        foreach (var run in merged)
        {
            if (run.Text == "\n")
            {
                text.Length = text.ToString().TrimEnd(' ').Length;
                text.Append('\n');
                continue;
            }

            var words = run.Text.Trim(' ');
            var before = run.Text.StartsWith(' ') && text.Length > 0 && text[^1] is not (' ' or '\n');
            if (words.Length == 0)
            {
                text.Append(before ? " " : "");
                continue;
            }

            var marker = (run.Bold ? "**" : "") + (run.Italic ? "*" : "");
            text.Append(before ? " " : "").Append(marker).Append(Escape(words)).Append(marker).Append(run.Text.EndsWith(' ') ? " " : "");
        }

        paragraphs.Add(string.Join('\n', text.ToString().Split('\n').Select(line => line.Trim())).Trim('\n'));
        runs.Clear();
    }

    private static string Escape(string text) => text.Replace("*", "\\*", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);

    private static string Collapse(string text) => Whitespace().Replace(text.Replace(' ', ' '), " ");

    private static int? Skip(string html, int at, string start, string end)
    {
        if (string.CompareOrdinal(html, at, start, 0, start.Length) != 0)
        {
            return null;
        }

        var close = html.IndexOf(end, at + start.Length, StringComparison.Ordinal);
        return close < 0 ? html.Length : close + end.Length;
    }

    // The > that ends the tag opening here, past any quoted attribute.
    private static int TagEnd(string html, int at)
    {
        var quote = '\0';
        for (var i = at + 1; i < html.Length; i++)
        {
            var c = html[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static (string Name, bool Closing, string Style) Tag(string inside)
    {
        var closing = inside.StartsWith('/');
        var name = new string(inside.AsSpan(closing ? 1 : 0).ToString().TakeWhile(c => char.IsLetterOrDigit(c) || c is ':' or '-').ToArray()).ToLowerInvariant();
        var style = StyleAttribute().Match(inside) is { Success: true } match ? match.Groups["value"].Value : "";
        return (name, closing, style);
    }

    private static bool Bold(string name, string style, bool inherited) => Property(style, "font-weight") switch
    {
        "bold" or "bolder" => true,
        "normal" or "lighter" => false,
        { } weight when int.TryParse(weight, NumberStyles.None, CultureInfo.InvariantCulture, out var value) => value >= 600,
        _ => name is "b" or "strong" || inherited,
    };

    private static bool Italic(string name, string style, bool inherited) => Property(style, "font-style") switch
    {
        "italic" or "oblique" => true,
        "normal" => false,
        _ => name is "i" or "em" || inherited,
    };

    // One property of an inline style, by its exact name, so Word's mso-bidi-font-style never counts as font-style.
    private static string? Property(string style, string name) =>
        style.Split(';').Select(declaration => declaration.Split(':', 2)).Where(pair => pair.Length == 2 && pair[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair[1].Trim().ToLowerInvariant()).LastOrDefault();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\bstyle\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();
}
