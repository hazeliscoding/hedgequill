using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Hedgequill.Core.Ao3;
using Ao3Run = Hedgequill.Core.Ao3.Run;
using Run = Avalonia.Controls.Documents.Run;

namespace Hedgequill.Desktop.Views;

public partial class PreviewView : UserControl
{
    // The prototype's preview: Cardo at 15.5 px on a 1.6 line height, with spacing in ems of it.
    private const double Size = 15.5;

    public PreviewView() => InitializeComponent();

    /// <summary>A chapter's scenes as AO3 shows them, a scene break between scenes.</summary>
    public void ShowChapter(string title, string meta, IEnumerable<IReadOnlyList<Block>> scenes)
    {
        ChapterTitle.Text = title;
        ChapterMeta.Text = meta;
        Body.Children.Clear();
        foreach (var (scene, index) in scenes.Select((scene, index) => (scene, index)))
        {
            if (index > 0)
            {
                Body.Children.Add(Divider());
            }

            Body.Children.AddRange(scene.Select(Render));
        }

        Chapter.IsVisible = true;
        Empty.IsVisible = false;
    }

    /// <summary>Nothing to preview: why, and what to do about it.</summary>
    public void ShowEmpty(string title, string detail)
    {
        EmptyTitle.Text = title;
        EmptyDetail.Text = detail;
        EmptyDetail.IsVisible = detail.Length > 0;
        Body.Children.Clear();
        Chapter.IsVisible = false;
        Empty.IsVisible = true;
    }

    private static Control Render(Block block) => block switch
    {
        Paragraph paragraph => Text(paragraph.Lines, new Thickness(0, 0, 0, Size * 0.9)),
        Heading heading => Heading(heading),
        Quote quote => Stack(new Thickness(Size * 1.4, 0, 0, Size * 0.9), quote.Paragraphs.Select(paragraph => Text(paragraph.Lines, new Thickness(0, 0, 0, Size * 0.9), "quote"))),
        ListBlock list => Stack(new Thickness(Size * 1.4, 0, 0, Size * 0.9), list.Items.Select((item, i) => Item(list.Ordered ? $"{list.Start + i}." : "•", Ao3Text.Posted(item)))),
        // AO3 doesn't keep code or tables, so they show as the plain lines it posts.
        CodeBlock code => Text(code.Lines, new Thickness(0, 0, 0, Size * 0.9)),
        Table table => Text(table.Rows.Select(Ao3Text.Posted), new Thickness(0, 0, 0, Size * 0.9)),
        _ => Divider(),
    };

    private static TextBlock Text(IEnumerable<Line> lines, Thickness margin, string? style = null)
    {
        var text = new TextBlock { Margin = margin };
        text.Classes.Add("preview-text");
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

    private static TextBlock Heading(Heading heading) => Text([heading.Line], new Thickness(0, Size * 1.2, 0, Size * 0.5), "preview-heading");

    // A link looks like one but never opens: Hedgequill launches nothing but the project folder. A plain run sets no
    // style of its own, so a quote's italic carries into it.
    private static Run Inline(Ao3Run run)
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

        if (run.Link is not null)
        {
            inline.Classes.Add("link");
        }

        return inline;
    }

    private static Grid Item(string marker, IEnumerable<Line> item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, Size * 0.3) };
        var number = new TextBlock { Text = marker, Margin = new Thickness(0, 0, 8, 0) };
        number.Classes.Add("preview-text");
        var text = Text(item, default);
        Grid.SetColumn(text, 1);
        grid.Children.AddRange([number, text]);
        return grid;
    }

    private static StackPanel Stack(Thickness margin, IEnumerable<Control> children)
    {
        var stack = new StackPanel { Margin = margin };
        stack.Children.AddRange(children);
        return stack;
    }

    private static Border Divider()
    {
        var line = new Border { Height = 1, Margin = new Thickness(Size * 3, Size * 1.8), HorizontalAlignment = HorizontalAlignment.Stretch };
        line.Classes.Add("preview-break");
        return line;
    }
}
