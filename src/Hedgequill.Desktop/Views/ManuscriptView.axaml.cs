using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>One scene in a chapter's overview.</summary>
public sealed record SceneRow(Node Node, string Number, string Title, string Words);

public partial class ManuscriptView : UserControl
{
    private readonly Dictionary<Node, TextDocument> _documents = [];
    private readonly Dictionary<Node, int> _carets = [];
    private Project? _project;
    private Node? _node;
    private readonly SearchPanel _search;
    private bool _loading;

    public ManuscriptView()
    {
        InitializeComponent();
        _search = SearchPanel.Install(Editor);
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.AllowScrollBelowDocument = false;
        // Direct properties, which a style with a class can't set; bound to the theme's resources so they follow light and dark.
        var area = Editor.TextArea;
        area.Bind(TextArea.CaretBrushProperty, area.GetResourceObservable("Accent"));
        area.Bind(TextArea.SelectionBrushProperty, area.GetResourceObservable("SelectionManuscript"));
        area.Bind(TextArea.SelectionForegroundProperty, area.GetResourceObservable("TextPrimary"));
        area.SelectionBorder = null;
        area.SelectionCornerRadius = 2;
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => ((TranslateTransform)Overlay.RenderTransform!).Y = -Editor.TextArea.TextView.ScrollOffset.Y;
        Editor.SizeChanged += (_, _) => Layout();
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TemplatedControl.FontFamilyProperty || e.Property == TemplatedControl.FontSizeProperty)
            {
                UpdateLineHeight();
            }
        };
        DocHeader.SizeChanged += (_, _) => Layout();
        Editor.TextChanged += (_, _) =>
        {
            EmptyHint.IsVisible = Editor.Document.TextLength == 0;
            if (!_loading && _node is { } node)
            {
                Edited?.Invoke(node);
                if (FocusMode)
                {
                    UpdateWords();
                }
            }
        };
        Editor.AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Tunnel);
        BoldButton.Click += (_, _) => Wrap(Markdown.Bold);
        ItalicButton.Click += (_, _) => Wrap(Markdown.Italic);
        BreakButton.Click += (_, _) => InsertSceneBreak();
        AddSceneButton.Click += (_, _) => AddSceneRequested?.Invoke();
        LeaveFocus.Click += (_, _) => FocusExitRequested?.Invoke();
        ChapterScenes.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Control { DataContext: SceneRow row })
            {
                OpenRequested?.Invoke(row.Node);
            }
        });
        ToolTip.SetTip(BoldButton, $"Bold ({Command}B)");
        ToolTip.SetTip(ItalicButton, $"Italic ({Command}I)");
        ToolTip.SetTip(BreakButton, $"Scene break ({Command}Enter)");
    }

    /// <summary>The editor's text changed for this scene or page; the text itself stays in the editor until Commit.</summary>
    public event Action<Node>? Edited;

    public event Action<Node>? OpenRequested;

    public event Action? AddSceneRequested;

    public event Action? FocusExitRequested;

    public AvaloniaEdit.TextEditor TextEditor => Editor;

    public Node? Node => _node;

    public bool FocusMode
    {
        get;
        set
        {
            field = value;
            Toolbar.IsVisible = !value;
            FocusFooter.IsVisible = value && _node is not null;
            Layout();
        }
    }

    /// <summary>The save state shown in focus mode's corner.</summary>
    public string SaveLabel
    {
        get => FooterSave.Text ?? "";
        set => FooterSave.Text = value;
    }

    private static string Command => OperatingSystem.IsMacOS() ? "⌘" : "Ctrl+";

    /// <summary>Copies the editor's text into the open scene or page, so saving and counting see it.</summary>
    public void Commit()
    {
        if (_node is { } node && _documents.TryGetValue(node, out var document))
        {
            node.Text = document.Text;
        }
    }

    /// <summary>Replaces the editor's text with the scene's, as after Reload file. Undo history for it starts over.</summary>
    public void Reload(Node node)
    {
        if (_documents.Remove(node) && node == _node)
        {
            Show(_project!, node);
        }
    }

    public void Show(Project project, Node? node)
    {
        Commit();
        if (_node is { } previous)
        {
            _carets[previous] = Editor.CaretOffset;
        }

        _project = project;
        _node = node;
        EditorPane.IsVisible = node?.IsFile == true;
        ChapterPane.IsVisible = node?.Kind == NodeKind.Chapter;
        EmptyPane.IsVisible = !EditorPane.IsVisible && !ChapterPane.IsVisible;
        FocusFooter.IsVisible = FocusMode && node is not null;

        if (node is null)
        {
            EmptyTitle.Text = "";
            EmptyDetail.Text = "Select a scene to write.";
        }
        else if (node.IsFile)
        {
            ShowDocument(node);
        }
        else if (node.Kind == NodeKind.Chapter)
        {
            ShowChapter(node);
        }
        else
        {
            var count = node.Children.Count;
            EmptyTitle.Text = node.Title;
            EmptyDetail.Text = node.Kind == NodeKind.Act
                ? $"{count} chapter{(count == 1 ? "" : "s")}. Acts only group chapters; select a scene to write."
                : $"{count} page{(count == 1 ? "" : "s")}.";
        }

        UpdateWords();
    }

    public void UpdateWords()
    {
        FooterWords.Text = _node switch
        {
            { IsFile: true } node => Labels.Words(Words.Count(_documents.TryGetValue(node, out var document) ? document.Text : node.Text)),
            { Kind: NodeKind.Chapter } chapter => Labels.Words(chapter.Children.Sum(scene => Words.Count(scene.Text))),
            _ => "",
        };
    }

    public void Wrap(string marker) => Apply(Markdown.Wrap(Editor.Document.Text, Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength, marker));

    public void InsertSceneBreak() => Apply(Markdown.InsertSceneBreak(Editor.Document.Text, Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength));

    private void Apply(Change change)
    {
        if (_node?.IsFile != true)
        {
            return;
        }

        Editor.Document.Replace(change.Offset, change.Length, change.Insert);
        Editor.SelectionLength = 0;
        Editor.CaretOffset = change.Caret;
        Editor.TextArea.Focus();
    }

    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        // The text area takes Escape for itself, so focus mode looks first; an open Find keeps it.
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && FocusMode && !_search.IsOpened)
        {
            FocusExitRequested?.Invoke();
            e.Handled = true;
            return;
        }

        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.KeyModifiers != command)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.B:
                Wrap(Markdown.Bold);
                break;
            case Key.I:
                Wrap(Markdown.Italic);
                break;
            case Key.Enter when _node?.Kind == NodeKind.Scene:
                InsertSceneBreak();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void ShowDocument(Node node)
    {
        var isPage = node.Kind == NodeKind.Page;
        if (!_documents.TryGetValue(node, out var document))
        {
            document = new TextDocument(node.Text);
            _documents[node] = document;
        }

        _loading = true;
        Editor.Document = document;
        _loading = false;
        Editor.CaretOffset = Math.Min(_carets.GetValueOrDefault(node), document.TextLength);
        Editor.Classes.Set("page", isPage);
        EmptyHint.Classes.Set("page", isPage);
        BreakButton.IsVisible = !isPage;

        var chapter = ChapterOf(node);
        Eyebrow.IsVisible = !isPage;
        Eyebrow.Text = chapter is null ? "" : $"Chapter {NumberOf(chapter)} · {chapter.Title}".ToUpper(CultureInfo.CurrentCulture);
        DocTitle.Text = node.Title;
        DocTitle.FontSize = isPage ? 26 : 30;
        DocHeader.Margin = DocHeader.Margin.WithBottom(isPage ? 20 : 28);
        EmptyHint.Text = isPage ? "Notes are plain pages. Write anything." : "Begin here.";
        EmptyHint.IsVisible = document.TextLength == 0;
        AutomationProperties.SetName(Editor.TextArea, node.Title);
        Layout();
    }

    private void ShowChapter(Node chapter)
    {
        var scenes = chapter.Children.ToList();
        var words = scenes.Sum(scene => Words.Count(scene.Text));
        ChapterEyebrow.Text = $"CHAPTER {NumberOf(chapter)}";
        ChapterTitle.Text = chapter.Title;
        ChapterMeta.Text = $"{scenes.Count} scene{(scenes.Count == 1 ? "" : "s")} · {Labels.Words(words)}";
        ChapterScenes.ItemsSource = scenes.Select((scene, i) => new SceneRow(scene, (i + 1).ToString(CultureInfo.InvariantCulture), scene.Title, Labels.Words(Words.Count(scene.Text)))).ToList();
        ChapterScenes.IsVisible = scenes.Count > 0;
        ChapterEmpty.IsVisible = scenes.Count == 0;
    }

    /// <summary>The prototype's line heights: 1.7 for the manuscript, 1.65 for pages. AvaloniaEdit scales the font's own line spacing, so start from that.</summary>
    private void UpdateLineHeight()
    {
        var metrics = new Typeface(Editor.FontFamily).GlyphTypeface.Metrics;
        var natural = metrics.LineSpacing * Editor.FontSize / metrics.DesignEmHeight;
        var target = Editor.FontSize * (Editor.Classes.Contains("page") ? 1.65 : 1.7);
        if (natural > 0)
        {
            Editor.Options.LineHeightFactor = target / natural;
        }
    }

    /// <summary>Centres the text column as the prototype does: 620 px including 32 px of padding a side, 640 for a page.</summary>
    private void Layout()
    {
        if (_node?.IsFile != true)
        {
            return;
        }

        var measure = _node.Kind == NodeKind.Page ? 640 : 620;
        var side = Math.Max(32, ((Editor.Bounds.Width - measure) / 2) + 32);
        var top = FocusMode ? 120 : 64;
        var header = DocHeader.Bounds.Height + DocHeader.Margin.Bottom;
        Overlay.Margin = new Thickness(side, top, side, 0);
        EmptyHint.Margin = new Thickness(0, header, 0, 0);
        Editor.Padding = new Thickness(side, top + header, side, 160);
    }

    private Node? ChapterOf(Node node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.Kind == NodeKind.Chapter)
            {
                return current;
            }
        }

        return null;
    }

    private int NumberOf(Node chapter) => _project!.Root.Descendants().Where(node => node.Kind == NodeKind.Chapter).ToList().IndexOf(chapter) + 1;
}

internal static class ThicknessExtensions
{
    public static Thickness WithBottom(this Thickness thickness, double bottom) => new(thickness.Left, thickness.Top, thickness.Right, bottom);
}
