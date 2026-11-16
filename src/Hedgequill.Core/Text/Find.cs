using System.Text.RegularExpressions;

namespace Hedgequill.Core.Text;

/// <summary>Find in project: the words as typed, never a pattern, optionally matching case or whole words only.</summary>
public static class Find
{
    /// <summary>What to look for, or null when the query is empty.</summary>
    public static Regex? Pattern(string query, bool matchCase, bool wholeWords)
    {
        if (query.Length == 0)
        {
            return null;
        }

        var literal = Regex.Escape(query);
        // Lookarounds rather than \b, so a query that starts or ends with punctuation still finds whole words.
        var pattern = wholeWords ? $@"(?<!\w){literal}(?!\w)" : literal;
        return new Regex(pattern, matchCase ? RegexOptions.CultureInvariant : RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    /// <summary>Replaces every match with the replacement exactly as typed: "$1" stays "$1".</summary>
    public static string ReplaceAll(Regex pattern, string text, string replacement) => pattern.Replace(text, _ => replacement);
}
