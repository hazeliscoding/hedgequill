using System.Text;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop;

/// <summary>
/// HTML on the clipboard beside plain text, as word processors and mail apps read it: Windows' HTML Format, behind the
/// header <see cref="ClipboardHtml"/> writes, or macOS's public.html. The AOT probe checks both under NativeAOT.
/// </summary>
public static class RichClipboard
{
    private static readonly DataFormat<byte[]> Html = DataFormat.CreateBytesPlatformFormat(
        OperatingSystem.IsWindows() ? "HTML Format" : OperatingSystem.IsMacOS() ? "public.html" : "text/html");

    /// <summary>Puts text on the clipboard twice: as HTML, for apps that keep its italics and bold, and as plain text for the rest.</summary>
    public static Task Write(IClipboard clipboard, string text, string html)
    {
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, text);
        item.Set(Html, OperatingSystem.IsWindows() ? ClipboardHtml.Wrap(html) : Encoding.UTF8.GetBytes(html));
        var transfer = new DataTransfer();
        transfer.Add(item);
        return clipboard.SetDataAsync(transfer);
    }

    /// <summary>The HTML an app left on the clipboard, or null when it left none.</summary>
    public static async Task<string?> ReadHtml(IClipboard clipboard) =>
        await clipboard.TryGetValueAsync(Html) is { Length: > 0 } bytes ? ClipboardHtml.Unwrap(bytes) : null;
}
