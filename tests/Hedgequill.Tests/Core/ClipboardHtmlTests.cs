using System.Globalization;
using System.Text;
using Hedgequill.Core.Text;

namespace Hedgequill.Tests.Core;

public class ClipboardHtmlTests
{
    // Curly quotes, an accent and a dash take more than one UTF-8 byte, so offsets counted in characters would fall short.
    private const string Fragment = "<p><em>Mira’s</em> café — closed.</p>";

    [Fact]
    public void Wraps_HTML_with_a_header_whose_byte_offsets_find_the_fragment()
    {
        var data = ClipboardHtml.Wrap(Fragment);
        var text = Encoding.ASCII.GetString(data);

        Assert.StartsWith("Version:0.9\r\n", text, StringComparison.Ordinal);
        Assert.Equal(Fragment, Encoding.UTF8.GetString(data[Offset(text, "StartFragment")..Offset(text, "EndFragment")]));
        Assert.StartsWith("<html>", Encoding.UTF8.GetString(data[Offset(text, "StartHTML")..]), StringComparison.Ordinal);
        Assert.Equal(data.Length, Offset(text, "EndHTML"));
    }

    [Fact]
    public void Unwraps_the_fragment_and_ignores_the_zeros_Windows_pads_with() =>
        Assert.Equal(Fragment, ClipboardHtml.Unwrap([.. ClipboardHtml.Wrap(Fragment), 0, 0, 0]));

    [Fact]
    public void Reads_macOS_HTML_whole() =>
        Assert.Equal("<meta charset=\"utf-8\">" + Fragment, ClipboardHtml.Unwrap(Encoding.UTF8.GetBytes("<meta charset=\"utf-8\">" + Fragment)));

    private static int Offset(string header, string name)
    {
        var start = header.IndexOf(name + ":", StringComparison.Ordinal) + name.Length + 1;
        return int.Parse(header.AsSpan(start, 10), CultureInfo.InvariantCulture);
    }
}
