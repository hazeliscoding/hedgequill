using System.Globalization;
using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

/// <summary>A scene's text as it was before a session changed it: a plain Markdown file in Versions/.</summary>
public sealed record SceneVersion(DateTime When, string Path)
{
    public string Text => File.ReadAllText(Path).ReplaceLineEndings("\n");
}

/// <summary>
/// Earlier versions, as decided on 2026-11-14: the first save of a scene in a session keeps its previous text in Versions/,
/// under the same folders the scene sits in, so a rewrite the writer regrets can be brought back after Undo is gone.
/// </summary>
public sealed partial class Project
{
    public const string VersionsFolder = "Versions";

    // Sorts by name and holds no colon, which Windows won't put in a file name.
    private const string Stamp = "yyyy-MM-dd HH.mm.ss";

    private readonly HashSet<Node> _kept = [];

    /// <summary>A scene's kept versions, newest first.</summary>
    public IReadOnlyList<SceneVersion> Versions(Node scene)
    {
        var folder = VersionsOf(scene);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.md")
            .Select(path => (Path: path, Name: Path.GetFileNameWithoutExtension(path)))
            .Where(file => file.Name.Length >= Stamp.Length)
            .Select(file => DateTime.TryParseExact(file.Name[..Stamp.Length], Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? new SceneVersion(when, file.Path) : null)
            .OfType<SceneVersion>()
            // Two kept in the same second are "… 2.md" and so on, newer the longer the name.
            .OrderByDescending(version => version.When).ThenByDescending(version => version.Path.Length).ThenByDescending(version => version.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Puts an earlier version's text in the scene, keeping the text it replaces as a version too. The caller saves the scene,
    /// as it does any edit.
    /// </summary>
    public void BringBack(Node scene, SceneVersion version)
    {
        var text = version.Text;
        if (scene.Text != scene.SavedText)
        {
            // Unsaved edits: the text on disk is older still, and this session may not have kept it yet.
            KeepOnce(scene);
        }

        _kept.Add(scene);
        Keep(scene, scene.Text);
        scene.Text = text;
    }

    /// <summary>The first save of a scene this session keeps the text it's about to replace.</summary>
    private void KeepOnce(Node node)
    {
        if (node.Kind == NodeKind.Scene && !_kept.Contains(node))
        {
            Keep(node, node.SavedText);
            _kept.Add(node);
        }
    }

    private void Keep(Node scene, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var folder = VersionsOf(scene);
        Directory.CreateDirectory(folder);
        var name = FileNames.Unique(DateTime.Now.ToString(Stamp, CultureInfo.InvariantCulture), ".md", candidate => Path.Exists(Path.Combine(folder, candidate)));
        AtomicFile.Write(Path.Combine(folder, name), text.ReplaceLineEndings("\n"));
    }

    // Versions/ mirrors the outline: Chapter 1/Fog.md keeps its versions in Versions/Chapter 1/Fog/.
    private string VersionsOf(Node node)
    {
        var path = node.IsFile ? node.RelativePath[..^".md".Length] : node.RelativePath;
        return Path.Combine([Folder, VersionsFolder, .. path.Split('/')]);
    }

    /// <summary>A scene's versions follow it, or the chapter or act it's in, to a new name or place.</summary>
    private void MoveVersions(string from, string to)
    {
        // shortcut: a folder already at the new place (a deleted scene's, of the same name) keeps both apart; merge them if writers ask.
        if (from == to || !Directory.Exists(from) || Path.Exists(to))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            Directory.Move(from, to);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The rename itself is done; the versions stay under the old name, still on disk.
        }
    }
}
