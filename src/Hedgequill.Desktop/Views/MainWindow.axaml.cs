using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly RecentProjects _recent;

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

    public void ShowWelcome()
    {
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

    private async Task PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open folder" });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
        {
            Open(folder);
        }
    }
}
