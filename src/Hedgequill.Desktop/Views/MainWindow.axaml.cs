using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly RecentProjects _recent;
    private Saver? _saver;
    private bool _closeAnyway;

    public MainWindow()
        : this(new RecentProjects(RecentProjects.DefaultFolder))
    {
    }

    public MainWindow(RecentProjects recent)
    {
        _recent = recent;
        InitializeComponent();
        Sheet.CreateRequested += (location, title) =>
        {
            if (Create(location, title))
            {
                Sheet.Hide();
            }
        };
        ShowWelcome();
    }

    public Project? Project { get; private set; }

    public Saver? Saver => _saver;

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
        if (_saver is { } saver)
        {
            saver.Flush();
            if (saver.HasUnsaved)
            {
                Notice.Show("The project is still open", "Some changes couldn't be written to disk yet. Retry, or save a copy, from the save state at the top.");
                return;
            }
        }

        ShowWelcome();
    }

    public void ShowWelcome()
    {
        _saver?.Flush();
        _saver = null;
        Project = null;
        Title = "Hedgequill";
        var now = DateTimeOffset.Now;
        var welcome = new WelcomeView(_recent.Load().Where(entry => Directory.Exists(entry.Folder)).Select(entry => RecentRow.From(entry, now)));
        welcome.NewProjectRequested += Sheet.Show;
        welcome.OpenFolderRequested += () => _ = PickFolderAsync();
        welcome.RecentChosen += folder => Open(folder);
        Screen.Content = welcome;
    }

    public bool Open(string folder) => Attempt("Couldn't open the project", () => Show(Project.Open(folder)));

    public bool Create(string location, string title) => Attempt("Couldn't create the project", () => Show(Project.Create(location, title)));

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
            if (Screen.Content is WorkspaceView workspace)
            {
                workspace.Commit();
            }

            Project? copy = null;
            if (Attempt("Couldn't save the project there", () => copy = project.SaveAs(folder)) && copy is not null)
            {
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
