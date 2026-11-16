using Hedgequill.Core.Ao3;

namespace Hedgequill.Core.Text;

public static class Words
{
    /// <summary>The count AO3 shows for this Markdown: its text as AO3 HTML, counted by AO3's rules, so markers and scene breaks don't count.</summary>
    public static int Count(string text) => Ao3Text.Words(text);
}
