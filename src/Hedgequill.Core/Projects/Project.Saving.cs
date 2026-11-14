using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

/// <summary>Another app changed the file since Hedgequill last read or wrote it, so saving would overwrite that change.</summary>
public sealed class ChangedOnDiskException(string path) : IOException($"{path} changed outside Hedgequill.");

/// <summary>Writing scenes and pages. Every write is atomic, and nothing overwrites a change made in another app unless asked.</summary>
public sealed partial class Project
{
    /// <summary>Writes a scene's or page's text to its file, unless it's unchanged, or another app changed the file and <paramref name="overwrite"/> is false.</summary>
    public void Save(Node node, bool overwrite = false)
    {
        Require(node.IsFile && (node.Parent == Root || Root.Descendants().Contains(node)), "Only a scene or page in the project can be saved.");
        var path = PathOf(node);
        var text = node.Text.ReplaceLineEndings("\n");
        if (text == node.SavedText && File.Exists(path))
        {
            return;
        }

        if (!overwrite && File.Exists(path))
        {
            var disk = File.ReadAllText(path).ReplaceLineEndings("\n");
            if (disk != node.SavedText && disk != text)
            {
                throw new ChangedOnDiskException(path);
            }
        }

        // A folder deleted in another app comes back rather than losing the words.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, text);
        node.Text = node.SavedText = text;
    }

    /// <summary>Replaces a scene's or page's text with what's on disk now.</summary>
    public void Reload(Node node) => node.Text = node.SavedText = File.ReadAllText(PathOf(node)).ReplaceLineEndings("\n");

    /// <summary>Writes a scene's or page's text to a file of the writer's choosing, leaving the project as it is.</summary>
    public static void SaveCopy(Node node, string path) => AtomicFile.Write(path, node.Text.ReplaceLineEndings("\n"));

    /// <summary>Copies the whole project into another location with every scene's and page's text as it is now, and opens the copy.</summary>
    public Project SaveAs(string location)
    {
        Directory.CreateDirectory(location);
        var target = Path.Combine(location, FileNames.Unique(Path.GetFileName(Folder), "", candidate => Path.Exists(Path.Combine(location, candidate))));
        CopyFolder(Folder, target);
        foreach (var node in Root.Descendants().Where(node => node.IsFile))
        {
            AtomicFile.Write(Path.Combine([target, .. node.RelativePath.Split('/')]), node.Text.ReplaceLineEndings("\n"));
        }

        var copy = Open(target);
        copy.Root.Title = Title;
        copy.SaveIndex();
        return copy;
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.EnumerateDirectories(from))
        {
            CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }
}
