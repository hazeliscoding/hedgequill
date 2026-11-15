using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace Hedgequill.Desktop.Spelling;

/// <summary>Windows' Spell Checking API (spellcheck.h), through source-generated COM so it works under NativeAOT.</summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsSpellChecker : SpellChecker
{
    private static readonly Guid FactoryClass = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");
    private static readonly Guid FactoryInterface = new("8E018A9D-2415-4677-BF08-794EA61F94BB");
    private static readonly StrategyBasedComWrappers Wrappers = new();
    private const int InProcessServer = 1;

    private readonly ISpellChecker _checker;

    private WindowsSpellChecker(ISpellChecker checker) => _checker = checker;

    /// <summary>A checker for the writer's display language, else their regional format's, else US English.</summary>
    public static WindowsSpellChecker? Open()
    {
        Marshal.ThrowExceptionForHR(CoCreateInstance(FactoryClass, 0, InProcessServer, FactoryInterface, out var instance));
        var factory = (ISpellCheckerFactory)Wrappers.GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None);
        Marshal.Release(instance);
        foreach (var language in new[] { CultureInfo.CurrentUICulture.Name, CultureInfo.CurrentCulture.Name, "en-US" })
        {
            if (language.Length > 0 && factory.IsSupported(language) != 0)
            {
                return new WindowsSpellChecker(factory.CreateSpellChecker(language));
            }
        }

        return null;
    }

    protected override IReadOnlyList<Misspelling> CheckText(string text)
    {
        var found = new List<Misspelling>();
        var errors = _checker.Check(text);
        while (errors.Next(out var error) == 0 && error is not null)
        {
            found.Add(new((int)error.StartIndex(), (int)error.Length()));
        }

        return found;
    }

    protected override IReadOnlyList<string> SuggestFor(string word)
    {
        var suggestions = new List<string>();
        var list = _checker.Suggest(word);
        while (list.Next(1, out var item, out var fetched) == 0 && fetched == 1)
        {
            suggestions.Add(Marshal.PtrToStringUni(item) ?? string.Empty);
            Marshal.FreeCoTaskMem(item);
        }

        return suggestions;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid type, nint outer, int context, in Guid iid, out nint instance);

    // Each interface lists its methods in spellcheck.h's order up to the last one used, since COM calls by position.
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
    internal partial interface ISpellCheckerFactory
    {
        IEnumString SupportedLanguages();

        int IsSupported(string languageTag);

        ISpellChecker CreateSpellChecker(string languageTag);
    }

    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
    internal partial interface ISpellChecker
    {
        nint LanguageTag();

        IEnumSpellingError Check(string text);

        IEnumString Suggest(string word);
    }

    [GeneratedComInterface]
    [Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
    internal partial interface IEnumSpellingError
    {
        // S_FALSE, 1, once there are no more.
        [PreserveSig]
        int Next(out ISpellingError? error);
    }

    [GeneratedComInterface]
    [Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
    internal partial interface ISpellingError
    {
        uint StartIndex();

        uint Length();
    }

    [GeneratedComInterface]
    [Guid("00000101-0000-0000-C000-000000000046")]
    internal partial interface IEnumString
    {
        [PreserveSig]
        int Next(int count, out nint item, out int fetched);
    }
}
