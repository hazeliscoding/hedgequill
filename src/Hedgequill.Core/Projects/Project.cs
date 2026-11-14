using System.Globalization;
using System.Text.Json;
using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

/// <summary>A folder of Markdown files whose folders mirror the outline, plus hedgequill.json for order and details.</summary>
public sealed partial class Project
{
    public const string IndexFile = "hedgequill.json";
    public const string TrashFolder = "Trash";
    public const string UntitledStory = "Untitled story";

    private const string NotesFolder = "Notes";
    private const string ResearchFolder = "Research";

    // "Chapter 2" before "Chapter 10" when the index doesn't say.
    private static readonly StringComparer ByName = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering | CompareOptions.IgnoreCase);

    private Project(string folder, Node root)
    {
        Folder = folder;
        Root = root;
    }

    public string Folder { get; }

    public Node Root { get; }

    public string Title => Root.Title;

    public IEnumerable<Node> Scenes => Root.Descendants().Where(node => node.Kind == NodeKind.Scene);

    public int Words => Scenes.Sum(scene => Text.Words.Count(scene.Text));

    /// <summary>Makes a folder named after the title inside the location, holding Chapter 1/Scene 1.md, Notes/ and Research/.</summary>
    public static Project Create(string location, string title)
    {
        title = title.Trim() is { Length: > 0 } trimmed ? trimmed : UntitledStory;
        Directory.CreateDirectory(location);
        var name = FileNames.Unique(FileNames.From(title, UntitledStory), "", candidate => Path.Exists(Path.Combine(location, candidate)));
        var folder = Path.Combine(location, name);
        Directory.CreateDirectory(Path.Combine(folder, "Chapter 1"));
        Directory.CreateDirectory(Path.Combine(folder, NotesFolder));
        Directory.CreateDirectory(Path.Combine(folder, ResearchFolder));
        AtomicFile.Write(Path.Combine(folder, "Chapter 1", "Scene 1.md"), "");

        var project = Open(folder);
        project.Root.Title = title;
        project.SaveIndex();
        return project;
    }

    /// <summary>Reads a project, or any folder: what the index doesn't list is added in name order. Opening writes nothing.</summary>
    public static Project Open(string folder)
    {
        folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"There's no folder at {folder}.");
        }

        var index = ReadIndex(Path.Combine(folder, IndexFile));
        var root = new Node(NodeKind.Project, Path.GetFileName(folder), "");
        var acts = (index.Acts ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in Ordered(Folders(folder).Where(name => !IsReserved(name)), index, ""))
        {
            var path = Path.Combine(folder, name);
            // An empty folder is a chapter unless the index lists it as an act.
            var isAct = Folders(path).Any() || (acts.Contains(name) && !MarkdownFiles(path).Any());
            var node = new Node(isAct ? NodeKind.Act : NodeKind.Chapter, name, "");
            root.Add(node);
            if (isAct)
            {
                foreach (var chapter in Ordered(Folders(path), index, node.RelativePath))
                {
                    var child = new Node(NodeKind.Chapter, chapter, "");
                    node.Add(child);
                    AddFiles(child, Path.Combine(path, chapter), NodeKind.Scene, index);
                }
            }
            else
            {
                AddFiles(node, path, NodeKind.Scene, index);
            }
        }

        foreach (var (kind, name) in new[] { (NodeKind.Notes, NotesFolder), (NodeKind.Research, ResearchFolder) })
        {
            var group = new Node(kind, name, name);
            root.Add(group);
            if (Directory.Exists(Path.Combine(folder, name)))
            {
                AddFiles(group, Path.Combine(folder, name), NodeKind.Page, index);
            }
        }

        foreach (var node in root.Descendants().Prepend(root).Where(node => node.Kind is not (NodeKind.Notes or NodeKind.Research)))
        {
            node.Title = index.Titles?.GetValueOrDefault(node.RelativePath) ?? node.DefaultTitle;
        }

        return new Project(folder, root);
    }

    public string PathOf(Node node) => node.Parent is null ? Folder : Path.Combine(PathOf(node.Parent), node.Name);

    /// <summary>Writes hedgequill.json from the outline as it stands.</summary>
    public void SaveIndex()
    {
        var nodes = Root.Descendants().Prepend(Root).ToList();
        var titles = nodes
            .Where(node => node.Kind is not (NodeKind.Notes or NodeKind.Research) && node.Title != node.DefaultTitle)
            .ToDictionary(node => node.RelativePath, node => node.Title);
        var index = new ProjectIndex
        {
            Order = nodes.Where(node => node.Children.Count > 0).ToDictionary(node => node.RelativePath, node => node.Children.Select(child => child.Name).ToList()),
            Acts = Root.Children.Where(node => node.Kind == NodeKind.Act).Select(node => node.Name).ToList() is { Count: > 0 } acts ? acts : null,
            Titles = titles.Count > 0 ? titles : null,
        };
        AtomicFile.Write(Path.Combine(Folder, IndexFile), JsonSerializer.Serialize(index, CoreJson.Readable.ProjectIndex) + "\n");
    }

    private static ProjectIndex ReadIndex(string path)
    {
        ProjectIndex? index;
        try
        {
            index = JsonSerializer.Deserialize(File.ReadAllText(path), CoreJson.Readable.ProjectIndex);
        }
        catch (FileNotFoundException)
        {
            return new ProjectIndex();
        }
        catch (JsonException)
        {
            // It only holds the order, so a damaged one costs the order and nothing else.
            return new ProjectIndex();
        }

        return index switch
        {
            null => new ProjectIndex(),
            { Format: > ProjectIndex.CurrentFormat } => throw new InvalidDataException("A newer version of Hedgequill made this project. Update Hedgequill to open it."),
            _ => index,
        };
    }

    private static void AddFiles(Node parent, string folder, NodeKind kind, ProjectIndex index)
    {
        foreach (var name in Ordered(MarkdownFiles(folder), index, parent.RelativePath))
        {
            parent.Add(new Node(kind, name, "") { Text = File.ReadAllText(Path.Combine(folder, name)) });
        }
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> names, ProjectIndex index, string key)
    {
        var remaining = names.ToList();
        var ordered = new List<string>();
        foreach (var listed in index.Order?.GetValueOrDefault(key) ?? [])
        {
            // Another editor may have changed only the case of a name.
            var match = remaining.FirstOrDefault(name => string.Equals(name, listed, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
                remaining.Remove(match);
            }
        }

        return ordered.Concat(remaining.Order(ByName));
    }

    private static bool IsReserved(string name) =>
        name.Equals(NotesFolder, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(ResearchFolder, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(TrashFolder, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Folders(string folder) =>
        Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().Where(name => !name.StartsWith('.'));

    private static IEnumerable<string> MarkdownFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.md").Select(Path.GetFileName).OfType<string>()
            .Where(name => !name.StartsWith('.') && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
}
