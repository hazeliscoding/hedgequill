namespace Hedgequill.Desktop.Spelling;

/// <summary>A misspelled word: where it starts in the checked text, and its length.</summary>
public readonly record struct Misspelling(int Start, int Length);

/// <summary>
/// The system's spell checker, offline and in the writer's own languages and word list: Windows' Spell Checking API or
/// macOS's NSSpellChecker. Their native calls live in the two platform files and nowhere else; the network guard checks
/// every name those files pass.
/// </summary>
public abstract class SpellChecker
{
    /// <summary>The system's checker, or null where there's none, which leaves spell check off.</summary>
    public static SpellChecker? Create()
    {
        try
        {
            return OperatingSystem.IsWindows() ? WindowsSpellChecker.Open()
                : OperatingSystem.IsMacOS() ? MacSpellChecker.Open()
                : null;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or DllNotFoundException or EntryPointNotFoundException or InvalidCastException)
        {
            // A missing checker or language costs only the underlines, never the writing.
            return null;
        }
    }

    // Windows refuses an empty string.
    public IReadOnlyList<Misspelling> Check(string text) => string.IsNullOrWhiteSpace(text) ? [] : CheckText(text);

    public IReadOnlyList<string> Suggest(string word) => string.IsNullOrWhiteSpace(word) ? [] : SuggestFor(word);

    protected abstract IReadOnlyList<Misspelling> CheckText(string text);

    protected abstract IReadOnlyList<string> SuggestFor(string word);
}
