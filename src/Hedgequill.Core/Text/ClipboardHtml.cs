using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hedgequill.Core.Text;

/// <summary>
/// HTML as the clipboard carries it. Windows' "HTML Format" is UTF-8 after a header of byte offsets that tell another app
/// where the HTML and the copied fragment start and end. macOS's public.html is the HTML alone.
/// </summary>
public static partial class ClipboardHtml
{
    private const string Before = "<html><body>\r\n<!--StartFragment-->";
    private const string After = "<!--EndFragment-->\r\n</body></html>";

    /// <summary>Windows' HTML Format for a fragment of HTML, with its header's offsets counted in UTF-8 bytes.</summary>
    public static byte[] Wrap(string fragment)
    {
        var header = Header(0, 0, 0, 0).Length;
        var startFragment = header + Encoding.UTF8.GetByteCount(Before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(After);
        return Encoding.UTF8.GetBytes(Header(header, endHtml, startFragment, endFragment) + Before + fragment + After);
    }

    /// <summary>
    /// The copied fragment of Windows' HTML Format, between the offsets its header gives, or the whole HTML when there's no
    /// header, as macOS gives it. The trailing zeros Windows pads a clipboard block with are dropped.
    /// </summary>
    public static string Unwrap(byte[] data)
    {
        var length = Array.FindLastIndex(data, b => b != 0) + 1;
        var head = Encoding.ASCII.GetString(data, 0, Math.Min(length, 400));
        if (Offset(head, "StartFragment") is { } start && Offset(head, "EndFragment") is { } end && start <= end && end <= length)
        {
            return Encoding.UTF8.GetString(data, start, end - start);
        }

        if (Offset(head, "StartHTML") is { } html && html <= length)
        {
            return Encoding.UTF8.GetString(data, html, length - html);
        }

        return Encoding.UTF8.GetString(data, 0, length);
    }

    private static string Header(int startHtml, int endHtml, int startFragment, int endFragment) => string.Create(
        CultureInfo.InvariantCulture,
        $"Version:0.9\r\nStartHTML:{startHtml:D10}\r\nEndHTML:{endHtml:D10}\r\nStartFragment:{startFragment:D10}\r\nEndFragment:{endFragment:D10}\r\n");

    private static int? Offset(string head, string name) =>
        HeaderField().Matches(head).FirstOrDefault(match => match.Groups[1].Value == name) is { } field
            && int.TryParse(field.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : null;

    [GeneratedRegex(@"^(\w+):(-?\d+)\r?$", RegexOptions.Multiline)]
    private static partial Regex HeaderField();
}
