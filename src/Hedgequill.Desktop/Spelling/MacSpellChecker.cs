using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hedgequill.Desktop.Spelling;

/// <summary>macOS's NSSpellChecker, called through the Objective-C runtime, which works under NativeAOT.</summary>
[SupportedOSPlatform("macos")]
internal sealed unsafe partial class MacSpellChecker : SpellChecker
{
    private const string Runtime = "/usr/lib/libobjc.A.dylib";
    private static readonly nint NotFound = nint.MaxValue;

    private readonly nint _checker;
    private readonly nint _language;

    private MacSpellChecker(nint checker, nint language)
    {
        _checker = checker;
        _language = language;
    }

    /// <summary>
    /// A checker in the first language the writer chose under System Settings › Keyboard › Spelling. Left to guess, macOS
    /// reads the language off each line, and a short one can come back unchecked.
    /// </summary>
    public static MacSpellChecker? Open()
    {
        // The app has AppKit loaded already; a test without a window doesn't.
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
        var pool = objc_autoreleasePoolPush();
        try
        {
            var checker = Send(Class("NSSpellChecker"), Selector("sharedSpellChecker"));
            if (checker == 0)
            {
                return null;
            }

            var preferred = Send(checker, Selector("userPreferredLanguages"));
            var language = preferred != 0 && Send(preferred, Selector("count")) > 0
                ? Send(preferred, Selector("objectAtIndex:"), 0)
                : Send(checker, Selector("language"));
            return language == 0 ? null : new MacSpellChecker(checker, Send(language, Selector("retain")));
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    protected override IReadOnlyList<Misspelling> CheckText(string text)
    {
        var found = new List<Misspelling>();
        var pool = objc_autoreleasePoolPush();
        var native = ToNative(text);
        try
        {
            for (nint start = 0; start < text.Length;)
            {
                var range = CheckSpelling(_checker, Selector("checkSpellingOfString:startingAt:language:wrap:inSpellDocumentWithTag:wordCount:"), native, start, _language, 0, 0, 0);
                if (range.Location == NotFound || range.Location < start || range.Length == 0)
                {
                    break;
                }

                found.Add(new((int)range.Location, (int)range.Length));
                start = range.Location + range.Length;
            }
        }
        finally
        {
            Send(native, Selector("release"));
            objc_autoreleasePoolPop(pool);
        }

        return found;
    }

    protected override IReadOnlyList<string> SuggestFor(string word)
    {
        var suggestions = new List<string>();
        var pool = objc_autoreleasePoolPush();
        var native = ToNative(word);
        try
        {
            var guesses = Guesses(_checker, Selector("guessesForWordRange:inString:language:inSpellDocumentWithTag:"), new Range(0, word.Length), native, _language, 0);
            var count = guesses == 0 ? 0 : Send(guesses, Selector("count"));
            for (nint i = 0; i < count; i++)
            {
                suggestions.Add(ToManaged(Send(guesses, Selector("objectAtIndex:"), i)));
            }
        }
        finally
        {
            Send(native, Selector("release"));
            objc_autoreleasePoolPop(pool);
        }

        return suggestions;
    }

    private static nint ToNative(string text)
    {
        fixed (char* characters = text)
        {
            return Send(Send(Class("NSString"), Selector("alloc")), Selector("initWithCharacters:length:"), characters, text.Length);
        }
    }

    private static string ToManaged(nint native)
    {
        var length = Send(native, Selector("length"));
        var characters = new char[length];
        fixed (char* buffer = characters)
        {
            Send(native, Selector("getCharacters:range:"), buffer, new Range(0, length));
        }

        return new string(characters);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Range(nint Location, nint Length);

    [LibraryImport(Runtime, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Class(string name);

    [LibraryImport(Runtime, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);

    [LibraryImport(Runtime)]
    private static partial nint objc_autoreleasePoolPush();

    [LibraryImport(Runtime)]
    private static partial void objc_autoreleasePoolPop(nint pool);

    // objc_msgSend takes each method's own signature, so it gets one declaration per shape used.
    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint argument);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, char* characters, nint length);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void Send(nint receiver, nint selector, char* buffer, Range range);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial Range CheckSpelling(nint receiver, nint selector, nint text, nint start, nint language, byte wrap, nint tag, nint wordCount);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Guesses(nint receiver, nint selector, Range range, nint text, nint language, nint tag);
}
