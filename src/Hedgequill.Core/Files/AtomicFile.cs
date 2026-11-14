using System.Text;

namespace Hedgequill.Core.Files;

/// <summary>Writes a file so that a crash or a full disk leaves either the old file or the new one, never half of either.</summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static void Write(string path, string text)
    {
        // Same folder, so the final move is a rename on one volume. The dot hides it from macOS and from Hedgequill's own scan.
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Utf8.GetBytes(text));
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }
}
