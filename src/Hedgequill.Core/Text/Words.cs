namespace Hedgequill.Core.Text;

public static class Words
{
    /// <summary>Counts runs of non-space characters with at least one letter or digit, so Markdown markers and scene breaks don't count.</summary>
    public static int Count(string text)
    {
        var count = 0;
        var inWord = false;
        var hasLetter = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                count += inWord && hasLetter ? 1 : 0;
                inWord = hasLetter = false;
            }
            else
            {
                inWord = true;
                hasLetter |= char.IsLetterOrDigit(c);
            }
        }

        return count + (inWord && hasLetter ? 1 : 0);
    }
}
