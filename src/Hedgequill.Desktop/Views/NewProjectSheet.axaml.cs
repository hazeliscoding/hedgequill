using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Hedgequill.Desktop.Views;

public partial class NewProjectSheet : UserControl
{
    public NewProjectSheet()
    {
        InitializeComponent();
        Location = DefaultLocation;
        Create.Click += (_, _) => Submit();
        Cancel.Click += (_, _) => Hide();
        Choose.Click += async (_, _) =>
        {
            var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a location" });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
            {
                Location = folder;
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Submit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
    }

    /// <summary>The project title and the folder to create it in.</summary>
    public event Action<string, string>? CreateRequested;

    public static string DefaultLocation => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Stories");

    public string Location
    {
        get;
        set
        {
            field = value;
            LocationText.Text = Paths.Display(value);
        }
    } = "";

    public string ProjectTitle
    {
        get => TitleBox.Text ?? "";
        set => TitleBox.Text = value;
    }

    public void Show()
    {
        ProjectTitle = "";
        IsVisible = true;
        // Focus waits for layout: a control that was hidden can't take it yet.
        Dispatcher.UIThread.Post(() => TitleBox.Focus(), DispatcherPriority.Loaded);
    }

    public void Hide() => IsVisible = false;

    public void Submit() => CreateRequested?.Invoke(Location, ProjectTitle);
}
