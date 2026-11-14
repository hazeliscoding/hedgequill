namespace Hedgequill.Tests.Core;

internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("hedgequill-tests-").FullName;

    public string this[string relative] => System.IO.Path.Combine([Path, .. relative.Split('/')]);

    public void Write(string relative, string text = "")
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this[relative])!);
        File.WriteAllText(this[relative], text);
    }

    public void Folder(string relative) => Directory.CreateDirectory(this[relative]);

    /// <summary>Every file and folder under the root, with forward slashes, sorted.</summary>
    public IEnumerable<string> Tree(string? under = null) =>
        Directory.EnumerateFileSystemEntries(under is null ? Path : this[under], "*", SearchOption.AllDirectories)
            .Select(entry => System.IO.Path.GetRelativePath(Path, entry).Replace('\\', '/') + (Directory.Exists(entry) ? "/" : ""))
            .Order(StringComparer.Ordinal);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Path, recursive: true);
    }
}
