using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>Find in project: searches every scene and page as the writer types, and replaces in all of them at once.</summary>
public partial class FindPanel : UserControl
{
    // As the prototype lists them: up to 50 matches a scene, each with a little of the text around it.
    private const int MatchesShown = 50;
    private const int Before = 40;
    private const int After = 50;

    private readonly DispatcherTimer _search;

    public FindPanel()
    {
        InitializeComponent();
        // A moment after typing stops, so a one-letter query in a long project doesn't hold up the next key.
        _search = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Refresh());
        Query.TextChanged += (_, _) => SearchSoon();
        MatchCase.IsCheckedChanged += (_, _) => Refresh();
        WholeWords.IsCheckedChanged += (_, _) => Refresh();
        Close.Click += (_, _) => CloseRequested?.Invoke();
        ReplaceAll.Click += (_, _) =>
        {
            if (Pattern() is { } pattern)
            {
                ReplaceRequested?.Invoke(pattern, Replacement.Text ?? "");
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseRequested?.Invoke();
                e.Handled = true;
            }
        };
    }

    /// <summary>The scenes and pages to search, in outline order, holding the editor's text.</summary>
    public Func<IEnumerable<Node>> Documents { get; set; } = () => [];

    public event Action? CloseRequested;

    /// <summary>A match was chosen: the scene or page, and where in its text.</summary>
    public event Action<Node, int, int>? OpenRequested;

    /// <summary>Replace all: what to find, and what to put in its place.</summary>
    public event Action<Regex, string>? ReplaceRequested;

    public void Open()
    {
        Refresh();
        // After layout: the panel was hidden a moment ago, and a hidden box can't take focus.
        Dispatcher.UIThread.Post(
            () =>
            {
                Query.Focus(NavigationMethod.Tab);
                Query.SelectAll();
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>Searches again, as after a replace or an edit.</summary>
    public void Refresh()
    {
        _search.Stop();
        Results.Children.Clear();
        var pattern = Pattern();
        var (total, documents) = (0, 0);
        foreach (var node in pattern is null ? [] : Documents())
        {
            var matches = pattern!.Matches(node.Text);
            if (matches.Count == 0)
            {
                continue;
            }

            total += matches.Count;
            documents++;
            Results.Children.Add(Group(node));
            foreach (var match in matches.Take(MatchesShown))
            {
                Results.Children.Add(Row(node, match));
            }
        }

        Summary.Text = pattern is null ? "Every scene and page"
            : total == 0 ? "No matches"
            : $"{total.ToString("N0", CultureInfo.InvariantCulture)} match{(total == 1 ? "" : "es")} in {documents} scene{(documents == 1 ? "" : "s")}";
        ReplaceAll.IsEnabled = total > 0;
        if (total == 0)
        {
            Results.Children.Add(new TextBlock
            {
                Classes = { "manuscript-italic" },
                FontSize = 14,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
                Margin = new(6, 16, 6, 0),
                Text = pattern is null ? "Type to search every scene and page." : $"Nothing in this project matches “{Query.Text}”.",
            });
        }
    }

    private Regex? Pattern() => Find.Pattern(Query.Text ?? "", MatchCase.IsChecked == true, WholeWords.IsChecked == true);

    private void SearchSoon()
    {
        _search.Stop();
        _search.Start();
    }

    private static StackPanel Group(Node node) => new()
    {
        Margin = new(6, 10, 6, 2),
        Children =
        {
            new TextBlock { Classes = { "group-title" }, Text = node.Title, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Classes = { "caption" }, Text = node.Parent?.Title ?? "", TextTrimming = TextTrimming.CharacterEllipsis },
        },
    };

    private Button Row(Node node, Match match)
    {
        var text = node.Text;
        var start = Math.Max(0, match.Index - Before);
        var end = Math.Min(text.Length, match.Index + match.Length + After);
        var before = (start > 0 ? "…" : "") + Flat(text[start..match.Index]);
        var after = Flat(text[(match.Index + match.Length)..end]) + (end < text.Length ? "…" : "");
        var snippet = new TextBlock { Classes = { "match" } };
        snippet.Inlines = [new Run(before), new Run(Flat(match.Value)) { Classes = { "hit" } }, new Run(after)];
        var row = new Button { Classes = { "scene-row", "match" }, Content = snippet, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(row, $"{node.Title}: {before}{match.Value}{after}");
        var (index, length) = (match.Index, match.Length);
        row.Click += (_, _) => OpenRequested?.Invoke(node, index, length);
        return row;
    }

    // A paragraph break reads as one space in a snippet, as the prototype shows it.
    private static string Flat(string text) => LineBreaks().Replace(text, " ");

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreaks();
}
