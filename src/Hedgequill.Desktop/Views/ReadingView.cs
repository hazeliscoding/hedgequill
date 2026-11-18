using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Hedgequill.Core.Ao3;
using Ao3Run = Hedgequill.Core.Ao3.Run;
using Run = Avalonia.Controls.Documents.Run;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// A Notes or Research page's reading view, as Obsidian has: the page without its Markdown markers, at the page's size.
/// List text hangs beside its bullet or number, a checklist's boxes still tick, a table is drawn as one, and code keeps its
/// face and background. It reads the same blocks as AO3 HTML and the preview, and sets apart what AO3 drops.
/// </summary>
public sealed class ReadingView : UserControl
{
    private readonly StackPanel _body = new();
    private double _size = 15.5;
    private int? _toggled;

    public ReadingView() => Content = _body;

    /// <summary>A box was clicked or toggled with Space: the number, from 0, of the source line holding it.</summary>
    public event Action<int>? Toggled;

    /// <summary>Draws the page at this text size. A box just toggled keeps the keyboard focus.</summary>
    public void Show(string markdown, double size)
    {
        _size = size;
        _body.Children.Clear();
        _body.Children.AddRange(Ao3Text.Read(markdown).Select(Render));
        if (_toggled is { } line)
        {
            _toggled = null;
            _body.GetLogicalDescendants().OfType<CheckBox>().FirstOrDefault(box => Equals(box.Tag, line))?.Focus();
        }
    }

    private double Em(double ems) => _size * ems;

    private Control Render(Block block) => block switch
    {
        Paragraph paragraph => Text(paragraph.Lines, new Thickness(0, 0, 0, Em(0.9))),
        Heading heading => Heading(heading),
        Quote quote => Quote(quote),
        ListBlock list => List(list),
        CodeBlock code => Code(code),
        Table table => Table(table),
        _ => Break(),
    };

    private SelectableTextBlock Text(IEnumerable<Line> lines, Thickness margin, string? style = null)
    {
        var text = new SelectableTextBlock { Margin = margin, FontSize = _size, LineHeight = Em(1.65), TextWrapping = TextWrapping.Wrap };
        text.Classes.Add("reading");
        if (style is not null)
        {
            text.Classes.Add(style);
        }

        foreach (var (line, index) in lines.Select((line, index) => (line, index)))
        {
            if (index > 0)
            {
                text.Inlines!.Add(new LineBreak());
            }

            text.Inlines!.AddRange(line.Runs.Select(Inline));
        }

        return text;
    }

    // A link looks like one but never opens: Hedgequill launches nothing but the project folder. A plain run sets no
    // style of its own, so a quote's italic and a table header's weight carry into it.
    private Run Inline(Ao3Run run)
    {
        var inline = new Run(run.Text);
        if (run.Style.HasFlag(Style.Emphasis))
        {
            inline.FontStyle = FontStyle.Italic;
        }

        if (run.Style.HasFlag(Style.Strong))
        {
            inline.FontWeight = FontWeight.Bold;
        }

        if (run.Style.HasFlag(Style.Strike))
        {
            inline.TextDecorations = TextDecorations.Strikethrough;
        }

        if (run.Style.HasFlag(Style.Code))
        {
            inline.Classes.Add("code");
            inline.FontSize = Em(MarkdownStyles.CodeScale);
        }

        if (run.Link is not null)
        {
            inline.Classes.Add("link");
        }

        return inline;
    }

    private SelectableTextBlock Heading(Heading heading)
    {
        var text = Text([heading.Line], new Thickness(0, Em(1.2), 0, Em(0.6)), "reading-heading");
        text.FontSize = Em(1.3);
        text.LineHeight = Em(1.3 * 1.3);
        return text;
    }

    private Border Quote(Quote quote)
    {
        var stack = new StackPanel();
        stack.Children.AddRange(quote.Paragraphs.Select((paragraph, i) => Text(paragraph.Lines, new Thickness(0, i > 0 ? Em(0.9) : 0, 0, 0), "quote")));
        var border = new Border { Child = stack, Margin = new Thickness(0, 0, 0, Em(0.9)), Padding = new Thickness(Em(1), 0, 0, 0), BorderThickness = new Thickness(2, 0, 0, 0) };
        border.Classes.Add("reading-quote");
        return border;
    }

    private StackPanel List(ListBlock list)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, Em(0.9)) };
        stack.Children.AddRange(list.Items.Select((item, i) => Item(item, list.Ordered ? $"{list.Start + i}." : null)));
        return stack;
    }

    // The marker or box in its own column, so the item's text wraps under itself.
    private Grid Item(ListEntry item, string? number)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(Em(1.4) * item.Level, 0, 0, 0) };
        Control marker;
        if (item.Checked is { } done)
        {
            var box = new CheckBox { IsChecked = done, Tag = item.Source, Margin = new Thickness(0, (Em(1.65) - 13) / 2, Em(0.48), 0), VerticalAlignment = VerticalAlignment.Top };
            box.Classes.Add("box");
            AutomationProperties.SetName(box, string.Concat(item.Text.Lines.SelectMany(line => line.Runs).Select(run => run.Text)));
            box.Click += (_, _) =>
            {
                _toggled = item.Source;
                Toggled?.Invoke(item.Source);
            };
            marker = box;
        }
        else
        {
            var text = new TextBlock { Text = number ?? "•", FontSize = _size, LineHeight = Em(1.65), MinWidth = Em(number is null ? 1.1 : 1.5) };
            text.Classes.Add("reading-marker");
            marker = text;
        }

        var body = Text(item.Text.Lines, default, item.Checked == true ? "done" : null);
        Grid.SetColumn(body, 1);
        grid.Children.AddRange([marker, body]);
        return grid;
    }

    private Border Code(CodeBlock code)
    {
        var text = Text(code.Lines, default, "code");
        text.FontSize = Em(MarkdownStyles.CodeScale);
        text.LineHeight = Em(MarkdownStyles.CodeScale * 1.65);
        var border = new Border { Child = text, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, Em(0.9)), Padding = new Thickness(Em(0.85), Em(0.6)) };
        border.Classes.Add("reading-code");
        return border;
    }

    // A real table: a rule under every row, the header in bold.
    private Grid Table(Table table)
    {
        var columns = table.Rows.Max(row => row.Count);
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, Em(0.9)) };
        grid.ColumnDefinitions.AddRange(Enumerable.Range(0, columns).Select(_ => new ColumnDefinition(GridLength.Auto)));
        grid.RowDefinitions.AddRange(table.Rows.Select(_ => new RowDefinition(GridLength.Auto)));
        foreach (var (row, r) in table.Rows.Select((row, r) => (row, r)))
        {
            for (var c = 0; c < columns; c++)
            {
                var text = Text(c < row.Count ? [row[c]] : [], default, r == 0 ? "header" : null);
                text.FontSize = Em(0.94);
                var cell = new Border { Child = text, Padding = new Thickness(0, Em(0.35), Em(1.1), Em(0.35)), BorderThickness = new Thickness(0, 0, 0, 1) };
                cell.Classes.Add("reading-cell");
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private Border Break()
    {
        var line = new Border { Height = 1, Margin = new Thickness(Em(3), Em(1.6)) };
        line.Classes.Add("preview-break");
        return line;
    }
}
