# Saves the clipboard's HTML Format exactly as the app that copied it left it, for a paste fixture in
# tests/Hedgequill.Tests/Fixtures/Paste. Copy invented text, never a real fic, then run:
#   pwsh -NoProfile -File scripts/save_clipboard_html.ps1 tests/Hedgequill.Tests/Fixtures/Paste/<app>.cfhtml
# Two private details are blanked with as many x's, so the header's byte offsets still hold: the Windows user name in
# Word's temp-file links, and the page a browser says it copied from, which for Google Docs is the document's address.
param([Parameter(Mandatory)] [string] $Out)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class RawClipboard
{
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr handle);

    public static byte[] Read(string name)
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(RegisterClipboardFormat(name));
            if (handle == IntPtr.Zero) return null;
            var data = GlobalLock(handle);
            var bytes = new byte[(int)GlobalSize(handle).ToUInt32()];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            GlobalUnlock(handle);
            return bytes;
        }
        finally { CloseClipboard(); }
    }
}
"@

$bytes = [RawClipboard]::Read("HTML Format")
if (-not $bytes) { throw "There's no HTML on the clipboard. Copy the text again and rerun." }

$latin = [Text.Encoding]::Latin1
$text = $latin.GetString($bytes)
$text = $text.Replace("/Users/$env:USERNAME/", "/Users/" + ("x" * $env:USERNAME.Length) + "/")
$text = [regex]::Replace($text, "(?m)^(SourceURL:)(.*?)(\r?)$", { param($m) $m.Groups[1].Value + ("x" * $m.Groups[2].Value.Length) + $m.Groups[3].Value })
[IO.File]::WriteAllBytes($Out, $latin.GetBytes($text))
"Saved $($bytes.Length) bytes to $Out."
