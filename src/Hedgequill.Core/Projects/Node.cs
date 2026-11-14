using System.Collections.ObjectModel;

namespace Hedgequill.Core.Projects;

public enum NodeKind
{
    Project,
    Act,
    Chapter,
    Scene,
    Notes,
    Research,
    Page,
}

/// <summary>One entry in the outline, and the folder or Markdown file behind it.</summary>
public sealed class Node(NodeKind kind, string name, string title)
{
    public NodeKind Kind { get; } = kind;

    /// <summary>The file or folder name on disk, with ".md" for a file.</summary>
    public string Name { get; internal set; } = name;

    public string Title { get; internal set; } = title;

    public Node? Parent { get; internal set; }

    public ObservableCollection<Node> Children { get; } = [];

    /// <summary>A scene's or page's Markdown. Empty for folders.</summary>
    public string Text { get; set; } = "";

    public bool IsFile => Kind is NodeKind.Scene or NodeKind.Page;

    /// <summary>The path from the project folder, with forward slashes, as the index keys it. Empty for the project itself.</summary>
    public string RelativePath => Parent is null ? "" : Parent.Parent is null ? Name : $"{Parent.RelativePath}/{Name}";

    /// <summary>The title a node gets when the index names none: its file name without ".md".</summary>
    internal string DefaultTitle => IsFile ? Path.GetFileNameWithoutExtension(Name) : Name;

    internal void Add(Node child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    internal IEnumerable<Node> Descendants() => Children.SelectMany(child => child.Descendants().Prepend(child));
}
