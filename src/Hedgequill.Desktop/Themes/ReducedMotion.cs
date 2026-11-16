using System.Runtime.InteropServices;

namespace Hedgequill.Desktop.Themes;

/// <summary>
/// Whether the system asks apps to move less: Windows' Animation effects switch, or macOS's Reduce motion. Avalonia exposes
/// neither, so this reads it with one native call per platform, the only ones outside spell check.
/// </summary>
internal static partial class ReducedMotion
{
    private const string Runtime = "/usr/lib/libobjc.A.dylib";
    private const uint GetClientAreaAnimation = 0x1042;

    public static bool Asked()
    {
        if (OperatingSystem.IsWindows())
        {
            return SystemParametersInfoW(GetClientAreaAnimation, 0, out var animate, 0) && animate == 0;
        }

        if (OperatingSystem.IsMacOS())
        {
            // Program asks before Avalonia has loaded AppKit.
            NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
            var workspace = Send(Class("NSWorkspace"), Selector("sharedWorkspace"));
            return workspace != 0 && SendForBool(workspace, Selector("accessibilityDisplayShouldReduceMotion")) != 0;
        }

        return false;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint parameter, out int value, uint update);

    [LibraryImport(Runtime, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Class(string name);

    [LibraryImport(Runtime, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    // Objective-C's BOOL is one byte on both Apple Silicon and Intel.
    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial byte SendForBool(nint receiver, nint selector);
}
