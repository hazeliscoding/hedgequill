using Avalonia.Controls;

namespace Hedgequill.Desktop.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView(IEnumerable<RecentRow> recent)
    {
        InitializeComponent();
        var rows = recent.ToList();
        RecentList.ItemsSource = rows;
        RecentList.IsVisible = rows.Count > 0;
        NoRecent.IsVisible = rows.Count == 0;
        NewProject.Click += (_, _) => NewProjectRequested?.Invoke();
        OpenFolder.Click += (_, _) => OpenFolderRequested?.Invoke();
        RecentList.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Control { DataContext: RecentRow row })
            {
                RecentChosen?.Invoke(row.Folder);
            }
        });
    }

    public event Action? NewProjectRequested;

    public event Action? OpenFolderRequested;

    public event Action<string>? RecentChosen;
}
