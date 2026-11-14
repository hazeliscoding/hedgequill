using Avalonia;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

/// <summary>One visible line of the navigator. The prototype flattens the outline into rows, indented by depth.</summary>
public sealed class NavigatorRow(Node node, int depth, int chapterNumber, bool expanded, bool first, bool renaming)
{
    public Node Node { get; } = node;

    public string Title => Node.Title;

    /// <summary>Acts, Notes and Research are small labels over their contents.</summary>
    public bool IsLabel => Node.Kind is NodeKind.Act or NodeKind.Notes or NodeKind.Research;

    public bool IsChapter => Node.Kind == NodeKind.Chapter;

    public string? Number { get; } = chapterNumber > 0 ? chapterNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

    public bool HasChildren => Node.Children.Count > 0;

    public bool Expanded { get; } = expanded;

    public double Height => IsLabel ? 22 : 28;

    public Thickness Gap => new(0, IsLabel && !first ? 12 : 0, 0, 0);

    public Thickness Indent => new(4 + (depth * 14), 0, 6, 0);

    public double ChevronAngle => Expanded ? 90 : 0;

    /// <summary>An open act or group shows its chevron only on hover, as in the prototype.</summary>
    public bool QuietChevron => IsLabel && Expanded;

    public bool IsRenaming { get; } = renaming;

    /// <summary>Scenes, pages and chapters can be dragged, as in the prototype; acts and groups stay put.</summary>
    public bool IsDraggable => Node.Kind is NodeKind.Scene or NodeKind.Page or NodeKind.Chapter;

    public double Depth { get; } = depth;
}
