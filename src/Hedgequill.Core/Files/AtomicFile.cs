using System.Text;

namespace Hedgequill.Core.Files;

/// <summary>Another app has the file open, as a sync client does while it uploads. It passes; the save can try again.</summary>
public sealed class FileHeldException(string path, Exception inner)
    : IOException($"Another app has {Path.GetFileName(path)} open. Hedgequill will try again.", inner);

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

            try
            {
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception error) when (IsHeld(error, path))
            {
                throw new FileHeldException(path, error);
            }
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

    /// <summary>
    /// Whether a refusal means another app has the file open. Windows refuses to replace a file opened without delete sharing
    /// as "access denied", and to open one held exclusively as a sharing violation. A file marked read-only is refused the same
    /// way but stays refused, so it doesn't count.
    /// </summary>
    public static bool IsHeld(Exception error, string path) => error switch
    {
        IOException io => (io.HResult & 0xFFFF) is 32 or 33,
        UnauthorizedAccessException => File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly),
        _ => false,
    };
}
