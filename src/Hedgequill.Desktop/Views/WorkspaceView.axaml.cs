using Avalonia.Controls;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

public partial class WorkspaceView : UserControl
{
    private readonly Project _project;
    private readonly HashSet<Node> _collapsed = [];

    public WorkspaceView(Project project)
    {
        _project = project;
        InitializeComponent();
        ProjectTitle.Text = project.Title;
        Navigator.SelectionChanged += (_, _) => ShowSelected();
        Navigator.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Button { DataContext: NavigatorRow row })
            {
                Toggle(row.Node);
                e.Handled = true;
            }
        });
        Refresh();
        Select(project.Scenes.FirstOrDefault());
    }

    public IReadOnlyList<NavigatorRow> Rows => (IReadOnlyList<NavigatorRow>?)Navigator.ItemsSource ?? [];

    public Node? Selected => (Navigator.SelectedItem as NavigatorRow)?.Node;

    public void Select(Node? node) => Navigator.SelectedItem = Rows.FirstOrDefault(row => row.Node == node);

    public void Toggle(Node node)
    {
        if (!_collapsed.Remove(node))
        {
            _collapsed.Add(node);
        }

        var selected = Selected;
        Refresh();
        // A row inside a collapsed folder goes away, so its folder takes the selection.
        Select(selected is null ? null : Ancestors(selected).Prepend(selected).Reverse().FirstOrDefault(node => _collapsed.Contains(node)) ?? selected);
    }

    private void Refresh()
    {
        var chapters = _project.Root.Children.SelectMany(node => node.Kind == NodeKind.Act ? node.Children : [node]).Where(node => node.Kind == NodeKind.Chapter).ToList();
        var rows = new List<NavigatorRow>();
        Add(_project.Root, 0);
        Navigator.ItemsSource = rows;

        void Add(Node parent, int depth)
        {
            foreach (var node in parent.Children)
            {
                var expanded = !_collapsed.Contains(node);
                rows.Add(new NavigatorRow(node, depth, chapters.IndexOf(node) + 1, expanded, rows.Count == 0));
                if (expanded)
                {
                    Add(node, depth + 1);
                }
            }
        }
    }

    private void ShowSelected()
    {
        var node = Selected;
        var chapter = node is null ? null : Ancestors(node).Prepend(node).FirstOrDefault(n => n.Kind == NodeKind.Chapter);
        var chapterLabel = chapter is null ? "" : $"Chapter {Rows.First(row => row.Node == chapter).Number}";
        HeaderTitle.Text = node?.Title ?? _project.Title;
        HeaderSub.Text = node?.Kind switch
        {
            NodeKind.Scene => $"{chapterLabel} — {chapter!.Title}",
            NodeKind.Chapter => chapterLabel,
            NodeKind.Page => node.Parent!.Title,
            NodeKind.Act => "Act",
            _ => "",
        };
        Placeholder.Text = node?.Kind switch
        {
            NodeKind.Act => $"{node.Children.Count} chapter{(node.Children.Count == 1 ? "" : "s")}. Acts only group chapters; select a scene to write.",
            null => "Select a scene to write.",
            _ => "",
        };
    }

    private static IEnumerable<Node> Ancestors(Node node)
    {
        for (var parent = node.Parent; parent?.Parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }
}
