using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly RecentProjects _recent;
    private readonly Preferences _preferences;
    private readonly AppMenu _menu;
    private Saver? _saver;
    private bool _closeAnyway;
    private Control? _lastText;

    public MainWindow()
        : this(RecentProjects.DefaultFolder)
    {
    }

    /// <param name="settingsFolder">Where the Recent list and settings.json live.</param>
    public MainWindow(string settingsFolder)
    {
        _recent = new RecentProjects(settingsFolder);
        _preferences = new Preferences(settingsFolder);
        InitializeComponent();

        // The prototype's Windows frame: icon, menu bar and title share the caption row. macOS keeps its own title bar and shows the menu at the top of the screen.
        CaptionRow.IsVisible = !OperatingSystem.IsMacOS();
        if (OperatingSystem.IsWindows())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 32;
        }

        _menu = new AppMenu(this);
        NativeMenu.SetMenu(this, _menu.Menu);
        if (_preferences.Appearance != Appearance.System)
        {
            Apply(_preferences.Appearance);
        }

        Sheet.CreateRequested += (location, title) =>
        {
            if (Create(location, title))
            {
                Sheet.Hide();
            }
        };
        AddHandler(GotFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox box)
            {
                _lastText = box;
            }
            else if (e.Source is Visual visual && visual.FindAncestorOfType<TextEditor>(includeSelf: true) is { } editor)
            {
                _lastText = editor;
            }
        });
        KeyDown += (_, e) => e.Handled = e.Handled || _menu.TryShortcut(e);
        ShowWelcome();
    }

    public Project? Project { get; private set; }

    public Saver? Saver => _saver;

    public AppMenu Menu => _menu;

    public WorkspaceView? Workspace => Screen.Content as WorkspaceView;

    /// <summary>View › Appearance, remembered in settings.json.</summary>
    public Appearance Appearance
    {
        get => _preferences.Appearance;
        set
        {
            try
            {
                _preferences.Appearance = value;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The choice still applies now; it just won't be remembered.
            }

            Apply(value);
            _menu.Update();
        }
    }

    public void NewProject() => Sheet.Show();

    public void OpenFolder() => _ = PickFolderAsync();

    /// <summary>File › Show project folder: the one thing Hedgequill launches, the system's file manager on a local folder.</summary>
    public void ShowProjectFolder()
    {
        if (Project is { } project)
        {
            _ = TopLevel.GetTopLevel(this)!.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(project.Folder));
        }
    }

    public void Undo() => Edit(box => box.Undo(), editor => editor.Undo());

    public void Redo() => Edit(box => box.Redo(), editor => editor.Redo());

    public void Cut() => Edit(box => box.Cut(), editor => editor.Cut());

    public void Copy() => Edit(box => box.Copy(), editor => editor.Copy());

    public void Paste() => Edit(box => box.Paste(), editor => editor.Paste());

    /// <summary>Writes what's unsaved first. Text that still can't be written stops the close until the writer chooses to lose it.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _saver?.Flush();
        if (_saver is { HasUnsaved: true } && !_closeAnyway)
        {
            e.Cancel = true;
            Confirmation.Show(
                "Close without saving?",
                "Some changes couldn't be written to disk. Save a copy first, or close and lose them.",
                "Close anyway",
                () =>
                {
                    _closeAnyway = true;
                    Close();
                });
        }

        base.OnClosing(e);
    }

    /// <summary>Close project: writes what's unsaved, then shows the Welcome screen. Text that can't be written keeps the project open, with its state showing why.</summary>
    public void CloseProject()
    {
        if (CanLeaveProject())
        {
            ShowWelcome();
        }
    }

    public void ShowWelcome()
    {
        _saver?.Flush();
        _saver = null;
        Project = null;
        Title = "Hedgequill";
        var now = DateTimeOffset.Now;
        var recent = Recent();
        var welcome = new WelcomeView(recent.Select(entry => RecentRow.From(entry, now)));
        welcome.NewProjectRequested += Sheet.Show;
        welcome.OpenFolderRequested += () => _ = PickFolderAsync();
        welcome.RecentChosen += folder => Open(folder);
        Screen.Content = welcome;
        _menu.UpdateRecent(recent);
        _menu.Update();
    }

    public bool Open(string folder) => CanLeaveProject() && Attempt("Couldn't open the project", () => Show(Project.Open(folder)));

    public bool Create(string location, string title) => CanLeaveProject() && Attempt("Couldn't create the project", () => Show(Project.Create(location, title)));

    private static void Apply(Appearance appearance) => Application.Current!.RequestedThemeVariant = appearance switch
    {
        Appearance.Light => ThemeVariant.Light,
        Appearance.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    private List<RecentProject> Recent() => _recent.Load().Where(entry => Directory.Exists(entry.Folder)).ToList();

    /// <summary>Before another project replaces this one: write what's unsaved, and stay if it can't be written.</summary>
    private bool CanLeaveProject()
    {
        if (_saver is not { } saver)
        {
            return true;
        }

        saver.Flush();
        if (!saver.HasUnsaved)
        {
            return true;
        }

        Notice.Show("The project is still open", "Some changes couldn't be written to disk yet. Retry, or save a copy, from the save state at the top.");
        return false;
    }

    /// <summary>Edit menu commands act on the text field or editor that last had focus, since opening a menu can take focus away.</summary>
    private void Edit(Action<TextBox> box, Action<TextEditor> editor)
    {
        switch (_lastText)
        {
            case TextBox field when field.IsEffectivelyVisible && TopLevel.GetTopLevel(field) == this:
                box(field);
                break;
            case TextEditor text when text.IsEffectivelyVisible && TopLevel.GetTopLevel(text) == this:
                editor(text);
                break;
            default:
                if (Workspace is { OpenDocument: not null } workspace)
                {
                    editor(workspace.Editor);
                }

                break;
        }
    }

    private void Show(Project project)
    {
        Project = project;
        try
        {
            _recent.Add(project, DateTimeOffset.Now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Recent is a convenience; failing to update it mustn't keep the project closed.
        }

        Title = $"{project.Title} — Hedgequill";
        var workspace = new WorkspaceView(project);
        workspace.ConfirmRequested += Confirmation.Show;
        workspace.NoticeRequested += (title, detail, label, action) => Notice.Show(title, detail, label, action);

        var saver = new Saver(project, workspace.Commit, TimeSpan.FromMilliseconds(700));
        workspace.Edited += saver.Edited;
        workspace.FlushRequested += saver.Flush;
        workspace.CloseRequested += CloseProject;
        workspace.StateChanged += _menu.Update;
        saver.Reloaded += workspace.Reload;
        var status = new SaveStatus();
        status.Attach(saver, () => workspace.OpenPath);
        saver.Changed += () => workspace.SaveLabel = status.Text;
        workspace.SaveLabel = status.Text;
        status.SaveCopyRequested += node => _ = SaveCopyAsync(node);
        status.ChooseFolderRequested += () => _ = ChooseFolderAsync(project);
        workspace.SaveSlot.Content = status;
        _saver = saver;
        Screen.Content = workspace;
        _menu.UpdateRecent(Recent());
        _menu.Update();
    }

    private bool Attempt(string failure, Action action)
    {
        try
        {
            action();
            Notice.Hide();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Notice.Show(failure, error.Message);
            return false;
        }
    }

    private async Task SaveCopyAsync(Node node)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save a copy", SuggestedFileName = node.Name, DefaultExtension = "md" });
        if (file?.TryGetLocalPath() is { } path)
        {
            _saver?.Flush();
            if (Attempt("Couldn't save a copy", () => Project.SaveCopy(node, path)))
            {
                Notice.Show("Saved a copy", path);
            }
        }
    }

    /// <summary>Read-only: copies the project, with the text in memory, into a folder the writer picks, and carries on there.</summary>
    private async Task ChooseFolderAsync(Project project)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a writable folder" });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
        {
            Workspace?.Commit();
            Project? copy = null;
            if (Attempt("Couldn't save the project there", () => copy = project.SaveAs(folder)) && copy is not null)
            {
                // The edits that couldn't be written are in the copy now, so the old project can close.
                _saver = null;
                Show(copy);
                Notice.Show("Saved the project in a new folder", copy.Folder);
            }
        }
    }

    private async Task PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open folder" });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
        {
            Open(folder);
        }
    }
}
