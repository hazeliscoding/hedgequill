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
using Hedgequill.Desktop.Spelling;
using Hedgequill.Desktop.Themes;

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
    private readonly MarkdownStyles _styles;
    private readonly Checkboxes _boxes;
    private readonly CodeBlocks _code;
    private readonly TitleSpace _titleSpace;
    private bool _loading;
    private int _menuOffset;
    private int _textSize = 100;

    // The page's top margin: 64 px, or 120 in focus mode, sliding between them.
    private double _top = 64;
    private readonly Slide _topSlide;

    // One system checker for the app, opened the first time an editor needs it.
    private static readonly Lazy<SpellChecker?> Speller = new(SpellChecker.Create);

    public ManuscriptView()
    {
        InitializeComponent();
        _topSlide = new Slide(top =>
        {
            _top = top;
            Layout();
        });
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
        var blocks = new PageBlocks(area.TextView);
        _styles = new MarkdownStyles { Blocks = blocks, Faded = Brushes.Gray, Quote = Brushes.Gray, Done = Brushes.Gray, CodeBackground = Brushes.LightGray, Title = FontFamily.Default, Mono = FontFamily.Default };
        area.TextView.LineTransformers.Add(_styles);
        area.TextView.ElementGenerators.Add(new SceneBreaks(area, blocks));
        _boxes = new Checkboxes(blocks);
        _code = new CodeBlocks(blocks);
        area.TextView.ElementGenerators.Add(_boxes);
        area.TextView.ElementGenerators.Add(_code);
        area.TextView.ElementGenerators.Add(new TableColumns(blocks));
        area.TextView.BackgroundRenderers.Add(_code);
        _titleSpace = new TitleSpace(area.TextView);
        area.TextView.ElementGenerators.Insert(0, _titleSpace);
        // The caret on the first row brings the title above it back too, as Ctrl+Home would show a page's top.
        area.Caret.PositionChanged += (_, _) =>
        {
            if (area.Caret.Line == 1 && area.Caret.CalculateCaretRectangle().Top <= _titleSpace.Height + 1)
            {
                area.TextView.MakeVisible(new Rect(0, 0, 1, 1));
            }
        };
        if (Speller.Value is { } speller)
        {
            Spelling = new SpellingUnderlines(area.TextView, speller, word => _project?.Knows(word) == true);
            area.TextView.BackgroundRenderers.Add(Spelling);
        }

        area.ContextFlyout = new MenuFlyout().FillOnOpening(EditorMenu);
        area.AddHandler(ContextRequestedEvent, (_, e) => _menuOffset = OffsetAt(e), RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Restyle();
        ActualThemeVariantChanged += (_, _) => Restyle();
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
        LeaveFocus.Click += (_, _) => FocusToggleRequested?.Invoke();
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

    public event Action? FocusToggleRequested;

    /// <summary>Something went wrong that the writer should hear about: a heading and a detail.</summary>
    public event Action<string, string>? NoticeRequested;

    /// <summary>The "For AO3" fields under a chapter's or the work's overview.</summary>
    public DetailsFields Ao3Fields => ForAo3;

    public AvaloniaEdit.TextEditor TextEditor => Editor;

    /// <summary>Spell check's underlines, or null where the system has no checker.</summary>
    public SpellingUnderlines? Spelling { get; }

    public Node? Node => _node;

    public bool FocusMode
    {
        get;
        set
        {
            field = value;
            Toolbar.Opacity = value ? 0 : 1;
            Toolbar.IsHitTestVisible = !value;
            FocusFooter.IsVisible = value && _node is not null;
            _topSlide.Run(this, _top, value ? 120 : 64);
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
        ChapterPane.IsVisible = node?.Kind is NodeKind.Chapter or NodeKind.Project;
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
        else if (node.Kind == NodeKind.Project)
        {
            ShowWork(project);
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

    /// <summary>View › Larger text and Smaller text, in percent of the prototype's size: 19 px Cardo for scenes, 15.5 px for pages.</summary>
    public int TextSize
    {
        get => _textSize;
        set
        {
            _textSize = value;
            ApplyTextSize();
        }
    }

    /// <summary>Selects text in the open scene or page and scrolls to it, as a match in Find in project does.</summary>
    public void Reveal(int offset, int length)
    {
        offset = Math.Min(offset, Editor.Document.TextLength);
        Editor.Select(offset, Math.Min(length, Editor.Document.TextLength - offset));
        var location = Editor.Document.GetLocation(offset);
        Editor.ScrollTo(location.Line, location.Column);
        Editor.TextArea.Focus();
    }

    /// <summary>Edit › Find…: AvaloniaEdit's search panel, on the open scene or page.</summary>
    public void OpenFind()
    {
        if (_node?.IsFile == true)
        {
            _search.Open();
        }
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
            FocusToggleRequested?.Invoke();
            e.Handled = true;
            return;
        }

        // In a list, Enter starts the next item or ends the list, and Tab and Shift+Tab nest an item.
        if (e.Key is Key.Enter or Key.Tab && Editor.SelectionLength == 0 && _node?.IsFile == true && ListKey(e) is { } list)
        {
            if (list.Length > 0 || list.Insert.Length > 0)
            {
                Apply(list);
            }

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

    private Change? ListKey(KeyEventArgs e) => (e.Key, e.KeyModifiers) switch
    {
        (Key.Enter, KeyModifiers.None) => Markdown.Enter(Editor.Document.Text, Editor.CaretOffset),
        (Key.Tab, KeyModifiers.None or KeyModifiers.Shift) => Markdown.Nest(Editor.Document.Text, Editor.CaretOffset, e.KeyModifiers == KeyModifiers.Shift),
        _ => null,
    };

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
        ApplyTextSize();
        BreakButton.IsVisible = !isPage;

        var chapter = ChapterOf(node);
        Eyebrow.IsVisible = !isPage;
        Eyebrow.Text = chapter is null ? "" : $"Chapter {NumberOf(chapter)} · {chapter.Title}".ToUpper(CultureInfo.CurrentCulture);
        DocTitle.Text = node.Title;
        DocTitle.FontSize = isPage ? 26 : 30;
        DocHeader.Margin = DocHeader.Margin.WithBottom(isPage ? 20 : 28);
        EmptyHint.Text = isPage ? "Write anything. Lists, checklists, code and tables work here too." : "Begin here.";
        EmptyHint.IsVisible = document.TextLength == 0;
        AutomationProperties.SetName(Editor.TextArea, node.Title);
        Layout();
    }

    /// <summary>The work as AO3 lists it: its title, chapters and words, and its summary and notes.</summary>
    private void ShowWork(Project project)
    {
        var chapters = project.Root.Descendants().Count(node => node.Kind == NodeKind.Chapter);
        ChapterEyebrow.Text = "WORK";
        ChapterTitle.Text = project.Title;
        ChapterMeta.Text = $"{chapters} chapter{(chapters == 1 ? "" : "s")} · {Labels.Words(project.Words)}";
        ChapterScenes.IsVisible = false;
        ChapterEmpty.IsVisible = false;
        ForAo3.Show(project.Root);
    }

    private void ShowChapter(Node chapter)
    {
        ForAo3.Show(chapter);
        var scenes = chapter.Children.ToList();
        var words = scenes.Sum(scene => Words.Count(scene.Text));
        ChapterEyebrow.Text = $"CHAPTER {NumberOf(chapter)}";
        ChapterTitle.Text = chapter.Title;
        ChapterMeta.Text = $"{scenes.Count} scene{(scenes.Count == 1 ? "" : "s")} · {Labels.Words(words)}";
        ChapterScenes.ItemsSource = scenes.Select((scene, i) => new SceneRow(scene, (i + 1).ToString(CultureInfo.InvariantCulture), scene.Title, Labels.Words(Words.Count(scene.Text)))).ToList();
        ChapterScenes.IsVisible = scenes.Count > 0;
        ChapterEmpty.IsVisible = scenes.Count == 0;
    }

    // The colorizer's and the underlines' brushes follow light and dark.
    private void Restyle()
    {
        _styles.Faded = Resource<IBrush>("MarkerFaded") ?? _styles.Faded;
        _styles.Quote = Resource<IBrush>("TextSecondary") ?? _styles.Quote;
        _styles.Title = Resource<FontFamily>("FontTitle") ?? _styles.Title;
        _styles.Mono = Resource<FontFamily>("FontMono") ?? _styles.Mono;
        _styles.Done = Resource<IBrush>("TextTertiary") ?? _styles.Done;
        _styles.CodeBackground = _code.Background = Resource<IBrush>("BgInset") ?? _styles.CodeBackground;
        _boxes.Border = Resource<IBrush>("TextTertiary") ?? _boxes.Border;
        _boxes.Accent = Resource<IBrush>("Accent") ?? _boxes.Accent;
        _boxes.Tick = Resource<IBrush>("TextOnAccent") ?? _boxes.Tick;
        if (Spelling is not null)
        {
            Spelling.Brush = Resource<IBrush>("Misspelling") ?? Spelling.Brush;
        }

        Editor.TextArea.TextView.Redraw();
    }

    // Where the menu was asked for: under the pointer, or at the caret from the keyboard.
    private int OffsetAt(ContextRequestedEventArgs e)
    {
        var view = Editor.TextArea.TextView;
        return e.TryGetPosition(view, out var point) && view.GetPosition(point + view.ScrollOffset) is { } position
            ? Editor.Document.GetOffset(position.Location)
            : Editor.CaretOffset;
    }

    /// <summary>The editor's menu, as the prototype draws it: a misspelled word's suggestions first, then editing.</summary>
    private List<Control> EditorMenu()
    {
        var items = new List<Control>();
        if (Spelling?.At(_menuOffset) is { } word && _project is { } project)
        {
            var text = Editor.Document.GetText(word);
            var suggestions = Speller.Value!.Suggest(text).Take(5).ToList();
            items.AddRange(suggestions.Count > 0
                ? suggestions.Select(suggestion => Item(suggestion, () => Apply(new Change(word.StartOffset, word.Length, suggestion, word.StartOffset + suggestion.Length))))
                : [new MenuItem { Header = "No suggestions", IsEnabled = false }]);
            items.Add(Item("Add to project dictionary", () => AddToDictionary(project, text)));
            items.Add(new Separator());
        }

        var selected = Editor.SelectionLength > 0;
        items.AddRange(
        [
            Item("Cut", Editor.Cut, Gesture(Key.X), selected),
            Item("Copy", Editor.Copy, Gesture(Key.C), selected),
            Item("Paste", Editor.Paste, Gesture(Key.V)),
            new Separator(),
            Item("Bold", () => Wrap(Markdown.Bold), Gesture(Key.B)),
            Item("Italic", () => Wrap(Markdown.Italic), Gesture(Key.I)),
        ]);
        if (_node?.Kind == NodeKind.Scene)
        {
            items.Add(Item("Insert scene break", InsertSceneBreak, Gesture(Key.Enter)));
        }

        items.AddRange([new Separator(), Item(FocusMode ? "Leave focus mode" : "Focus mode", () => FocusToggleRequested?.Invoke(), Gesture(Key.F, KeyModifiers.Shift))]);
        return items;
    }

    private void AddToDictionary(Project project, string word)
    {
        try
        {
            project.AddToDictionary(word);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The word stays known for this session; only hedgequill.json missed it.
            NoticeRequested?.Invoke($"Couldn't save “{word}” to the project dictionary", error.Message);
        }

        Spelling?.Refresh();
    }

    private static KeyGesture Gesture(Key key, KeyModifiers more = KeyModifiers.None) =>
        new(key, (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) | more);

    private static MenuItem Item(string header, Action action, KeyGesture? gesture = null, bool enabled = true)
    {
        var item = new MenuItem { Header = header, InputGesture = gesture, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private T? Resource<T>(string key)
        where T : class => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as T : null;

    private void ApplyTextSize()
    {
        var size = (Editor.Classes.Contains("page") ? 15.5 : 19) * _textSize / 100.0;
        Editor.FontSize = size;
        EmptyHint.FontSize = size;
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
        var header = DocHeader.Bounds.Height + DocHeader.Margin.Bottom;
        Overlay.Margin = new Thickness(side, _top, side, 0);
        EmptyHint.Margin = new Thickness(0, header, 0, 0);
        _titleSpace.Height = _top + header;
        Editor.Padding = new Thickness(side, 0, side, 160);
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
