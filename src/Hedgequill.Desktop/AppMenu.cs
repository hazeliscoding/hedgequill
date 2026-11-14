using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;
using Hedgequill.Desktop.Views;

namespace Hedgequill.Desktop;

/// <summary>
/// The prototype's menu bar: File, Edit, View and Chapter. It's one NativeMenu: macOS shows it at the top of the screen,
/// and Windows in the window's caption row. Help and the AO3 commands arrive with M3 and M5, when they do something.
/// </summary>
public sealed class AppMenu
{
    private static readonly KeyModifiers Command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private readonly MainWindow _window;
    private readonly List<Action> _refresh = [];
    private readonly List<(KeyGesture Gesture, Relay Command)> _shortcuts = [];
    private readonly NativeMenuItem _recent;

    public AppMenu(MainWindow window)
    {
        _window = window;
        bool Open() => Workspace is not null;
        bool Document() => Workspace?.OpenDocument is not null;
        bool Scene() => Workspace?.OpenDocument?.Kind == NodeKind.Scene;
        bool Renamable() => Workspace?.Selected is { Kind: not (NodeKind.Notes or NodeKind.Research) };
        bool Chapter() => Workspace?.CurrentChapter is not null;

        _recent = new NativeMenuItem("Open recent") { Menu = [] };
        var file = Submenu(
            "File",
            Item("New project…", window.NewProject, Gesture(Key.N, KeyModifiers.Shift)),
            Item("Open folder…", window.OpenFolder, Gesture(Key.O)),
            _recent,
            new NativeMenuItemSeparator(),
            Item("New scene", () => Workspace?.AddScene(), Gesture(Key.N), Open),
            Item("New chapter", () => Workspace?.AddChapter(), enabled: Open),
            new NativeMenuItemSeparator(),
            Item("Show project folder", window.ShowProjectFolder, enabled: Open),
            Item("Close project", window.CloseProject, enabled: Open));
        if (!OperatingSystem.IsMacOS())
        {
            // macOS puts Quit in the app menu itself; Windows closes on Alt+F4 without help.
            file.Menu!.Add(new NativeMenuItemSeparator());
            file.Menu.Add(new NativeMenuItem("Exit") { Gesture = new KeyGesture(Key.F4, KeyModifiers.Alt), Command = new Relay(window.Close, () => true) });
        }

        var edit = Submenu(
            "Edit",
            Item("Undo", window.Undo, Gesture(Key.Z), Open),
            Item("Redo", window.Redo, Gesture(Key.Z, KeyModifiers.Shift), Open),
            new NativeMenuItemSeparator(),
            Item("Cut", window.Cut, Gesture(Key.X), Open),
            Item("Copy", window.Copy, Gesture(Key.C), Open),
            Item("Paste", window.Paste, Gesture(Key.V), Open),
            new NativeMenuItemSeparator(),
            Item("Bold", () => Workspace?.Wrap(Markdown.Bold), Gesture(Key.B), Document),
            Item("Italic", () => Workspace?.Wrap(Markdown.Italic), Gesture(Key.I), Document),
            Item("Insert scene break", () => Workspace?.InsertSceneBreak(), Gesture(Key.Enter), Scene),
            new NativeMenuItemSeparator(),
            Item("Rename", () => Workspace?.StartRename(Workspace.Selected!), new KeyGesture(Key.F2), Renamable),
            Item("Find…", () => Workspace?.OpenFind(), Gesture(Key.F), Document));

        var view = Submenu(
            "View",
            Toggle("Navigator", () => Workspace?.ToggleNavigator(), () => Workspace?.NavigatorShown == true, Gesture(Key.OemPipe), () => Open() && Workspace?.FocusMode != true),
            Toggle("Focus mode", () => Workspace?.ToggleFocus(), () => Workspace?.FocusMode == true, Gesture(Key.F, KeyModifiers.Shift), Open),
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Appearance") { IsEnabled = false },
            Choice("Match system", Appearance.System),
            Choice("Light", Appearance.Light),
            Choice("Dark", Appearance.Dark),
            new NativeMenuItemSeparator(),
            Item("Collapse all", () => Workspace?.CollapseAll(), enabled: Open));

        var chapter = Submenu(
            "Chapter",
            Item("Open chapter overview", () => Workspace?.Select(Workspace.CurrentChapter), enabled: Chapter),
            Item("Duplicate chapter", () => Workspace?.Duplicate(Workspace.CurrentChapter!), enabled: Chapter),
            new NativeMenuItemSeparator(),
            Item("Delete chapter", () => Workspace?.AskDelete(Workspace.CurrentChapter!), enabled: Chapter));

        Menu = [file, edit, view, chapter];
    }

    public NativeMenu Menu { get; }

    private WorkspaceView? Workspace => _window.Workspace;

    /// <summary>Re-reads which items are enabled and checked: after a project opens or closes, the selection changes, or a view toggles.</summary>
    public void Update()
    {
        foreach (var refresh in _refresh)
        {
            refresh();
        }
    }

    /// <summary>Lists the Recent projects that still exist under File › Open recent.</summary>
    public void UpdateRecent(IReadOnlyList<RecentProject> recent)
    {
        _recent.Menu!.Items.Clear();
        foreach (var project in recent)
        {
            _recent.Menu.Add(new NativeMenuItem(project.Title) { Command = new Relay(() => _window.Open(project.Folder), () => true) });
        }

        _recent.IsEnabled = recent.Count > 0;
    }

    /// <summary>
    /// Runs the item whose shortcut this is. The Windows menu bar only shows shortcuts, so the window passes its unhandled keys
    /// here; on macOS this runs before the menu would, so nothing fires twice.
    /// </summary>
    public bool TryShortcut(KeyEventArgs e)
    {
        foreach (var (gesture, command) in _shortcuts)
        {
            if (gesture.Matches(e) && command.CanExecute(null))
            {
                command.Execute(null);
                return true;
            }
        }

        return false;
    }

    private static KeyGesture Gesture(Key key, KeyModifiers more = KeyModifiers.None) => new(key, Command | more);

    private static NativeMenuItem Submenu(string header, params NativeMenuItemBase[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items)
        {
            menu.Add(item);
        }

        return new NativeMenuItem(header) { Menu = menu };
    }

    private NativeMenuItem Item(string header, Action action, KeyGesture? gesture = null, Func<bool>? enabled = null)
    {
        var command = new Relay(action, enabled ?? (() => true));
        _refresh.Add(command.Raise);
        if (gesture is not null)
        {
            _shortcuts.Add((gesture, command));
        }

        return new NativeMenuItem(header) { Gesture = gesture, Command = command };
    }

    private NativeMenuItem Toggle(string header, Action action, Func<bool> isChecked, KeyGesture gesture, Func<bool> enabled)
    {
        var item = Item(header, action, gesture, enabled);
        item.ToggleType = MenuItemToggleType.CheckBox;
        _refresh.Add(() => item.IsChecked = isChecked());
        return item;
    }

    private NativeMenuItem Choice(string header, Appearance appearance)
    {
        var item = Item(header, () => _window.Appearance = appearance);
        item.ToggleType = MenuItemToggleType.Radio;
        _refresh.Add(() => item.IsChecked = _window.Appearance == appearance);
        return item;
    }

    /// <summary>A menu command; Raise re-asks whether it can run, which enables or disables its item.</summary>
    private sealed class Relay(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute();

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
