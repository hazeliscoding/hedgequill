using Avalonia.Controls;
using Avalonia.Layout;
using Hedgequill.Core.Ao3;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

public partial class DetailsFields : UserControl
{
    private readonly List<Field> _fields;
    private Node? _node;
    private bool _loading;

    public DetailsFields()
    {
        InitializeComponent();
        // AO3's limits on its posting form: 1,250 characters for a summary, 5,000 for notes.
        _fields =
        [
            new("Summary", 1250, 3, node => node.Summary, (node, text) => node.Summary = text),
            new("Beginning notes", 5000, 4, node => node.BeginningNotes, (node, text) => node.BeginningNotes = text),
            new("End notes", 5000, 4, node => node.EndNotes, (node, text) => node.EndNotes = text),
        ];
        foreach (var field in _fields)
        {
            Fields.Children.Add(Build(field));
        }
    }

    /// <summary>A field's text changed; the node holds it now, and hedgequill.json needs saving.</summary>
    public event Action? Changed;

    /// <summary>Copy: the field's label and its text as AO3 HTML.</summary>
    public event Action<string, string>? CopyRequested;

    /// <summary>The work's fields when the node is the project, else the chapter's.</summary>
    public void Show(Node node)
    {
        _node = node;
        var whole = node.Kind == NodeKind.Project ? "the work" : "the chapter";
        _loading = true;
        foreach (var field in _fields)
        {
            field.Box.Text = field.Get(node);
            field.Box.PlaceholderText = field.Label switch
            {
                "Summary" => $"A few lines readers see on AO3 before they open {whole}.",
                "Beginning notes" => $"Shown before {whole}: thanks, context, a heads-up.",
                _ => $"Shown after {whole}.",
            };
            Count(field);
        }

        _loading = false;
    }

    private Grid Build(Field field)
    {
        var label = new TextBlock { Text = field.Label, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("field-label");
        field.CountText.VerticalAlignment = VerticalAlignment.Center;
        field.CountText.Classes.Add("field-count");
        var copy = new Button { Content = "Copy" };
        copy.Classes.AddRange(["small", "tertiary"]);
        Avalonia.Automation.AutomationProperties.SetName(copy, $"Copy {field.Label.ToLowerInvariant()}");
        ToolTip.SetTip(copy, $"Copy the {field.Label.ToLowerInvariant()} as HTML for AO3");
        copy.Click += (_, _) => CopyRequested?.Invoke(field.Label, Html(field.Box.Text));

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(field.CountText, 2);
        Grid.SetColumn(copy, 3);
        header.Children.AddRange([label, field.CountText, copy]);

        field.Box.Classes.Add("ao3-field");
        field.Box.MinHeight = (field.Rows * 23.25) + 22;
        Avalonia.Automation.AutomationProperties.SetName(field.Box, field.Label);
        field.Box.TextChanged += (_, _) =>
        {
            if (_loading || _node is null)
            {
                return;
            }

            field.Set(_node, field.Box.Text ?? "");
            Count(field);
            Changed?.Invoke();
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 6 };
        Grid.SetRow(field.Box, 1);
        grid.Children.AddRange([header, field.Box]);
        return grid;
    }

    // AO3 counts what's pasted, and Copy gives HTML, so the count is the HTML's length.
    private static void Count(Field field)
    {
        var length = Html(field.Box.Text).Length;
        field.CountText.Text = length > field.Limit ? $"{length:N0} of {field.Limit:N0} · {length - field.Limit:N0} over" : $"{length:N0} of {field.Limit:N0}";
        field.CountText.Classes.Set("over", length > field.Limit);
    }

    private static string Html(string? text) => string.IsNullOrWhiteSpace(text) ? "" : Ao3Text.Html(text);

    private sealed record Field(string Label, int Limit, int Rows, Func<Node, string> Get, Action<Node, string> Set)
    {
        public TextBox Box { get; } = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

        public TextBlock CountText { get; } = new();
    }
}
