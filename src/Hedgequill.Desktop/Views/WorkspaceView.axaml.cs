using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hedgequill.Core.Ao3;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

public enum Placement
{
    Before,
    After,
    Into,
}

public partial class WorkspaceView : UserControl
{
    private const double DragThreshold = 4;

    private readonly Project _project;
    private readonly HashSet<Node> _collapsed = [];
    private Node? _renaming;
    private (Node Node, Point Start)? _press;
    private bool _dragging;
    private bool _navigatorWanted = true;
    private NavigatorRow? _contextRow;
    private (Node Target, Placement Where)? _drop;
    private readonly DispatcherTimer _previewTimer;
    private bool _previewWanted = true;
    private double _previewWidth = 340;

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
        Navigator.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Navigator.AddHandler(LostFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox box && _renaming is not null)
            {
                CommitRename(box.Text ?? "");
            }
        });
        Navigator.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual visual && visual.FindAncestorOfType<Button>(includeSelf: true) is null && Row(e.Source) is { } row)
            {
                StartRename(row.Node);
            }
        };
        // The row menus, as the prototype has them. The tunnel notes which row before the flyout opens, so a right-click,
        // Shift+F10 and the Menu key all get the menu for that row; empty space gets the add menu.
        Navigator.AddHandler(
            ContextRequestedEvent,
            (_, e) =>
            {
                _contextRow = Row(e.Source);
                if (_contextRow is { Node.Kind: not (NodeKind.Notes or NodeKind.Research) } row)
                {
                    Select(row.Node);
                }
            },
            RoutingStrategies.Tunnel);
        var rowMenu = new MenuFlyout();
        rowMenu.Opening += (_, _) => rowMenu.ItemsSource = _contextRow is { } row ? MenuFor(row.Node) : AddMenu();
        Navigator.ContextFlyout = rowMenu;
        Navigator.AddHandler(PointerPressedEvent, OnPress, RoutingStrategies.Tunnel);
        Navigator.AddHandler(PointerMovedEvent, OnDrag, RoutingStrategies.Tunnel);
        Navigator.AddHandler(PointerReleasedEvent, OnRelease, RoutingStrategies.Tunnel);
        Navigator.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag());
        NewScene.Click += (_, _) => AddScene();
        ProjectMenu.Click += (_, _) => new MenuFlyout { ItemsSource = new List<Control> { Item("Close project", () => CloseRequested?.Invoke()) } }.ShowAt(ProjectMenu);
        NavigatorToggle.Click += (_, _) => ToggleNavigator();
        FocusButton.Click += (_, _) => ToggleFocus();
        Manuscript.OpenRequested += Select;
        Manuscript.AddSceneRequested += AddScene;
        Manuscript.FocusToggleRequested += ToggleFocus;
        Manuscript.NoticeRequested += (heading, detail) => NoticeRequested?.Invoke(heading, detail, null, null);
        Manuscript.Edited += node =>
        {
            Edited?.Invoke(node);
            // The preview follows the text a moment after typing stops, so a keystroke never waits on it.
            _previewTimer!.Stop();
            _previewTimer.Start();
        };
        AddHandler(KeyDownEvent, OnWorkspaceKey, RoutingStrategies.Tunnel);
        ToolTip.SetTip(FocusButton, $"Focus mode ({Shortcut("Shift+F")})");
        _previewTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) =>
        {
            _previewTimer!.Stop();
            UpdatePreview();
        });
        PreviewToggle.Click += (_, _) => TogglePreview();
        CopyHtml.Click += (_, _) => _ = CopyChapterHtml();
        var previewMenu = new MenuFlyout();
        previewMenu.Opening += (_, _) => previewMenu.ItemsSource = PreviewMenu();
        Preview.ContextFlyout = previewMenu;
        // The preview starts hidden in a window too narrow for three columns, as the prototype does under 1,200 px.
        AttachedToVisualTree += (_, _) =>
        {
            _previewWanted = TopLevel.GetTopLevel(this) is not { ClientSize.Width: < 1200 };
            ShowPreview();
        };
        var addMenu = new MenuFlyout { Placement = PlacementMode.TopEdgeAlignedRight };
        addMenu.Opening += (_, _) => addMenu.ItemsSource = AddMenu();
        More.Flyout = addMenu;

        Refresh();
        Select(project.Scenes.FirstOrDefault());
    }

    /// <summary>Asks the window to confirm: the question, what will happen, the action's label, and what to do on yes.</summary>
    public event Action<string, string, string, Action>? ConfirmRequested;

    /// <summary>Asks the window for a notice: a title, a line of detail, and an optional action with its label.</summary>
    public event Action<string, string, string?, Action?>? NoticeRequested;

    /// <summary>The open scene's or page's text changed in the editor.</summary>
    public event Action<Node>? Edited;

    /// <summary>Close project, from the project menu: back to the Welcome screen.</summary>
    public event Action? CloseRequested;

    /// <summary>The selection, focus mode or the navigator changed, so the menu bar's enabled and checked items may too.</summary>
    public event Action? StateChanged;

    /// <summary>Before anything moves or closes a file: write what's in the editor.</summary>
    public event Action? FlushRequested;

    /// <summary>Show HTML: the window opens the sheet with a copied chapter's title and HTML.</summary>
    public event Action<ChapterCopy>? HtmlRequested;

    public bool FocusMode { get; private set; }

    public bool PreviewShown => _previewWanted && !FocusMode;

    /// <summary>The last chapter copied for AO3, for Show last HTML….</summary>
    public ChapterCopy? LastCopy { get; private set; }

    /// <summary>A chapter with scenes to copy: the selected one, or the selected scene's.</summary>
    public bool CanCopyChapter => CurrentChapter is { Children.Count: > 0 };

    /// <summary>The open scene's or page's file, or empty.</summary>
    public string OpenPath => Manuscript.Node is { IsFile: true } node && node.Parent is not null ? _project.PathOf(node) : "";

    public string SaveLabel
    {
        get => Manuscript.SaveLabel;
        set => Manuscript.SaveLabel = value;
    }

    /// <summary>Copies the editor's text into its scene or page.</summary>
    public void Commit() => Manuscript.Commit();

    public void Reload(Node node) => Manuscript.Reload(node);

    public bool NavigatorShown => NavigatorPane.IsVisible;

    public IReadOnlyList<NavigatorRow> Rows => (IReadOnlyList<NavigatorRow>?)Navigator.ItemsSource ?? [];

    public Node? Selected => (Navigator.SelectedItem as NavigatorRow)?.Node;

    public Node? Renaming => _renaming;

    public Project Project => _project;

    public AvaloniaEdit.TextEditor Editor => Manuscript.TextEditor;

    /// <summary>The scene or page open in the editor, if any.</summary>
    public Node? OpenDocument => Manuscript.Node is { IsFile: true } node ? node : null;

    /// <summary>The selected chapter, or the chapter the selected scene is in.</summary>
    public Node? CurrentChapter => ChapterOf(Selected);

    public void Wrap(string marker) => Manuscript.Wrap(marker);

    public void InsertSceneBreak() => Manuscript.InsertSceneBreak();

    public void OpenFind() => Manuscript.OpenFind();

    /// <summary>Copies a scene, page or chapter to "… copy" right after it and selects the copy, as the prototype does.</summary>
    public void Duplicate(Node node)
    {
        if (node.Kind is not (NodeKind.Scene or NodeKind.Page or NodeKind.Chapter))
        {
            return;
        }

        FlushRequested?.Invoke();
        Manuscript.Commit();
        Attempt($"Couldn't duplicate “{node.Title}”", () => AfterChange(_project.Duplicate(node)));
    }

    public void CollapseAll()
    {
        _collapsed.UnionWith(_project.Root.Descendants().Where(node => node.Children.Count > 0));
        var selected = Selected;
        Refresh();
        Select(selected is null ? null : Ancestors(selected).LastOrDefault() ?? selected);
    }

    public void Select(Node? node) => Navigator.SelectedItem = Rows.FirstOrDefault(row => row.Node == node);

    /// <summary>Puts keyboard focus on the selected row. The ListBox itself doesn't take focus, its rows do, and a new row exists only after layout.</summary>
    public void FocusNavigator()
    {
        Navigator.UpdateLayout();
        (Navigator.ContainerFromItem(Navigator.SelectedItem ?? new object()) ?? Navigator.ContainerFromIndex(0))?.Focus();
    }

    /// <summary>Focus mode hides the navigator and the header and centres the page lower, as the prototype does; it restores the navigator as it was.</summary>
    public void ToggleFocus()
    {
        FocusMode = !FocusMode;
        Header.Opacity = FocusMode ? 0 : 1;
        Header.IsHitTestVisible = !FocusMode;
        Columns.ColumnDefinitions[0].Width = new GridLength(!FocusMode && _navigatorWanted ? 236 : 0);
        NavigatorPane.IsVisible = !FocusMode && _navigatorWanted;
        ContentPane.Classes.Set("focus", FocusMode);
        Manuscript.FocusMode = FocusMode;
        ShowPreview();
        if (Manuscript.Node?.IsFile == true)
        {
            Manuscript.TextEditor.TextArea.Focus();
        }

        StateChanged?.Invoke();
    }

    public void ToggleNavigator()
    {
        _navigatorWanted = !_navigatorWanted;
        Columns.ColumnDefinitions[0].Width = new GridLength(_navigatorWanted ? 236 : 0);
        NavigatorPane.IsVisible = _navigatorWanted;
        NavigatorToggle.Classes.Set("selected", _navigatorWanted);
        AutomationProperties.SetName(NavigatorToggle, _navigatorWanted ? "Hide navigator" : "Show navigator");
        StateChanged?.Invoke();
    }

    public void TogglePreview()
    {
        if (PreviewShown)
        {
            _previewWidth = Columns.ColumnDefinitions[3].ActualWidth;
        }

        _previewWanted = !_previewWanted;
        ShowPreview();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Copy chapter HTML: the chapter's scenes as AO3 HTML, on the clipboard, with a notice that offers the HTML. A chapter over
    /// AO3's 500,000 characters still copies, with a warning.
    /// </summary>
    public async Task CopyChapterHtml(Node? chapter = null)
    {
        chapter ??= CurrentChapter;
        if (chapter is not { Kind: NodeKind.Chapter, Children.Count: > 0 })
        {
            return;
        }

        Manuscript.Commit();
        var scenes = chapter.Children.Select(scene => scene.Text).ToList();
        var html = Ao3Text.ChapterHtml(scenes);
        var number = Chapters().IndexOf(chapter) + 1;
        var warnings = html.Length > ChapterCopy.Limit
            ? new[] { $"This chapter is {html.Length:N0} characters of HTML, and AO3 takes up to {ChapterCopy.Limit:N0}. Split it into two chapters before posting." }
            : [];
        var copied = await CopyText(html);
        var copy = new ChapterCopy(number, chapter.Title, html, scenes.Count, scenes.Sum(Words.Count), copied, warnings);
        LastCopy = copy;
        StateChanged?.Invoke();
        if (!copied)
        {
            NoticeRequested?.Invoke("Couldn't reach the clipboard", "Open the HTML and copy it by hand.", "Show HTML", () => HtmlRequested?.Invoke(copy));
        }
        else if (warnings.Length > 0)
        {
            NoticeRequested?.Invoke($"Chapter {number} copied, but it's too long for AO3", "AO3 takes up to 500,000 characters a chapter.", "Show details", () => HtmlRequested?.Invoke(copy));
        }
        else
        {
            NoticeRequested?.Invoke($"Chapter {number} copied as HTML", "Paste into AO3's HTML editor, not Rich Text. The title goes in AO3's own field.", "Show HTML", () => HtmlRequested?.Invoke(copy));
        }
    }

    public async Task CopyChapterTitle()
    {
        if (CurrentChapter is { } chapter && await CopyText(chapter.Title))
        {
            NoticeRequested?.Invoke("Chapter title copied", "", null, null);
        }
    }

    public void ShowLastHtml()
    {
        if (LastCopy is { } copy)
        {
            HtmlRequested?.Invoke(copy);
        }
    }

    /// <summary>Puts text on the clipboard, and says whether it got there.</summary>
    public async Task<bool> CopyText(string text)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return false;
            }

            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A clipboard another app holds open refuses the text; the sheet still shows it to copy by hand.
            return false;
        }
    }

    // The preview shows when wanted and not in focus mode, at the width it had before it was hidden.
    private void ShowPreview()
    {
        var shown = PreviewShown;
        var column = Columns.ColumnDefinitions[3];
        column.MinWidth = shown ? 280 : 0;
        column.MaxWidth = shown ? 460 : 0;
        column.Width = new GridLength(shown ? _previewWidth : 0);
        Preview.IsVisible = shown;
        PreviewSplitter.IsVisible = shown;
        PreviewToggle.Classes.Set("selected", _previewWanted);
        AutomationProperties.SetName(PreviewToggle, _previewWanted ? "Hide preview" : "Show preview");
        ToolTip.SetTip(PreviewToggle, $"{(_previewWanted ? "Hide" : "Show")} preview ({Shortcut("Shift+P")})");
        UpdatePreview();
    }

    /// <summary>The chapter the selection belongs to, as AO3 shows it, or why there's nothing to show.</summary>
    private void UpdatePreview()
    {
        CopyHtml.IsEnabled = CanCopyChapter;
        ToolTip.SetTip(CopyHtml, CurrentChapter is { } current ? $"Copy Chapter {Chapters().IndexOf(current) + 1} as HTML for AO3 ({Shortcut("Shift+C")})" : "Select a scene or chapter to copy");
        if (!PreviewShown)
        {
            return;
        }

        var selected = Selected;
        switch (CurrentChapter)
        {
            case { Children.Count: > 0 } chapter:
                Manuscript.Commit();
                var scenes = chapter.Children.Select(scene => Ao3Text.Read(scene.Text)).ToList();
                Preview.ShowChapter(chapter.Title, $"Chapter {Chapters().IndexOf(chapter) + 1} · {Labels.Words(scenes.Sum(Ao3Text.Words))}", scenes);
                break;
            case not null:
                Preview.ShowEmpty("Nothing to preview yet.", "Add a scene to this chapter and it will appear here.");
                break;
            default:
                Preview.ShowEmpty(
                    selected?.Kind == NodeKind.Page ? "Notes aren't part of a chapter." : "Select a scene to preview its chapter.",
                    selected?.Kind == NodeKind.Page ? "The preview shows the chapter a scene belongs to." : "");
                break;
        }
    }

    private List<Control> PreviewMenu()
    {
        var chapter = CurrentChapter;
        var copy = new KeyGesture(Key.C, (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) | KeyModifiers.Shift);
        var hide = new KeyGesture(Key.P, (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) | KeyModifiers.Shift);
        var items = new List<Control>
        {
            Item("Copy chapter HTML", () => _ = CopyChapterHtml(), copy),
            Item("Copy chapter title", () => _ = CopyChapterTitle()),
            new Separator(),
            Item("Open chapter overview", () => Select(chapter)),
            new Separator(),
            Item("Hide preview", TogglePreview, hide),
        };
        items[0].IsEnabled = CanCopyChapter;
        items[1].IsEnabled = items[3].IsEnabled = chapter is not null;
        return items;
    }

    public void Toggle(Node node)
    {
        if (!_collapsed.Remove(node))
        {
            _collapsed.Add(node);
        }

        var selected = Selected;
        Refresh();
        // A row inside a collapsed folder goes away, so its folder takes the selection.
        Select(selected is null ? null : Ancestors(selected).Prepend(selected).Reverse().FirstOrDefault(_collapsed.Contains) ?? selected);
    }

    /// <summary>New scene: in the selected chapter, else the first one; a project with no chapter gets one instead.</summary>
    public void AddScene()
    {
        var chapter = ChapterOf(Selected) ?? Chapters().FirstOrDefault();
        if (chapter is null)
        {
            Add(ActOf(Selected) ?? _project.Root, NodeKind.Chapter);
        }
        else
        {
            Add(chapter, NodeKind.Scene);
        }
    }

    /// <summary>New chapter: in the selected act, or at the top level.</summary>
    public void AddChapter() => Add(ActOf(Selected) ?? _project.Root, NodeKind.Chapter);

    public void Add(Node parent, NodeKind kind) => Attempt($"Couldn't add a {kind.ToString().ToLowerInvariant()}", () =>
    {
        var node = _project.Add(parent, kind);
        _collapsed.Remove(parent);
        StartRename(node);
    });

    public void StartRename(Node node)
    {
        if (node.Kind is NodeKind.Notes or NodeKind.Research)
        {
            return;
        }

        _renaming = node;
        Refresh();
        Select(node);
        Dispatcher.UIThread.Post(
            () =>
            {
                if (Navigator.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.IsEffectivelyVisible) is { } box)
                {
                    box.Focus();
                    box.SelectAll();
                }
            },
            DispatcherPriority.Loaded);
    }

    public void CommitRename(string title)
    {
        if (_renaming is not { } node)
        {
            return;
        }

        _renaming = null;
        FlushRequested?.Invoke();
        Attempt($"Couldn't rename “{node.Title}”", () => _project.Rename(node, title));
        AfterChange(node);
    }

    public void CancelRename()
    {
        if (_renaming is { } node)
        {
            _renaming = null;
            AfterChange(node);
        }
    }

    /// <summary>Asks first, as the prototype does; the file or folder then moves to Trash/ with Undo in a notice.</summary>
    public void AskDelete(Node node)
    {
        if (node.Kind is NodeKind.Notes or NodeKind.Research or NodeKind.Project)
        {
            return;
        }

        var what = node.Kind.ToString().ToLowerInvariant();
        var count = node.Children.Count;
        var detail = node.IsFile
            ? $"The {what}'s file moves to the project's Trash folder. You can restore it from there."
            : $"The {what} and its {count} item{(count == 1 ? "" : "s")} move to the project's Trash folder. Nothing is erased.";
        ConfirmRequested?.Invoke($"Delete “{node.Title}”?", detail, "Delete", () => Delete(node));
    }

    public void Delete(Node node)
    {
        var rows = Rows.ToList();
        var at = rows.FindIndex(row => row.Node == node);
        var selected = Selected;
        var fallback = rows.Take(at).Reverse().FirstOrDefault(row => !IsGroup(row.Node))?.Node
            ?? rows.Skip(at + 1).FirstOrDefault(row => !IsGroup(row.Node) && !IsWithin(row.Node, node))?.Node;
        FlushRequested?.Invoke();
        Attempt($"Couldn't delete “{node.Title}”", () =>
        {
            var deleted = _project.Delete(node);
            Refresh();
            Select(selected is not null && IsWithin(selected, node) ? fallback : selected);
            NoticeRequested?.Invoke($"Moved “{node.Title}” to Trash", "", "Undo", () => Attempt($"Couldn't put “{node.Title}” back", () =>
            {
                _project.Restore(deleted);
                AfterChange(node);
            }));
        });
    }

    /// <summary>Alt+Up and Alt+Down: one place among its neighbours. A scene or page at the edge moves into the next chapter or group.</summary>
    public void Nudge(Node node, int delta)
    {
        if (node.Parent is not { } parent || node.Kind is NodeKind.Notes or NodeKind.Research)
        {
            return;
        }

        var target = parent.Children.IndexOf(node) + delta;
        if (target >= 0 && target < _project.EndOf(parent))
        {
            Move(node, parent, target);
        }
        else if (node.IsFile)
        {
            var holders = _project.Root.Descendants().Where(candidate => Project.CanHold(candidate, node.Kind)).ToList();
            var next = holders.IndexOf(parent) + delta;
            if (next >= 0 && next < holders.Count)
            {
                Move(node, holders[next], delta < 0 ? int.MaxValue : 0);
            }
        }
    }

    public void Move(Node node, Node parent, int index) => Attempt($"Couldn't move “{node.Title}”", () =>
    {
        FlushRequested?.Invoke();
        _project.Move(node, parent, index);
        _collapsed.Remove(parent);
        AfterChange(node);
    });

    /// <summary>Where a drop on a row would put the dragged node, following the prototype's rules; null when it can't go there.</summary>
    public static Placement? DropOn(Node dragged, Node target, double fraction)
    {
        var where = fraction < 0.3 ? Placement.Before : fraction > 0.7 ? Placement.After : Placement.Into;
        if (Allowed(dragged, target, where))
        {
            return where;
        }

        where = fraction < 0.5 ? Placement.Before : Placement.After;
        return Allowed(dragged, target, where) ? where : null;
    }

    public void Drop(Node dragged, Node target, Placement where)
    {
        if (where == Placement.Into)
        {
            Move(dragged, target, int.MaxValue);
        }
        else
        {
            var siblings = target.Parent!.Children.Where(child => child != dragged).ToList();
            Move(dragged, target.Parent, siblings.IndexOf(target) + (where == Placement.After ? 1 : 0));
        }
    }

    private static bool Allowed(Node dragged, Node target, Placement where) =>
        dragged != target && (where == Placement.Into
            ? Project.CanHold(target, dragged.Kind)
            : target.Kind == dragged.Kind && target.Parent is { } parent && Project.CanHold(parent, dragged.Kind));

    private static NavigatorRow? Row(object? source) => (source as StyledElement)?.DataContext as NavigatorRow;

    private static bool IsGroup(Node node) => node.Kind is NodeKind.Notes or NodeKind.Research;

    private static bool IsWithin(Node node, Node ancestor) => node == ancestor || Ancestors(node).Contains(ancestor);

    private static IEnumerable<Node> Ancestors(Node node)
    {
        for (var parent = node.Parent; parent?.Parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }

    private static Node? ChapterOf(Node? node) => node is null ? null : Ancestors(node).Prepend(node).FirstOrDefault(n => n.Kind == NodeKind.Chapter);

    private static Node? ActOf(Node? node) => node is null ? null : Ancestors(node).Prepend(node).FirstOrDefault(n => n.Kind == NodeKind.Act);

    private static MenuItem Item(string header, Action action, KeyGesture? gesture = null, bool danger = false)
    {
        var item = new MenuItem { Header = header, InputGesture = gesture };
        item.Classes.Set("danger", danger);
        item.Click += (_, _) => action();
        return item;
    }

    private List<Node> Chapters() => _project.Root.Descendants().Where(node => node.Kind == NodeKind.Chapter).ToList();

    private List<Control> MenuFor(Node node)
    {
        var rename = OperatingSystem.IsMacOS() ? new KeyGesture(Key.Enter) : new KeyGesture(Key.F2);
        var delete = OperatingSystem.IsMacOS() ? new KeyGesture(Key.Back, KeyModifiers.Meta) : new KeyGesture(Key.Delete);
        var duplicate = new KeyGesture(Key.D, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        return node.Kind switch
        {
            NodeKind.Scene or NodeKind.Page =>
                [Item("Rename", () => StartRename(node), rename), Item("Duplicate", () => Duplicate(node), duplicate), new Separator(), Item(node.Kind == NodeKind.Scene ? "Delete scene" : "Delete page", () => AskDelete(node), delete, danger: true)],
            NodeKind.Chapter =>
                [Item("New scene", () => Add(node, NodeKind.Scene)), Item("Rename", () => StartRename(node), rename), Item("Duplicate", () => Duplicate(node)), new Separator(), Item("Copy chapter HTML", () => _ = CopyChapterHtml(node), new KeyGesture(Key.C, (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) | KeyModifiers.Shift)), new Separator(), Item("Delete chapter", () => AskDelete(node), delete, danger: true)],
            NodeKind.Act =>
                [Item("New chapter", () => Add(node, NodeKind.Chapter)), Item("Rename", () => StartRename(node), rename), new Separator(), Item("Delete act", () => AskDelete(node), delete, danger: true)],
            _ => [Item("New page", () => Add(node, NodeKind.Page))],
        };
    }

    private List<Control> AddMenu()
    {
        var notes = _project.Root.Children.First(node => node.Kind == NodeKind.Notes);
        return
        [
            Item("New scene", AddScene),
            Item("New chapter", AddChapter),
            Item("New act", () => Add(_project.Root, NodeKind.Act)),
            new Separator(),
            Item("New note", () => Add(notes, NodeKind.Page)),
            Item("Collapse all", CollapseAll),
        ];
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_renaming is not null)
        {
            // Inside the rename box: Enter keeps the new title, Escape the old one, and Up and Down stay put.
            if (e.Key == Key.Enter && e.Source is TextBox box)
            {
                CommitRename(box.Text ?? "");
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelRename();
                e.Handled = true;
            }
            else if (e.Key is Key.Up or Key.Down)
            {
                e.Handled = true;
            }

            return;
        }

        if (Selected is not { } node)
        {
            return;
        }

        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        switch (e.Key)
        {
            case Key.Up or Key.Down when e.KeyModifiers == KeyModifiers.Alt:
                Nudge(node, e.Key == Key.Up ? -1 : 1);
                break;
            case Key.D when e.KeyModifiers == command:
                Duplicate(node);
                break;
            case Key.F2 or Key.Enter when e.KeyModifiers == KeyModifiers.None:
                StartRename(node);
                break;
            case Key.Delete when e.KeyModifiers == KeyModifiers.None:
            case Key.Back when e.KeyModifiers is KeyModifiers.None || e.KeyModifiers == command:
                AskDelete(node);
                break;
            case Key.Right when e.KeyModifiers == KeyModifiers.None && node.Children.Count > 0 && _collapsed.Contains(node):
                Toggle(node);
                FocusNavigator();
                break;
            case Key.Left when e.KeyModifiers == KeyModifiers.None:
                if (node.Children.Count > 0 && !_collapsed.Contains(node))
                {
                    Toggle(node);
                }
                else if (Ancestors(node).FirstOrDefault() is { } parent)
                {
                    Select(parent);
                }

                FocusNavigator();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnPress(object? sender, PointerPressedEventArgs e)
    {
        _press = null;
        if (_renaming is null && e.GetCurrentPoint(Navigator).Properties.IsLeftButtonPressed && Row(e.Source) is { IsDraggable: true } row)
        {
            _press = (row.Node, e.GetPosition(Navigator));
        }
    }

    private void OnDrag(object? sender, PointerEventArgs e)
    {
        if (_press is not { } press)
        {
            return;
        }

        var at = e.GetPosition(Navigator);
        if (!_dragging)
        {
            if (Math.Abs(at.X - press.Start.X) < DragThreshold && Math.Abs(at.Y - press.Start.Y) < DragThreshold)
            {
                return;
            }

            _dragging = true;
            e.Pointer.Capture(Navigator);
        }

        _drop = null;
        foreach (var container in Navigator.GetRealizedContainers().OfType<ListBoxItem>())
        {
            var top = container.TranslatePoint(default, Navigator)!.Value.Y;
            if (container.DataContext is NavigatorRow row && at.Y >= top && at.Y < top + container.Bounds.Height
                && DropOn(press.Node, row.Node, (at.Y - top) / container.Bounds.Height) is { } where)
            {
                _drop = (row.Node, where);
                ShowDrop(container, row, where);
                break;
            }
        }

        if (_drop is null)
        {
            DropLine.IsVisible = DropBox.IsVisible = false;
        }

        e.Handled = true;
    }

    private void OnRelease(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging && _press is { } press && _drop is { } drop)
        {
            e.Handled = true;
            EndDrag();
            e.Pointer.Capture(null);
            Drop(press.Node, drop.Target, drop.Where);
            return;
        }

        EndDrag();
        if (e.Pointer.Captured == Navigator)
        {
            e.Pointer.Capture(null);
        }
    }

    private void EndDrag()
    {
        _press = null;
        _dragging = false;
        _drop = null;
        DropLine.IsVisible = DropBox.IsVisible = false;
    }

    private void ShowDrop(ListBoxItem container, NavigatorRow row, Placement where)
    {
        var origin = container.TranslatePoint(default, DropLayer)!.Value;
        var size = container.Bounds.Size;
        DropBox.IsVisible = where == Placement.Into;
        DropLine.IsVisible = where != Placement.Into;
        if (where == Placement.Into)
        {
            Canvas.SetLeft(DropBox, origin.X);
            Canvas.SetTop(DropBox, origin.Y);
            DropBox.Width = size.Width;
            DropBox.Height = size.Height;
        }
        else
        {
            Canvas.SetLeft(DropLine, origin.X + row.Indent.Left);
            Canvas.SetTop(DropLine, origin.Y + (where == Placement.Before ? -1 : size.Height - 1));
            DropLine.Width = Math.Max(0, size.Width - row.Indent.Left - 6);
        }
    }

    private void AfterChange(Node node)
    {
        Refresh();
        Select(node);
        FocusNavigator();
    }

    private void Attempt(string failure, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Refresh();
            NoticeRequested?.Invoke(failure, error.Message, null, null);
        }
    }

    private void Refresh()
    {
        var chapters = Chapters();
        var rows = new List<NavigatorRow>();
        Add(_project.Root, 0);
        Navigator.ItemsSource = rows;
        ShowSelected();

        void Add(Node parent, int depth)
        {
            foreach (var node in parent.Children)
            {
                var expanded = !_collapsed.Contains(node);
                rows.Add(new NavigatorRow(node, depth, chapters.IndexOf(node) + 1, expanded, rows.Count == 0, node == _renaming));
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
        var chapter = ChapterOf(node);
        var chapterLabel = chapter is null ? "" : $"Chapter {Chapters().IndexOf(chapter) + 1}";
        HeaderTitle.Text = node?.Title ?? _project.Title;
        HeaderSub.Text = node?.Kind switch
        {
            NodeKind.Scene => $"{chapterLabel} — {chapter!.Title}",
            NodeKind.Chapter => chapterLabel,
            NodeKind.Page => node.Parent!.Title,
            NodeKind.Act => "Act",
            _ => "",
        };
        if (node != Manuscript.Node)
        {
            FlushRequested?.Invoke();
        }

        Manuscript.Show(_project, node);
        UpdatePreview();
        StateChanged?.Invoke();
    }

    private static string Shortcut(string keys) => OperatingSystem.IsMacOS() ? "⌘" + keys.Replace("Shift+", "⇧", StringComparison.Ordinal) : "Ctrl+" + keys;

    private void OnWorkspaceKey(object? sender, KeyEventArgs e)
    {
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.F && e.KeyModifiers == (command | KeyModifiers.Shift))
        {
            ToggleFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.OemPipe && e.KeyModifiers == command && !FocusMode)
        {
            ToggleNavigator();
            e.Handled = true;
        }
    }
}
