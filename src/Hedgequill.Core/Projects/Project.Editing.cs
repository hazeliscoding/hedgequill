using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

/// <summary>What Delete moved into Trash/, so Undo can put it back.</summary>
public sealed record Deleted(Node Node, Node Parent, int Index, string TrashPath);

/// <summary>Changes to the outline. Each one changes the disk first, then the outline, then writes hedgequill.json.</summary>
public sealed partial class Project
{
    public static bool CanHold(Node parent, NodeKind kind) => (parent.Kind, kind) switch
    {
        (NodeKind.Project, NodeKind.Act or NodeKind.Chapter) => true,
        (NodeKind.Act, NodeKind.Chapter) => true,
        (NodeKind.Chapter, NodeKind.Scene) => true,
        (NodeKind.Notes or NodeKind.Research, NodeKind.Page) => true,
        _ => false,
    };

    /// <summary>Where a new child goes: last, except that Notes and Research stay at the bottom of the project.</summary>
    public int EndOf(Node parent) =>
        parent == Root ? Root.Children.Count(node => node.Kind is NodeKind.Act or NodeKind.Chapter) : parent.Children.Count;

    /// <summary>Adds an untitled act, chapter, scene or page at the end of the parent, as an empty folder or file.</summary>
    public Node Add(Node parent, NodeKind kind)
    {
        Require(CanHold(parent, kind), $"A {parent.Kind} can't hold a {kind}.");
        var extension = kind is NodeKind.Scene or NodeKind.Page ? ".md" : "";
        var folder = PathOf(parent);
        Directory.CreateDirectory(folder);
        var name = UniqueName(parent, Untitled(kind), extension, Untitled(kind), except: null);
        if (extension.Length > 0)
        {
            AtomicFile.Write(Path.Combine(folder, name), "");
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(folder, name));
        }

        var node = new Node(kind, name, "");
        node.Title = node.DefaultTitle;
        parent.Insert(EndOf(parent), node);
        SaveIndex();
        return node;
    }

    /// <summary>Gives a node a new title, and its file or folder the closest name the disk allows.</summary>
    public void Rename(Node node, string title)
    {
        title = title.Trim();
        Require(node.Parent is not null && node.Kind is not (NodeKind.Notes or NodeKind.Research), "Only acts, chapters, scenes and pages can be renamed.");
        if (title.Length == 0 || title == node.Title)
        {
            return;
        }

        var name = UniqueName(node.Parent!, title, Extension(node), Untitled(node.Kind), except: node);
        if (name != node.Name)
        {
            MoveOnDisk(node, PathOf(node), Path.Combine(PathOf(node.Parent!), name));
            node.Name = name;
        }

        node.Title = title;
        SaveIndex();
    }

    /// <summary>Moves a node to a position among the parent's children, counted without the node itself.</summary>
    public void Move(Node node, Node parent, int index)
    {
        Require(node.Parent is not null && CanHold(parent, node.Kind), $"A {parent.Kind} can't hold a {node.Kind}.");
        if (node.Parent != parent)
        {
            var name = UniqueName(parent, BaseName(node), Extension(node), Untitled(node.Kind), except: null);
            Directory.CreateDirectory(PathOf(parent));
            MoveOnDisk(node, PathOf(node), Path.Combine(PathOf(parent), name));
            node.Name = name;
        }

        node.Detach();
        parent.Insert(Math.Clamp(index, 0, EndOf(parent)), node);
        SaveIndex();
    }

    /// <summary>Moves a node's file or folder into Trash/, under the same folders it sat in, and takes it out of the outline.</summary>
    public Deleted Delete(Node node)
    {
        Require(node.Parent is not null && node.Kind is not (NodeKind.Notes or NodeKind.Research), "Only acts, chapters, scenes and pages can be deleted.");
        var parent = node.Parent!;
        var trashFolder = Path.Combine([Folder, TrashFolder, .. parent.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)]);
        Directory.CreateDirectory(trashFolder);
        var trashPath = Path.Combine(trashFolder, FileNames.Unique(BaseName(node), Extension(node), candidate => Path.Exists(Path.Combine(trashFolder, candidate))));
        MoveOnDisk(node, PathOf(node), trashPath);

        var index = node.Detach();
        SaveIndex();
        return new Deleted(node, parent, index, trashPath);
    }

    /// <summary>Undoes a Delete: the file or folder goes back where it was, and empty folders it leaves in Trash/ go away.</summary>
    public void Restore(Deleted deleted)
    {
        var (node, parent, index, trashPath) = deleted;
        Require(parent == Root || Root.Descendants().Contains(parent), "The folder it came from is gone.");
        var name = UniqueName(parent, BaseName(node), Extension(node), Untitled(node.Kind), except: null);
        Directory.CreateDirectory(PathOf(parent));
        MoveOnDisk(node, trashPath, Path.Combine(PathOf(parent), name));
        node.Name = name;
        parent.Insert(Math.Min(index, EndOf(parent)), node);
        SaveIndex();

        var trash = Path.Combine(Folder, TrashFolder);
        for (var folder = Path.GetDirectoryName(trashPath); folder is not null && folder.Length >= trash.Length && !Directory.EnumerateFileSystemEntries(folder).Any(); folder = Path.GetDirectoryName(folder))
        {
            Directory.Delete(folder);
        }
    }

    private static string Untitled(NodeKind kind) => kind switch
    {
        NodeKind.Act => "New act",
        NodeKind.Chapter => "Untitled chapter",
        NodeKind.Scene => "Untitled scene",
        _ => "Untitled page",
    };

    private static string Extension(Node node) => node.IsFile ? ".md" : "";

    private static string BaseName(Node node) => node.IsFile ? Path.GetFileNameWithoutExtension(node.Name) : node.Name;

    private static void MoveOnDisk(Node node, string from, string to)
    {
        if (node.IsFile)
        {
            File.Move(from, to);
        }
        else
        {
            Directory.Move(from, to);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>A name in the parent's folder that no file, sibling or reserved folder uses; the node itself doesn't count, so a case change is allowed.</summary>
    private string UniqueName(Node parent, string title, string extension, string fallback, Node? except)
    {
        var folder = PathOf(parent);
        return FileNames.Unique(FileNames.From(title, fallback), extension, candidate =>
            !(except is not null && string.Equals(candidate, except.Name, StringComparison.OrdinalIgnoreCase)) &&
            (Path.Exists(Path.Combine(folder, candidate)) ||
             parent.Children.Any(child => string.Equals(child.Name, candidate, StringComparison.OrdinalIgnoreCase)) ||
             (parent == Root && IsReserved(candidate))));
    }
}
