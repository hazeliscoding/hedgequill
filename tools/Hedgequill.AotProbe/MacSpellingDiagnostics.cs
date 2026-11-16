using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hedgequill.AotProbe;

// Prints what NSSpellChecker reports about itself and the raw ranges it returns, so a macOS failure in CI explains itself.
[SupportedOSPlatform("macos")]
internal static unsafe partial class MacSpellingDiagnostics
{
    private const string Runtime = "/usr/lib/libobjc.A.dylib";

    public static void Print(string text)
    {
        var pool = objc_autoreleasePoolPush();
        try
        {
            var checker = Send(Class("NSSpellChecker"), Selector("sharedSpellChecker"));
            Console.WriteLine($"      language: {ToManaged(Send(checker, Selector("language")))}");
            Console.WriteLine($"      identifies languages: {SendBool(checker, Selector("automaticallyIdentifiesLanguages"))}");
            var available = Send(checker, Selector("availableLanguages"));
            Console.WriteLine($"      available: {Send(available, Selector("count"))}, first {ToManaged(Send(available, Selector("objectAtIndex:"), 0))}");
            var native = ToNative(text);
            var shortForm = CheckShort(checker, Selector("checkSpellingOfString:startingAt:"), native, 0);
            Console.WriteLine($"      checkSpellingOfString:startingAt: {shortForm.Location}, {shortForm.Length}");
            foreach (var language in new[] { "", "en", "en_US" })
            {
                var name = language.Length == 0 ? 0 : ToNative(language);
                var range = CheckLong(checker, Selector("checkSpellingOfString:startingAt:language:wrap:inSpellDocumentWithTag:wordCount:"), native, 0, name, 0, 0, 0);
                Console.WriteLine($"      long form, language \"{language}\": {range.Location}, {range.Length}");
            }

            var tag = Send(Class("NSSpellChecker"), Selector("uniqueSpellDocumentTag"));
            var tagged = CheckLong(checker, Selector("checkSpellingOfString:startingAt:language:wrap:inSpellDocumentWithTag:wordCount:"), native, 0, 0, 0, tag, 0);
            Console.WriteLine($"      long form, tag {tag}: {tagged.Location}, {tagged.Length}");
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
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
        if (native == 0)
        {
            return "(nil)";
        }

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

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial byte SendBool(nint receiver, nint selector);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint argument);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, char* characters, nint length);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void Send(nint receiver, nint selector, char* buffer, Range range);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial Range CheckShort(nint receiver, nint selector, nint text, nint start);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial Range CheckLong(nint receiver, nint selector, nint text, nint start, nint language, byte wrap, nint tag, nint wordCount);
}
